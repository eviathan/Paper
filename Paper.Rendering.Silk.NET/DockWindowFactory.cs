using Paper.Core.Dock;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Silk.NET.Maths;

namespace Paper.Rendering.Silk.NET
{
    // ── DockWindowFactory ─────────────────────────────────────────────────────
    //
    // Convenience helper that wires DockWindowSession to CanvasManager so ejected
    // panels automatically spawn real OS windows, and dropped panels dock into
    // existing windows when the cursor lands on one.
    //
    // Usage:
    //
    //   var session = new DockWindowSession();
    //   var panels  = new[] { ... };
    //
    //   var main = new Canvas("Editor", 1280, 720) { DisableVSync = true };
    //   main.DockSession = session;
    //   main.Mount(() => DockContext.Root(panels, session: session, windowId: "main"));
    //
    //   DockWindowFactory.Wire(session, panels, primaryCanvas: main, primaryWindowId: "main");
    //   CanvasManager.Run(main);
    //
    // ─────────────────────────────────────────────────────────────────────────

    public static class DockWindowFactory
    {
        /// <summary>
        /// Wire the session to CanvasManager so ejected panels create new OS windows,
        /// panels dropped onto existing windows dock into them, and empty windows close.
        /// </summary>
        /// <param name="session">The shared dock session.</param>
        /// <param name="panels">Panel registrations — same list passed to all DockContext.Root calls.</param>
        /// <param name="primaryCanvas">The main Canvas, if any, to register for drop-target detection.</param>
        /// <param name="primaryWindowId">The windowId used by the primary canvas (default "main").</param>
        /// <param name="windowWidth">Initial width of spawned windows (default 600).</param>
        /// <param name="windowHeight">Initial height of spawned windows (default 400).</param>
        /// <param name="windowTitle">Title for spawned windows (default "Panel").</param>
        /// <param name="configureCanvas">Optional callback to further configure a new Canvas before adding it.</param>
        public static void Wire(
            DockWindowSession                session,
            IReadOnlyList<PanelRegistration> panels,
            Canvas?                          primaryCanvas    = null,
            string                           primaryWindowId  = "main",
            int                              windowWidth      = 600,
            int                              windowHeight     = 400,
            string                           windowTitle      = "Panel",
            Action<Canvas>?                  configureCanvas  = null)
        {
            var windowsById = new Dictionary<string, Canvas>();

            // Register the primary window so it can receive cross-window drops.
            if (primaryCanvas != null)
            {
                windowsById[primaryWindowId] = primaryCanvas;
                var captured = primaryCanvas;
                session.RegisterWindowBounds(primaryWindowId, () =>
                {
                    var pos = captured.ScreenPosition;
                    var sz  = captured.ScreenSize;
                    return (pos.X, pos.Y, sz.X, sz.Y);
                });
            }

            session.NewWindowRequested += (panelId, initialState, x, y) =>
            {
                Console.WriteLine($"[DockDbg] NewWindowRequested fired: panelId={panelId} pos=({x},{y})");
                var windowId = Guid.NewGuid().ToString("N")[..8];
                var canvas   = new Canvas(windowTitle, windowWidth, windowHeight)
                {
                    DisableVSync    = true,
                    InitialPosition = new Vector2D<int>(x, y),
                    DockSession     = session,
                };
                configureCanvas?.Invoke(canvas);
                canvas.Mount(_ => DockContext.Root(panels, initialState, session: session, windowId: windowId));

                // Register bounds so this new window can receive cross-window drops.
                var capturedCanvas = canvas;
                session.RegisterWindowBounds(windowId, () =>
                {
                    var pos = capturedCanvas.ScreenPosition;
                    var sz  = capturedCanvas.ScreenSize;
                    return (pos.X, pos.Y, sz.X, sz.Y);
                });

                windowsById[windowId] = canvas;
                CanvasManager.Add(canvas);
            };

            // Close secondary windows when they become empty (last panel moved out).
            session.WindowEmptied += windowId =>
            {
                Console.WriteLine($"[DockDbg] WindowEmptied: windowId={windowId} isPrimary={windowId == primaryWindowId}");
                // Never close the primary window, and keep it registered so it can
                // receive future cross-window drops (e.g. panel dragged back after a round-trip).
                if (windowId == primaryWindowId) return;

                if (!windowsById.TryGetValue(windowId, out var canvas)) return;
                canvas.Shutdown();
                windowsById.Remove(windowId);
                session.UnregisterWindowBounds(windowId);
            };
        }
    }
}
