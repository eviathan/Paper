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
        public required DockState          State          { get; init; }
        public required Action<DockAction> Dispatch       { get; init; }
        public DockTheme                   Theme          { get; init; } = DockTheme.Dark;

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
        /// <summary>Maps PanelId → PanelRegistration (content factory + constraints).</summary>
        public required IReadOnlyDictionary<string, PanelRegistration> PanelRegistry { get; init; }

        // ── Convenience ───────────────────────────────────────────────────────
        public PanelRegistration? GetRegistration(string panelId) =>
            PanelRegistry.TryGetValue(panelId, out var reg) ? reg : null;

        public Func<UINode>? GetPanel(string panelId) =>
            GetRegistration(panelId)?.Factory;

        public DockPanelConstraints? GetConstraints(string panelId) =>
            GetRegistration(panelId)?.Constraints;

        // ── Hidden / auto-hide panel queries ─────────────────────────────────
        /// <summary>Panels that have been closed and can be re-shown.</summary>
        public IReadOnlyList<PanelNode> HiddenPanels => State.HiddenPanels;

        /// <summary>True if a panel with this id is currently closed (not in any layout slot).</summary>
        public bool IsPanelHidden(string panelId) =>
            State.HiddenPanels.Any(p => p.PanelId == panelId);

        /// <summary>Re-open a hidden panel at the given location.</summary>
        public void ShowPanel(string panelId, string? targetNodeId = null, DropZone zone = DropZone.Right) =>
            Dispatch(new DockShowPanel { PanelId = panelId, TargetNodeId = targetNodeId, Zone = zone });

        /// <summary>Send a panel to an auto-hide edge strip.</summary>
        public void SetAutoHide(string panelId, AutoHideEdge edge) =>
            Dispatch(new DockSetAutoHide { PanelId = panelId, Edge = edge });

        /// <summary>Restore a panel from auto-hide back into the tiled layout.</summary>
        public void RestoreFromAutoHide(string panelId) =>
            Dispatch(new DockSetAutoHide { PanelId = panelId, Edge = null });

        // ── Preset helpers ────────────────────────────────────────────────────
        /// <summary>Serializes the current layout to a named JSON preset string.</summary>
        public string SavePreset(string name) =>
            new DockLayoutPreset
            {
                Name   = name,
                Layout = State.Root.Serialize(),
                Floats = State.Floats.Select(f => ((DockNode)f).Serialize()).ToList(),
            }.Serialize();

        /// <summary>Creates a <see cref="DockLoadPreset"/> action from a saved preset string.</summary>
        public DockAction LoadPreset(string presetJson)
        {
            var preset = DockLayoutPreset.Deserialize(presetJson);
            var root   = DockNode.Deserialize(preset.Layout);
            var floats = preset.Floats
                .Select(json => (FloatNode)DockNode.Deserialize(json))
                .ToList();
            return new DockLoadPreset
            {
                State = new DockState { Root = root, Floats = floats },
            };
        }

        public DockContextValue WithDrag(bool dragging, string? panelId = null, string? nodeId = null,
                                          string? overNodeId = null, DropZone zone = DropZone.None,
                                          float x = 0, float y = 0) =>
            new()
            {
                State           = State,
                Dispatch        = Dispatch,
                Theme           = Theme,
                PanelRegistry   = PanelRegistry,
                SetDragging     = SetDragging,
                IsDraggingPanel = dragging,
                DragPanelId     = panelId,
                DragNodeId      = nodeId,
                DragOverNodeId  = overNodeId,
                DragOverZone    = zone,
                DragX           = x,
                DragY           = y,
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
        /// <param name="panels">
        /// Panel registrations — content factories, optional default docking positions, and constraints.
        /// When <paramref name="initialState"/> is null the initial layout is built automatically from
        /// each registration's <see cref="PanelRegistration.DefaultTargetPanelId"/> /
        /// <see cref="PanelRegistration.DefaultZone"/>.
        /// </param>
        /// <param name="initialState">
        /// Explicit starting layout. When null, auto-built from panel registrations.
        /// </param>
        public static UINode Root(
            IReadOnlyList<PanelRegistration> panels,
            DockState?   initialState = null,
            DockTheme?   theme        = null,
            StyleSheet?  style        = null,
            string?      key          = null,
            params UINode[] children)
        {
            var registry = panels.ToDictionary(p => p.PanelId);

            return UI.Component(props =>
            {
                var reg      = props.Get<IReadOnlyDictionary<string, PanelRegistration>>("registry")!;
                var init     = props.Get<DockState>("initialState")
                               ?? BuildInitialState(props.Get<IReadOnlyList<PanelRegistration>>("panels")!);
                var dockTheme = props.Get<DockTheme>("theme") ?? DockTheme.Dark;

                var (dockState, dispatch) = Paper.Core.Hooks.Hooks.UseReducer<DockState, DockAction>(DockReducer.Reduce, init);
                var (dragCtx, setDragCtx, _) = Paper.Core.Hooks.Hooks.UseState<DockContextValue?>(null);

                var contextValue = dragCtx ?? new DockContextValue
                {
                    State         = dockState,
                    Dispatch      = dispatch,
                    Theme         = dockTheme,
                    PanelRegistry = reg,
                };

                // Ensure State and Theme are always current even if dragCtx carries a stale snapshot
                var live = new DockContextValue
                {
                    State           = dockState,
                    Dispatch        = dispatch,
                    Theme           = dockTheme,
                    PanelRegistry   = reg,
                    IsDraggingPanel = contextValue.IsDraggingPanel,
                    DragPanelId     = contextValue.DragPanelId,
                    DragNodeId      = contextValue.DragNodeId,
                    DragOverNodeId  = contextValue.DragOverNodeId,
                    DragOverZone    = contextValue.DragOverZone,
                    DragX           = contextValue.DragX,
                    DragY           = contextValue.DragY,
                    SetDragging     = (dragging, panelId, nodeId) =>
                    {
                        if (dragging)
                            setDragCtx(new DockContextValue
                            {
                                State           = dockState,
                                Dispatch        = dispatch,
                                Theme           = dockTheme,
                                PanelRegistry   = reg,
                                IsDraggingPanel = true,
                                DragPanelId     = panelId,
                                DragNodeId      = nodeId,
                            });
                        else
                            setDragCtx(null);
                    },
                };

                var children2  = props.Children;
                var outerStyle = new StyleSheet
                {
                    Display       = Display.Flex,
                    FlexDirection = FlexDirection.Column,
                    Overflow      = Overflow.Hidden,
                    FlexGrow      = 1f,
                    Width         = Length.Percent(100),
                    Height        = Length.Percent(100),
                }.Merge(props.Style ?? StyleSheet.Empty);

                // When no children are passed, render the dock container automatically.
                // DockContainer.Render() reads from the Context.Provider(live, ...) below.
                var content = children2.Count == 0
                    ? DockContainer.Render()
                    : UI.Box(new StyleSheet { FlexGrow = 1f, Width = Length.Percent(100), Height = Length.Percent(100) }, children2.ToArray());

                return UI.Box(outerStyle, Context.Provider(live, content));
            }, new PropsBuilder()
                .Set("registry", (object)registry)
                .Set("panels",   (object)panels)
                .Set("initialState", (object?)initialState)
                .Set("theme",    (object?)(theme ?? DockTheme.Dark))
                .Style(style ?? StyleSheet.Empty)
                .Children(children)
                .Build(), key);
        }

        // ── Auto layout builder ───────────────────────────────────────────────

        /// <summary>
        /// Builds an initial <see cref="DockState"/> from panel registrations.
        /// The first panel becomes the root; subsequent panels are inserted next to
        /// their <see cref="PanelRegistration.DefaultTargetPanelId"/> (or the root if null).
        /// </summary>
        private static DockState BuildInitialState(IReadOnlyList<PanelRegistration> panels)
        {
            if (panels.Count == 0)
                return new DockState { Root = new PanelNode { PanelId = "empty" } };

            DockNode root = new PanelNode { PanelId = panels[0].PanelId, Title = panels[0].Title };

            for (int i = 1; i < panels.Count; i++)
            {
                var reg         = panels[i];
                var panel       = new PanelNode { PanelId = reg.PanelId, Title = reg.Title };
                var targetId    = reg.DefaultTargetPanelId != null
                    ? FindNodeIdByPanelId(root, reg.DefaultTargetPanelId)
                    : root.NodeId;
                root = DockReducer.InsertPanelPublic(root, targetId ?? root.NodeId, reg.DefaultZone, panel);
            }

            return new DockState { Root = root };
        }

        private static string? FindNodeIdByPanelId(DockNode node, string panelId) => node switch
        {
            PanelNode p    when p.PanelId == panelId => p.NodeId,
            TabGroupNode tg => tg.Tabs.FirstOrDefault(t => t.PanelId == panelId)?.NodeId,
            SplitNode s     => FindNodeIdByPanelId(s.First, panelId) ?? FindNodeIdByPanelId(s.Second, panelId),
            _               => null,
        };
    }
}
