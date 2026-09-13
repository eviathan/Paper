namespace Paper.Core.Dock
{
    // ── DockWindowSession ─────────────────────────────────────────────────────
    //
    // Coordinates dock operations across multiple OS windows in the same process.
    //
    // Setup:
    //   var session = new DockWindowSession(panels);
    //   session.NewWindowRequested += (panelId, state) => { /* create new Canvas */ };
    //   session.WindowEmptied      += windowId          => { /* close that Canvas */ };
    //
    //   // Per Canvas:
    //   canvas.DockSession = session;
    //   DockContext.Root(panels, session: session, windowId: "main", ...)
    //
    // Cross-window drag flow:
    //   1. Panel drag starts in Window A → BeginCrossWindowDrag()
    //   2. Cursor enters Window B → Canvas B detects active drag via IsCrossWindowDragActive
    //   3. Drop in Window B → dispatch DockAcceptExternalPanel → CompleteCrossWindowDrop()
    //   4. CompleteCrossWindowDrop fires removeFromSource → panel removed from Window A
    //
    // Screen-position drop flow (macOS implicit grab workaround):
    //   1. Drag ends outside source window → DockContext.HandleEject is called
    //   2. HandleEject calls TryExternalDrop(sourceWindowId, panel, screenX, screenY)
    //   3. If screenX/Y falls within a registered window's bounds → ExternalPanelArrived fired
    //   4. Target DockContext.Root listens and dispatches DockAcceptExternalPanelOuter
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class DockWindowSession
    {
        private string? _crossDragPanelId;
        private string? _crossDragSourceWindowId;
        private Action? _crossDragRemoveFromSource;

        // ── Window bounds registry (for screen-position drop detection) ────────
        private readonly Dictionary<string, Func<(int x, int y, int w, int h)>> _windowBounds = new();

        /// <summary>True while a panel is being dragged between windows.</summary>
        public bool IsCrossWindowDragActive => _crossDragPanelId != null;

        /// <summary>
        /// Most recent screen-space cursor position during a cross-window drag.
        /// Updated by the source window on every mouse-move event.
        /// Other windows poll this to render the drag ghost at the correct position.
        /// </summary>
        public int CrossDragCursorScreenX { get; private set; }
        /// <inheritdoc cref="CrossDragCursorScreenX"/>
        public int CrossDragCursorScreenY { get; private set; }

        /// <summary>Called by the source window on every mouse-move during a cross-window drag.</summary>
        public void UpdateCrossWindowCursorPosition(int screenX, int screenY)
        {
            CrossDragCursorScreenX = screenX;
            CrossDragCursorScreenY = screenY;
        }

        /// <summary>PanelId being dragged across windows, or null if none.</summary>
        public string? CrossDragPanelId => _crossDragPanelId;

        /// <summary>WindowId the cross-window drag originated from.</summary>
        public string? CrossDragSourceWindowId => _crossDragSourceWindowId;

        /// <summary>Fired when the cross-window drag state changes (begin or end).</summary>
        public event Action? DragStateChanged;

        /// <summary>
        /// Fired when a panel was dropped at a screen position that falls within a registered window's bounds.
        /// Parameters: targetWindowId, panel, window-relative X, window-relative Y, window width, window height.
        /// Each DockContext.Root subscribes and dispatches DockAcceptExternalPanelOuter when its windowId matches.
        /// </summary>
        public event Action<string, PanelNode, int, int, int, int>? ExternalPanelArrived;

        /// <summary>
        /// Fired when a panel was dragged outside every window and should open in a new OS window.
        /// Handler should create a new Canvas and call DockContext.Root with the provided state.
        /// <para>
        /// Parameters: panelId, initialState, approxScreenX, approxScreenY
        /// (coordinates are window-relative drag-end position — use as a hint for <c>Canvas.InitialPosition</c>).
        /// </para>
        /// </summary>
        public event Action<string, DockState, int, int>? NewWindowRequested;

        /// <summary>
        /// Fired when a window's layout became empty after a panel was moved out.
        /// Handler should close the corresponding Canvas/window.
        /// </summary>
        public event Action<string>? WindowEmptied;

        // ── Cross-window drag lifecycle ───────────────────────────────────────

        /// <summary>
        /// Called by the source window when a panel drag starts.
        /// <paramref name="removeFromSource"/> is invoked if the panel is dropped in another window.
        /// </summary>
        public void BeginCrossWindowDrag(string panelId, string sourceWindowId, Action removeFromSource)
        {
            _crossDragPanelId          = panelId;
            _crossDragSourceWindowId   = sourceWindowId;
            _crossDragRemoveFromSource = removeFromSource;
            DragStateChanged?.Invoke();
        }

        /// <summary>
        /// Called by the receiving window after successfully accepting a cross-window drop.
        /// Removes the panel from the source window and clears drag state.
        /// </summary>
        public void CompleteCrossWindowDrop()
        {
            _crossDragRemoveFromSource?.Invoke();
            ClearDragState();
        }

        /// <summary>Clears cross-window drag state without removing from source (same-window drop or cancel).</summary>
        public void CancelCrossWindowDrag()
        {
            ClearDragState();
        }

        private void ClearDragState()
        {
            _crossDragPanelId          = null;
            _crossDragSourceWindowId   = null;
            _crossDragRemoveFromSource = null;
            DragStateChanged?.Invoke();
        }

        // ── Window bounds registration ────────────────────────────────────────

        /// <summary>
        /// Register a window's screen-bounds getter. Called by DockWindowFactory when a canvas is created.
        /// The getter is called at drop-time to check whether a drag-end position falls within this window.
        /// </summary>
        public void RegisterWindowBounds(string windowId, Func<(int x, int y, int w, int h)> getBounds)
            => _windowBounds[windowId] = getBounds;

        /// <summary>Remove a window's bounds registration (called when the window closes).</summary>
        public void UnregisterWindowBounds(string windowId)
            => _windowBounds.Remove(windowId);

        /// <summary>
        /// Check if the given screen position falls within any registered window other than the source.
        /// If a match is found, fires <see cref="ExternalPanelArrived"/> and returns true.
        /// </summary>
        internal bool TryExternalDrop(string sourceWindowId, PanelNode panel, int screenX, int screenY)
        {
            Console.WriteLine($"[DockDbg] TryExternalDrop: panel={panel.PanelId} src={sourceWindowId} screen=({screenX},{screenY}) registeredWindows=[{string.Join(",", _windowBounds.Keys)}]");
            foreach (var (windowId, getBounds) in _windowBounds)
            {
                if (windowId == sourceWindowId) continue;
                var (wx, wy, ww, wh) = getBounds();
                bool hit = screenX >= wx && screenX < wx + ww && screenY >= wy && screenY < wy + wh;
                Console.WriteLine($"[DockDbg] TryExternalDrop: checking window={windowId} bounds=({wx},{wy},{ww},{wh}) hit={hit}");
                if (hit)
                {
                    ExternalPanelArrived?.Invoke(windowId, panel, screenX - wx, screenY - wy, ww, wh);
                    return true;
                }
            }
            Console.WriteLine($"[DockDbg] TryExternalDrop: no matching window found");
            return false;
        }

        // ── Internal callbacks from DockContext ───────────────────────────────

        internal void RequestNewWindow(string panelId, DockState initialState, int x = 100, int y = 100)
            => NewWindowRequested?.Invoke(panelId, initialState, x, y);

        internal void NotifyWindowEmptied(string windowId)
            => WindowEmptied?.Invoke(windowId);
    }
}
