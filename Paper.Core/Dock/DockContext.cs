using Paper.Core.Context;
using Paper.Core.Hooks;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Core.Dock
{
    // ── DockContext ───────────────────────────────────────────────────────────
    //
    // Shared via PaperContext so any panel/component in the tree can dispatch
    // dock actions without prop-drilling.
    //
    // Usage:
    //   var ctx = Hooks.UseContext(DockContext.Context);
    //   ctx.Dispatch(new DockMinimize { PanelId = "hierarchy" });
    //   ctx.IsDraggingPanel  — true while a panel header is being dragged
    //   ctx.DragOverNodeId   — NodeId of the node the drag is currently over
    //   ctx.DragOverZone     — which drop zone quadrant
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class DockContextValue
    {
        public required DockState            State            { get; init; }
        public required Action<DockAction>   Dispatch         { get; init; }

        // ── Drag tracking (set by DockPanel header drag handlers) ─────────────
        public bool     IsDraggingPanel  { get; init; } = false;
        public string?  DragPanelId      { get; init; } = null;   // PanelId being dragged
        public string?  DragNodeId       { get; init; } = null;   // FloatNode.NodeId if floating
        public string?  DragOverNodeId   { get; init; } = null;   // NodeId the ghost is over
        public DropZone DragOverZone     { get; init; } = DropZone.None;
        // Current pointer position (for floating ghost preview)
        public float    DragX            { get; init; } = 0;
        public float    DragY            { get; init; } = 0;

        // ── Called by DockPanel/DockContainer to start/end drag tracking ──────
        /// <summary>
        /// Invoke to begin drag: SetDragging(true, panelId, floatNodeId)
        /// Invoke to end drag:   SetDragging(false, null, null)
        /// </summary>
        public Action<bool, string?, string?>? SetDragging { get; init; }

        // ── Panel registry ────────────────────────────────────────────────────
        /// <summary>Maps PanelId → content factory function.</summary>
        public required IReadOnlyDictionary<string, Func<UINode>> PanelRegistry { get; init; }

        // ── Convenience ───────────────────────────────────────────────────────
        public Func<UINode>? GetPanel(string panelId) =>
            PanelRegistry.TryGetValue(panelId, out var f) ? f : null;

        public DockContextValue WithDrag(bool dragging, string? panelId = null, string? nodeId = null,
                                          string? overNodeId = null, DropZone zone = DropZone.None,
                                          float x = 0, float y = 0) =>
            new()
            {
                State          = State,
                Dispatch       = Dispatch,
                PanelRegistry  = PanelRegistry,
                SetDragging    = SetDragging,
                IsDraggingPanel = dragging,
                DragPanelId    = panelId,
                DragNodeId     = nodeId,
                DragOverNodeId = overNodeId,
                DragOverZone   = zone,
                DragX          = x,
                DragY          = y,
            };
    }

    public static class DockContext
    {
        /// <summary>The Paper context object. Pass to Hooks.UseContext() inside any dock component.</summary>
        public static readonly PaperContext<DockContextValue?> Context =
            PaperContext.Create<DockContextValue?>(null);

        /// <summary>
        /// Root component that owns the dock state + provides context.
        /// Wrap your entire editor root with this, or call <see cref="UI.DockRoot"/>.
        /// </summary>
        public static UINode Root(
            IReadOnlyDictionary<string, Func<UINode>> panelRegistry,
            DockState?   initialState = null,
            StyleSheet?  style        = null,
            string?      key          = null,
            params UINode[] children)
        {
            return UI.Component(props =>
            {
                var registry = props.Get<IReadOnlyDictionary<string, Func<UINode>>>("registry")!;
                var init     = props.Get<DockState>("initialState") ?? new DockState { Root = new PanelNode { PanelId = "empty" } };

                var (dockState, dispatch) = Paper.Core.Hooks.Hooks.UseReducer<DockState, DockAction>(DockReducer.Reduce, init);
                // Drag state lives here too so it participates in reconciliation
                var (dragCtx, setDragCtx, _) = Paper.Core.Hooks.Hooks.UseState<DockContextValue?>(null);

                var contextValue = dragCtx ?? new DockContextValue
                {
                    State         = dockState,
                    Dispatch      = dispatch,
                    PanelRegistry = registry,
                };
                // Ensure state is always current even if dragCtx carries stale version
                var live = new DockContextValue
                {
                    State          = dockState,
                    Dispatch       = dispatch,
                    PanelRegistry  = registry,
                    IsDraggingPanel = contextValue.IsDraggingPanel,
                    DragPanelId    = contextValue.DragPanelId,
                    DragNodeId     = contextValue.DragNodeId,
                    DragOverNodeId = contextValue.DragOverNodeId,
                    DragOverZone   = contextValue.DragOverZone,
                    DragX          = contextValue.DragX,
                    DragY          = contextValue.DragY,
                    SetDragging    = (dragging, panelId, nodeId) =>
                    {
                        if (dragging)
                            setDragCtx(new DockContextValue
                            {
                                State           = dockState,
                                Dispatch        = dispatch,
                                PanelRegistry   = registry,
                                IsDraggingPanel = true,
                                DragPanelId     = panelId,
                                DragNodeId      = nodeId,
                            });
                        else
                            setDragCtx(null);
                    },
                };

                var children2 = props.Children;
                var outerStyle = new StyleSheet
                {
                    Display       = Display.Flex,
                    FlexDirection = FlexDirection.Column,
                    Overflow      = Overflow.Hidden,
                    FlexGrow      = 1f,
                    Width         = Length.Percent(100),
                    Height        = Length.Percent(100),
                }.Merge(props.Style ?? StyleSheet.Empty);
                return UI.Box(outerStyle, Context.Provider(live, children2.ToArray()));
            }, new PropsBuilder()
                .Set("registry", panelRegistry)
                .Set("initialState", initialState)
                .Style(style ?? StyleSheet.Empty)
                .Children(children)
                .Build(), key);
        }
    }
}
