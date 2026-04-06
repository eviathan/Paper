using Paper.Core.VirtualDom;

namespace Paper.Core.Dock
{
    // ── Panel registration ────────────────────────────────────────────────────

    /// <summary>
    /// Describes a panel that can be hosted in the dock system.
    /// Pass a list of these to <see cref="DockContext.Root"/> to register panels
    /// and optionally declare their default docking position.
    /// </summary>
    public sealed record PanelRegistration(
        string                PanelId,
        string                Title,
        Func<UINode>          Factory,
        /// <summary>Dock adjacent to this PanelId by default (null = use root).</summary>
        string?               DefaultTargetPanelId = null,
        DropZone              DefaultZone          = DropZone.Right,
        DockPanelConstraints? Constraints          = null);

    /// <summary>
    /// Restricts what the user can do with a specific panel.
    /// null fields mean "no restriction".
    /// </summary>
    public sealed record DockPanelConstraints(
        bool AllowClose    = true,
        bool AllowFloat    = true,
        bool AllowMinimize = true,
        bool AllowMaximize = true,
        /// <summary>Minimum panel width in px (0 = no minimum).</summary>
        float MinWidth  = 0,
        /// <summary>Minimum panel height in px (0 = no minimum).</summary>
        float MinHeight = 0,
        /// <summary>Which drop zones are offered when another panel is dragged onto this one.
        /// null = all zones allowed.</summary>
        IReadOnlySet<DropZone>? AllowedDropZones = null);

    // ── DockState ─────────────────────────────────────────────────────────────
    //
    // Holds the full docking layout as an immutable snapshot.
    // All mutations go through DockReducer which returns a new DockState.
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class DockState
    {
        /// <summary>The root of the tiled layout tree.</summary>
        public DockNode Root { get; init; } = new PanelNode { PanelId = "empty" };

        /// <summary>Floating (torn-off) panels, rendered as portal overlays on top of everything.</summary>
        public IReadOnlyList<FloatNode> Floats { get; init; } = Array.Empty<FloatNode>();

        /// <summary>Panels that have been closed — preserved so they can be re-shown.</summary>
        public IReadOnlyList<PanelNode> HiddenPanels { get; init; } = Array.Empty<PanelNode>();

        /// <summary>Panels in auto-hide state (collapsed to an edge strip).</summary>
        public IReadOnlyList<AutoHideEntry> AutoHidePanels { get; init; } = Array.Empty<AutoHideEntry>();

        /// <summary>The panelId that is currently full-screened (null = none).</summary>
        public string? MaximizedPanelId { get; init; } = null;

        public DockState With(
            DockNode?                    root            = null,
            IReadOnlyList<FloatNode>?    floats          = null,
            IReadOnlyList<PanelNode>?    hiddenPanels    = null,
            IReadOnlyList<AutoHideEntry>? autoHidePanels = null,
            string?                      maximizedPanelId = null,
            bool                         clearMaximized   = false) =>
            new()
            {
                Root             = root             ?? Root,
                Floats           = floats            ?? Floats,
                HiddenPanels     = hiddenPanels      ?? HiddenPanels,
                AutoHidePanels   = autoHidePanels    ?? AutoHidePanels,
                MaximizedPanelId = clearMaximized ? null : (maximizedPanelId ?? MaximizedPanelId),
            };
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    public abstract class DockAction { }

    /// <summary>Drag a panel header and drop it onto another panel's drop zone.</summary>
    public sealed class DockDrop : DockAction
    {
        public required string   SourcePanelId { get; init; }
        public required string   TargetNodeId  { get; init; }
        public required DropZone Zone          { get; init; }
    }

    /// <summary>Drop a panel onto a screen-edge zone (wraps the entire root in a new split).</summary>
    public sealed class DockDropOuter : DockAction
    {
        public required string   SourcePanelId { get; init; }
        public required DropZone Zone          { get; init; }
    }

    /// <summary>Panel header dragged outside the window — becomes a FloatNode.</summary>
    public sealed class DockTearOff : DockAction
    {
        public required string SourcePanelId { get; init; }
        public float X { get; init; }
        public float Y { get; init; }
    }

    /// <summary>Close a panel — removes it from the layout and adds it to HiddenPanels.</summary>
    public sealed class DockClosePanel : DockAction
    {
        public required string PanelId { get; init; }
    }

    /// <summary>Re-show a previously closed panel.</summary>
    public sealed class DockShowPanel : DockAction
    {
        public required string PanelId     { get; init; }
        /// <summary>NodeId to dock next to. null = dock at root.</summary>
        public string?         TargetNodeId { get; init; } = null;
        public DropZone        Zone         { get; init; } = DropZone.Right;
    }

    /// <summary>Move a panel to an auto-hide edge strip, or restore it to the tiled layout.</summary>
    public sealed class DockSetAutoHide : DockAction
    {
        public required string PanelId  { get; init; }
        /// <summary>null = restore to tiled layout.</summary>
        public AutoHideEdge?   Edge     { get; init; } = null;
    }

    /// <summary>Dock a floating panel back into the tiled layout.</summary>
    public sealed class DockDockFloat : DockAction
    {
        public required string   FloatNodeId  { get; init; }
        public required string   TargetNodeId { get; init; }
        public required DropZone Zone         { get; init; }
    }

    /// <summary>Dock a floating panel to a screen-edge zone.</summary>
    public sealed class DockDockFloatOuter : DockAction
    {
        public required string   FloatNodeId { get; init; }
        public required DropZone Zone        { get; init; }
    }

    /// <summary>Move a floating panel (drag its title bar).</summary>
    public sealed class DockMoveFloat : DockAction
    {
        public required string FloatNodeId { get; init; }
        public float X { get; init; }
        public float Y { get; init; }
    }

    /// <summary>Resize a floating panel.</summary>
    public sealed class DockResizeFloat : DockAction
    {
        public required string FloatNodeId { get; init; }
        public float Width  { get; init; }
        public float Height { get; init; }
    }

    /// <summary>Drag the splitter between two children of a SplitNode.</summary>
    public sealed class DockResizeSplit : DockAction
    {
        public required string SplitNodeId { get; init; }
        /// <summary>New ratio [0.05..0.95] for the first child.</summary>
        public float Ratio { get; init; }
    }

    /// <summary>Switch the active tab in a TabGroupNode.</summary>
    public sealed class DockSelectTab : DockAction
    {
        public required string TabGroupNodeId { get; init; }
        public int Index { get; init; }
    }

    /// <summary>Toggle minimized state of a panel.</summary>
    public sealed class DockMinimize : DockAction
    {
        public required string PanelId   { get; init; }
        public bool            Minimized { get; init; }
    }

    /// <summary>Maximize a single panel (fills the entire dock area).</summary>
    public sealed class DockMaximize : DockAction
    {
        /// <summary>Set to null to restore.</summary>
        public string? PanelId { get; init; }
    }

    /// <summary>Replace the entire layout (e.g. loading a preset).</summary>
    public sealed class DockLoadPreset : DockAction
    {
        public required DockState State { get; init; }
    }

    // ── Reducer ───────────────────────────────────────────────────────────────

    public static class DockReducer
    {
        public static DockState Reduce(DockState state, DockAction action) => action switch
        {
            DockDrop           a => HandleDrop(state, a),
            DockDropOuter      a => HandleDropOuter(state, a),
            DockTearOff        a => HandleTearOff(state, a),
            DockClosePanel     a => HandleClosePanel(state, a),
            DockShowPanel      a => HandleShowPanel(state, a),
            DockSetAutoHide    a => HandleSetAutoHide(state, a),
            DockDockFloat      a => HandleDockFloat(state, a),
            DockDockFloatOuter a => HandleDockFloatOuter(state, a),
            DockMoveFloat      a => HandleMoveFloat(state, a),
            DockResizeFloat    a => HandleResizeFloat(state, a),
            DockResizeSplit    a => HandleResizeSplit(state, a),
            DockSelectTab      a => HandleSelectTab(state, a),
            DockMinimize       a => HandleMinimize(state, a),
            DockMaximize       a => HandleMaximize(state, a),
            DockLoadPreset     a => a.State,
            _                  => state,
        };

        // ── Drop ──────────────────────────────────────────────────────────────

        private static DockState HandleDrop(DockState state, DockDrop a)
        {
            var (sourcePanel, treeWithout) = ExtractPanel(state.Root, a.SourcePanelId);
            if (sourcePanel == null) return state;
            var newRoot = InsertPanelSmart(treeWithout, a.TargetNodeId, a.Zone, sourcePanel);
            return state.With(root: newRoot, clearMaximized: true);
        }

        // ── DropOuter ─────────────────────────────────────────────────────────

        private static DockState HandleDropOuter(DockState state, DockDropOuter a)
        {
            var (panel, treeWithout) = ExtractPanel(state.Root, a.SourcePanelId);
            if (panel == null) return state;

            bool isFirst  = a.Zone == DropZone.Left || a.Zone == DropZone.Top;
            var direction = (a.Zone == DropZone.Left || a.Zone == DropZone.Right)
                ? DockDirection.Horizontal : DockDirection.Vertical;
            var tree = treeWithout ?? new PanelNode { PanelId = "empty" };
            var newRoot = new SplitNode
            {
                Direction = direction,
                Ratio     = isFirst ? 0.25f : 0.75f,
                First     = isFirst ? (DockNode)panel : tree,
                Second    = isFirst ? tree : panel,
            };
            return state.With(root: newRoot, clearMaximized: true);
        }

        // ── TearOff ───────────────────────────────────────────────────────────

        private static DockState HandleTearOff(DockState state, DockTearOff a)
        {
            var (sourcePanel, treeWithout) = ExtractPanel(state.Root, a.SourcePanelId);
            if (sourcePanel == null) return state;

            var floatNode = new FloatNode
            {
                Panel  = sourcePanel,
                X      = a.X,
                Y      = a.Y,
                Width  = 400,
                Height = 300,
            };
            var newFloats = state.Floats.Append(floatNode).ToList();
            return state.With(root: treeWithout, floats: newFloats);
        }

        // ── ClosePanel ────────────────────────────────────────────────────────

        private static DockState HandleClosePanel(DockState state, DockClosePanel a)
        {
            // Try tiled layout first
            var (panel, treeWithout) = ExtractPanel(state.Root, a.PanelId);
            if (panel != null)
            {
                var hidden = new List<PanelNode>(state.HiddenPanels) { panel };
                return state.With(
                    root: treeWithout,
                    hiddenPanels: hidden,
                    clearMaximized: state.MaximizedPanelId == a.PanelId);
            }

            // Try floats
            var floatNode = state.Floats.FirstOrDefault(f => f.Panel.PanelId == a.PanelId);
            if (floatNode != null)
            {
                var newFloats = state.Floats.Where(f => f.Panel.PanelId != a.PanelId).ToList();
                var hidden    = new List<PanelNode>(state.HiddenPanels) { floatNode.Panel };
                return state.With(floats: newFloats, hiddenPanels: hidden);
            }

            // Try auto-hide
            var autoEntry = state.AutoHidePanels.FirstOrDefault(e => e.Panel.PanelId == a.PanelId);
            if (autoEntry != null)
            {
                var newAuto  = state.AutoHidePanels.Where(e => e.Panel.PanelId != a.PanelId).ToList();
                var hidden   = new List<PanelNode>(state.HiddenPanels) { autoEntry.Panel };
                return state.With(autoHidePanels: newAuto, hiddenPanels: hidden);
            }

            return state;
        }

        // ── ShowPanel ────────────────────────────────────────────────────────

        private static DockState HandleShowPanel(DockState state, DockShowPanel a)
        {
            var panel = state.HiddenPanels.FirstOrDefault(p => p.PanelId == a.PanelId);
            if (panel == null) return state;

            var newHidden  = state.HiddenPanels.Where(p => p.PanelId != a.PanelId).ToList();
            var targetId   = a.TargetNodeId ?? state.Root.NodeId;
            var newRoot    = InsertPanelSmart(state.Root, targetId, a.Zone, panel);
            return state.With(root: newRoot, hiddenPanels: newHidden);
        }

        // ── SetAutoHide ───────────────────────────────────────────────────────

        private static DockState HandleSetAutoHide(DockState state, DockSetAutoHide a)
        {
            if (a.Edge == null)
            {
                // Restore from auto-hide back into tiled layout
                var entry = state.AutoHidePanels.FirstOrDefault(e => e.Panel.PanelId == a.PanelId);
                if (entry == null) return state;
                var newAuto = state.AutoHidePanels.Where(e => e.Panel.PanelId != a.PanelId).ToList();
                var newRoot = InsertPanelSmart(state.Root, state.Root.NodeId, DropZone.Right, entry.Panel);
                return state.With(root: newRoot, autoHidePanels: newAuto);
            }
            else
            {
                // Move from tiled layout to auto-hide strip
                var (panel, treeWithout) = ExtractPanel(state.Root, a.PanelId);
                if (panel == null)
                {
                    // Try floats
                    var fn = state.Floats.FirstOrDefault(f => f.Panel.PanelId == a.PanelId);
                    if (fn == null) return state;
                    var newFloats2 = state.Floats.Where(f => f.Panel.PanelId != a.PanelId).ToList();
                    var entry2 = new AutoHideEntry { Panel = fn.Panel, Edge = a.Edge.Value };
                    var newAuto2 = new List<AutoHideEntry>(state.AutoHidePanels) { entry2 };
                    return state.With(floats: newFloats2, autoHidePanels: newAuto2);
                }
                var autoEntry = new AutoHideEntry { Panel = panel, Edge = a.Edge.Value };
                var newAutoHide = new List<AutoHideEntry>(state.AutoHidePanels) { autoEntry };
                return state.With(root: treeWithout, autoHidePanels: newAutoHide);
            }
        }

        // ── DockFloat ─────────────────────────────────────────────────────────

        private static DockState HandleDockFloat(DockState state, DockDockFloat a)
        {
            var floatNode = state.Floats.FirstOrDefault(f => f.NodeId == a.FloatNodeId);
            if (floatNode == null) return state;
            var newFloats = state.Floats.Where(f => f.NodeId != a.FloatNodeId).ToList();
            var newRoot   = InsertPanelSmart(state.Root, a.TargetNodeId, a.Zone, floatNode.Panel);
            return state.With(root: newRoot, floats: newFloats);
        }

        // ── DockFloatOuter ────────────────────────────────────────────────────

        private static DockState HandleDockFloatOuter(DockState state, DockDockFloatOuter a)
        {
            var floatNode = state.Floats.FirstOrDefault(f => f.NodeId == a.FloatNodeId);
            if (floatNode == null) return state;
            var newFloats = state.Floats.Where(f => f.NodeId != a.FloatNodeId).ToList();

            bool isFirst  = a.Zone == DropZone.Left || a.Zone == DropZone.Top;
            var direction = (a.Zone == DropZone.Left || a.Zone == DropZone.Right)
                ? DockDirection.Horizontal : DockDirection.Vertical;
            var newRoot   = new SplitNode
            {
                Direction = direction,
                Ratio     = isFirst ? 0.25f : 0.75f,
                First     = isFirst ? (DockNode)floatNode.Panel : state.Root,
                Second    = isFirst ? state.Root : floatNode.Panel,
            };
            return state.With(root: newRoot, floats: newFloats, clearMaximized: true);
        }

        // ── MoveFloat / ResizeFloat ───────────────────────────────────────────

        private static DockState HandleMoveFloat(DockState state, DockMoveFloat a)
        {
            var newFloats = state.Floats.Select(f =>
                f.NodeId == a.FloatNodeId
                    ? new FloatNode { NodeId = f.NodeId, Panel = f.Panel, X = a.X, Y = a.Y, Width = f.Width, Height = f.Height }
                    : f).ToList();
            return state.With(floats: newFloats);
        }

        private static DockState HandleResizeFloat(DockState state, DockResizeFloat a)
        {
            var newFloats = state.Floats.Select(f =>
                f.NodeId == a.FloatNodeId
                    ? new FloatNode { NodeId = f.NodeId, Panel = f.Panel, X = f.X, Y = f.Y, Width = a.Width, Height = a.Height }
                    : f).ToList();
            return state.With(floats: newFloats);
        }

        // ── ResizeSplit ───────────────────────────────────────────────────────

        private static DockState HandleResizeSplit(DockState state, DockResizeSplit a)
        {
            var newRoot = MapNode(state.Root, node =>
            {
                if (node is SplitNode s && s.NodeId == a.SplitNodeId)
                    return new SplitNode
                    {
                        NodeId    = s.NodeId,
                        Direction = s.Direction,
                        Ratio     = Math.Clamp(a.Ratio, 0.05f, 0.95f),
                        First     = s.First,
                        Second    = s.Second,
                    };
                return node;
            });
            return state.With(root: newRoot);
        }

        // ── SelectTab ────────────────────────────────────────────────────────

        private static DockState HandleSelectTab(DockState state, DockSelectTab a)
        {
            var newRoot = MapNode(state.Root, node =>
            {
                if (node is TabGroupNode tg && tg.NodeId == a.TabGroupNodeId)
                    return new TabGroupNode
                    {
                        NodeId      = tg.NodeId,
                        Tabs        = tg.Tabs,
                        ActiveIndex = Math.Clamp(a.Index, 0, tg.Tabs.Count - 1),
                    };
                return node;
            });
            return state.With(root: newRoot);
        }

        // ── Minimize / Maximize ───────────────────────────────────────────────

        private static DockState HandleMinimize(DockState state, DockMinimize a)
        {
            var newRoot = MapNode(state.Root, node =>
            {
                if (node is PanelNode p && p.PanelId == a.PanelId)
                    return new PanelNode { NodeId = p.NodeId, PanelId = p.PanelId, Title = p.Title, Minimized = a.Minimized, Maximized = p.Maximized };
                if (node is TabGroupNode tg)
                {
                    bool any  = false;
                    var tabs  = tg.Tabs.Select(t =>
                    {
                        if (t.PanelId == a.PanelId) { any = true; return new PanelNode { NodeId = t.NodeId, PanelId = t.PanelId, Title = t.Title, Minimized = a.Minimized, Maximized = t.Maximized }; }
                        return t;
                    }).ToList();
                    return any ? new TabGroupNode { NodeId = tg.NodeId, Tabs = tabs, ActiveIndex = tg.ActiveIndex } : node;
                }
                return node;
            });
            return state.With(root: newRoot);
        }

        private static DockState HandleMaximize(DockState state, DockMaximize a) =>
            state.With(maximizedPanelId: a.PanelId, clearMaximized: a.PanelId == null);

        // ── Tree helpers ──────────────────────────────────────────────────────

        /// <summary>Extract a PanelNode by PanelId from the tree, returning it and the pruned tree.</summary>
        private static (PanelNode? panel, DockNode? tree) ExtractPanel(DockNode node, string panelId)
        {
            switch (node)
            {
                case PanelNode p when p.PanelId == panelId:
                    return (p, null);

                case TabGroupNode tg:
                {
                    var idx = tg.Tabs.FindIndex(t => t.PanelId == panelId);
                    if (idx < 0) return (null, node);
                    var found     = tg.Tabs[idx];
                    var remaining = tg.Tabs.Where((_, i) => i != idx).ToList();
                    if (remaining.Count == 0) return (found, null);
                    if (remaining.Count == 1) return (found, remaining[0]);
                    return (found, new TabGroupNode { NodeId = tg.NodeId, Tabs = remaining, ActiveIndex = Math.Clamp(tg.ActiveIndex, 0, remaining.Count - 1) });
                }

                case SplitNode s:
                {
                    var (fp, ft) = ExtractPanel(s.First, panelId);
                    if (fp != null)
                        return (fp, ft == null ? s.Second : new SplitNode { NodeId = s.NodeId, Direction = s.Direction, Ratio = s.Ratio, First = ft, Second = s.Second });
                    var (sp, st) = ExtractPanel(s.Second, panelId);
                    if (sp != null)
                        return (sp, st == null ? s.First : new SplitNode { NodeId = s.NodeId, Direction = s.Direction, Ratio = s.Ratio, First = s.First, Second = st });
                    return (null, node);
                }

                case FloatNode f when f.Panel.PanelId == panelId:
                    return (f.Panel, null);

                default:
                    return (null, node);
            }
        }

        /// <summary>
        /// Insert a panel at the given target / zone.
        /// If the current root is the empty placeholder, it is replaced entirely.
        /// </summary>
        private static DockNode InsertPanelSmart(DockNode? root, string targetNodeId, DropZone zone, PanelNode panel)
        {
            if (root == null || (root is PanelNode ep && ep.PanelId == "empty"))
                return panel;
            return InsertPanel(root, targetNodeId, zone, panel);
        }

        /// <summary>Public wrapper used by DockContext when building the initial layout.</summary>
        internal static DockNode InsertPanelPublic(DockNode root, string targetNodeId, DropZone zone, PanelNode panel) =>
            InsertPanelSmart(root, targetNodeId, zone, panel);

        /// <summary>Insert a PanelNode at the drop zone of the node with the given NodeId.</summary>
        private static DockNode InsertPanel(DockNode root, string targetNodeId, DropZone zone, PanelNode panel)
        {
            return MapNode(root, node =>
            {
                if (node.NodeId != targetNodeId) return node;

                if (zone == DropZone.Center)
                {
                    if (node is TabGroupNode tg)
                    {
                        var tabs = new List<PanelNode>(tg.Tabs) { panel };
                        return new TabGroupNode { NodeId = tg.NodeId, Tabs = tabs, ActiveIndex = tabs.Count - 1 };
                    }
                    if (node is PanelNode existing)
                        return new TabGroupNode { Tabs = new List<PanelNode> { existing, panel }, ActiveIndex = 1 };
                    return node;
                }

                bool isFirst  = zone == DropZone.Left || zone == DropZone.Top;
                var direction = (zone == DropZone.Left || zone == DropZone.Right)
                    ? DockDirection.Horizontal : DockDirection.Vertical;
                return new SplitNode
                {
                    Direction = direction,
                    Ratio     = isFirst ? 0.35f : 0.65f,
                    First     = isFirst ? panel : node,
                    Second    = isFirst ? node  : panel,
                };
            });
        }

        /// <summary>Post-order map — replaces every node with the result of <paramref name="map"/>.</summary>
        private static DockNode MapNode(DockNode node, Func<DockNode, DockNode> map)
        {
            DockNode inner = node switch
            {
                SplitNode s => new SplitNode
                {
                    NodeId    = s.NodeId,
                    Direction = s.Direction,
                    Ratio     = s.Ratio,
                    First     = MapNode(s.First, map),
                    Second    = MapNode(s.Second, map),
                },
                TabGroupNode tg => new TabGroupNode
                {
                    NodeId      = tg.NodeId,
                    Tabs        = tg.Tabs,
                    ActiveIndex = tg.ActiveIndex,
                },
                _ => node,
            };
            return map(inner);
        }
    }
}
