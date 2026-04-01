namespace Paper.Core.Dock
{
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

        /// <summary>The panelId that is currently full-screened (null = none).</summary>
        public string? MaximizedPanelId { get; init; } = null;

        public DockState With(
            DockNode?              root              = null,
            IReadOnlyList<FloatNode>? floats         = null,
            string?                maximizedPanelId  = null,
            bool                   clearMaximized    = false) =>
            new()
            {
                Root             = root             ?? Root,
                Floats           = floats            ?? Floats,
                MaximizedPanelId = clearMaximized ? null : (maximizedPanelId ?? MaximizedPanelId),
            };
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    public abstract class DockAction { }

    /// <summary>Drag a panel header and drop it onto another panel's drop zone.</summary>
    public sealed class DockDrop : DockAction
    {
        /// <summary>PanelId of the panel being dragged.</summary>
        public required string SourcePanelId  { get; init; }
        /// <summary>NodeId of the DockNode the panel was dropped onto.</summary>
        public required string TargetNodeId   { get; init; }
        public required DropZone Zone         { get; init; }
    }

    /// <summary>Panel header dragged outside the window — becomes a FloatNode.</summary>
    public sealed class DockTearOff : DockAction
    {
        public required string SourcePanelId { get; init; }
        public float X { get; init; }
        public float Y { get; init; }
    }

    /// <summary>Dock a floating panel back into the layout.</summary>
    public sealed class DockDockFloat : DockAction
    {
        public required string FloatNodeId   { get; init; }
        public required string TargetNodeId  { get; init; }
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
        public required string PanelId { get; init; }
        public bool Minimized { get; init; }
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
            DockDrop       a => HandleDrop(state, a),
            DockTearOff    a => HandleTearOff(state, a),
            DockDockFloat  a => HandleDockFloat(state, a),
            DockMoveFloat  a => HandleMoveFloat(state, a),
            DockResizeFloat a => HandleResizeFloat(state, a),
            DockResizeSplit a => HandleResizeSplit(state, a),
            DockSelectTab  a => HandleSelectTab(state, a),
            DockMinimize   a => HandleMinimize(state, a),
            DockMaximize   a => HandleMaximize(state, a),
            DockLoadPreset a => a.State,
            _              => state,
        };

        // ── Drop ──────────────────────────────────────────────────────────────

        private static DockState HandleDrop(DockState state, DockDrop a)
        {
            // Find & extract source panel from the tree
            var (sourcePanel, treeWithout) = ExtractPanel(state.Root, a.SourcePanelId);
            if (sourcePanel == null) return state;

            // Insert at target location
            var newRoot = InsertPanel(treeWithout ?? new PanelNode { PanelId = "empty" },
                                      a.TargetNodeId, a.Zone, sourcePanel);
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
            return state.With(root: treeWithout ?? new PanelNode { PanelId = "empty" }, floats: newFloats);
        }

        // ── DockFloat ─────────────────────────────────────────────────────────

        private static DockState HandleDockFloat(DockState state, DockDockFloat a)
        {
            var floatNode = state.Floats.FirstOrDefault(f => f.NodeId == a.FloatNodeId);
            if (floatNode == null) return state;

            var newFloats = state.Floats.Where(f => f.NodeId != a.FloatNodeId).ToList();
            var newRoot   = InsertPanel(state.Root, a.TargetNodeId, a.Zone, floatNode.Panel);
            return state.With(root: newRoot, floats: newFloats);
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
                    bool any = false;
                    var tabs = tg.Tabs.Select(t =>
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
                    return (p, null);  // null = remove this node entirely

                case TabGroupNode tg:
                {
                    var idx = tg.Tabs.FindIndex(t => t.PanelId == panelId);
                    if (idx < 0) return (null, node);
                    var found = tg.Tabs[idx];
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

        /// <summary>Insert a PanelNode at the drop zone of the node with the given NodeId.</summary>
        private static DockNode InsertPanel(DockNode root, string targetNodeId, DropZone zone, PanelNode panel)
        {
            return MapNode(root, node =>
            {
                if (node.NodeId != targetNodeId) return node;

                if (zone == DropZone.Center)
                {
                    // Merge into (or create) a tab group
                    if (node is TabGroupNode tg)
                    {
                        var tabs = new List<PanelNode>(tg.Tabs) { panel };
                        return new TabGroupNode { NodeId = tg.NodeId, Tabs = tabs, ActiveIndex = tabs.Count - 1 };
                    }
                    if (node is PanelNode existing)
                        return new TabGroupNode { Tabs = new List<PanelNode> { existing, panel }, ActiveIndex = 1 };
                    return node;
                }

                // Split
                bool isFirst = zone == DropZone.Left || zone == DropZone.Top;
                var direction = (zone == DropZone.Left || zone == DropZone.Right)
                    ? DockDirection.Horizontal
                    : DockDirection.Vertical;
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
                    Tabs        = tg.Tabs,   // tabs are leaves, no children to map
                    ActiveIndex = tg.ActiveIndex,
                },
                _ => node,
            };
            return map(inner);
        }
    }
}
