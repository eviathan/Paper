using Paper.Core.Events;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using H = Paper.Core.Hooks.Hooks;

namespace Paper.Core.Dock
{
    // ── DockContainer ─────────────────────────────────────────────────────────
    //
    // Renders the DockNode tree recursively.
    // SplitNode  → two children + a draggable resize handle
    // TabGroup   → tab bar + active panel
    // PanelNode  → DockPanel (header + content)
    //
    // Drop zones are rendered as a transparent overlay while a panel is dragged.
    // Floating panels are rendered via Portal on top of everything.
    // ─────────────────────────────────────────────────────────────────────────

    public static class DockContainer
    {
        // ── Theme ─────────────────────────────────────────────────────────────

        private static readonly PaperColour ColBg         = new("#1a1a2e");
        private static readonly PaperColour ColHeader     = new("#252545");
        private static readonly PaperColour ColHeaderHov  = new("#2d2d58");
        private static readonly PaperColour ColBorder     = new("#3a3a5a");
        private static readonly PaperColour ColText       = new("#c8c8e0");
        private static readonly PaperColour ColTextDim    = new("#7878a0");
        private static readonly PaperColour ColHandle     = new("#303050");
        private static readonly PaperColour ColHandleHov  = new("#5060b0");
        private static readonly PaperColour ColTabActive  = new("#3a3a70");
        private static readonly PaperColour ColTabHov     = new("#2a2a55");
        private static readonly PaperColour ColDropZone   = new(0.3f, 0.5f, 1f, 0.22f);
        private static readonly PaperColour ColDropCenter = new(0.3f, 0.8f, 0.5f, 0.22f);
        private static readonly PaperColour ColDropBorder = new(0.4f, 0.6f, 1f, 0.7f);
        private static readonly PaperColour ColFloat      = new("#1e1e38");
        private static readonly PaperColour ColFloatBorder= new("#4a4a80");
        private static readonly PaperColour ColMinStrip   = new("#141428");

        private const float HandlePx   = 5f;
        private const float HeaderPx   = 30f;
        private const float TabBarPx   = 28f;
        private const float MinStripPx = 24f;

        // ── Public entry point ────────────────────────────────────────────────

        /// <summary>
        /// Renders the full dock layout.
        /// <paramref name="style"/> is applied to the outermost container.
        /// </summary>
        public static UINode Render(StyleSheet? style = null) =>
            UI.Component(RootComponent, new PropsBuilder().Style(style ?? StyleSheet.Empty).Build());
        
        /// <summary>
        /// Renders the dock tree directly with provided state and context.
        /// Use this when you manage state yourself.
        /// </summary>
        public static UINode RenderTree(DockState state, DockContextValue ctx, StyleSheet? style = null)
        {
            // Build main tiled area
            UINode tileArea;
            if (state.MaximizedPanelId is { } maxId)
            {
                var maxPanel = FindPanel(state.Root, maxId);
                tileArea = maxPanel != null
                    ? DockPanel.Render(maxPanel, ctx)
                    : RenderNode(state.Root, ctx);
            }
            else
            {
                tileArea = RenderNode(state.Root, ctx);
            }
            
            var floatNodes = state.Floats.Count == 0
                ? Array.Empty<UINode>()
                : state.Floats.Select(f => RenderFloat(f, ctx)).ToArray();
            
            var hasFloats = floatNodes.Length > 0;
            
            var outer = new StyleSheet
            {
                Display  = Display.Flex,
                Width    = Length.Percent(100),
                Height   = Length.Percent(100),
                Position = Position.Relative,
                Overflow = Overflow.Hidden,
                FlexGrow = 1f,
            }.Merge(style ?? StyleSheet.Empty);
            
            if (!hasFloats)
                return UI.Box(outer, tileArea);
            
            return UI.Box(outer,
                tileArea,
                UI.Portal(floatNodes));
        }

        // ── Root component (reads context, renders tree + floats) ─────────────

        private static UINode RootComponent(Props p)
        {
            Console.WriteLine("[DockContainer.RootComponent] Called");
            var ctx = Paper.Core.Hooks.Hooks.UseContext(DockContext.Context);
            Console.WriteLine($"[DockContainer.RootComponent] ctx = {(ctx != null ? "not null" : "NULL")}");
            if (ctx == null) 
                return UI.Box(p.Style ?? StyleSheet.Empty,
                    UI.Text("NO CONTEXT - debug", new StyleSheet { Color = new PaperColour(1, 0, 1, 1) }));
            
            var state = ctx.State;

            // Build main tiled area
            UINode tileArea;
            if (ctx.State.MaximizedPanelId is { } maxId)
            {
                var maxPanel = FindPanel(state.Root, maxId);
                tileArea = maxPanel != null
                    ? DockPanel.Render(maxPanel, ctx)
                    : RenderNode(state.Root, ctx);
            }
            else
            {
                tileArea = RenderNode(state.Root, ctx);
            }

            // Build floating panel nodes (rendered via Portal so they sit on top)
            var floatNodes = state.Floats.Count == 0
                ? Array.Empty<UINode>()
                : state.Floats.Select(f => RenderFloat(f, ctx)).ToArray();

            var hasFloats = floatNodes.Length > 0;

            var outer = new StyleSheet
            {
                Display  = Display.Flex,
                Width    = Length.Percent(100),
                Height   = Length.Percent(100),
                Position = Position.Relative,
                Overflow = Overflow.Hidden,
                FlexGrow = 1f,
            }.Merge(p.Style ?? StyleSheet.Empty);
            
            if (!hasFloats)
                return UI.Box(outer, tileArea);

            return UI.Box(outer,
                tileArea,
                UI.Portal(floatNodes));
        }

        // ── Recursive node renderer ───────────────────────────────────────────

        internal static UINode RenderNode(DockNode node, DockContextValue ctx, string? key = null) =>
            node switch
            {
                SplitNode    s  => RenderSplit(s, ctx, key),
                TabGroupNode tg => RenderTabGroup(tg, ctx, key),
                PanelNode    p  => DockPanel.Render(p, ctx, key),
                _               => UI.Box(new StyleSheet { FlexGrow = 1, Background = ColBg }),
            };

        // ── SplitNode ─────────────────────────────────────────────────────────

        private static UINode RenderSplit(SplitNode s, DockContextValue ctx, string? key)
        {
            bool isH = s.Direction == DockDirection.Horizontal;
            string splitKey = key ?? s.NodeId;

            return UI.Component(props =>
            {
                var splitId  = props.Get<string>("splitId")!;
                var ctxLocal = H.UseContext(DockContext.Context)!;
                var ratioState = H.UseStable(() => new float[] { props.Get<float?>("ratio") ?? 0.5f });
                var resizeState = H.UseStable(() => new float[] { 0f, 0f, 0f, 0f });

                float currentRatio = ratioState[0];

                StyleSheet SizeStyle(bool first) => isH
                    ? new StyleSheet { Width = Length.Percent(first ? currentRatio * 100f : (1f - currentRatio) * 100f), Display = Display.Flex, FlexDirection = FlexDirection.Column, Overflow = Overflow.Hidden }
                    : new StyleSheet { Height = Length.Percent(first ? currentRatio * 100f : (1f - currentRatio) * 100f), Display = Display.Flex, FlexDirection = FlexDirection.Column, Overflow = Overflow.Hidden };

                var handleStyle = new StyleSheet
                {
                    Width    = isH ? Length.Px(HandlePx) : Length.Percent(100),
                    Height   = isH ? Length.Percent(100) : Length.Px(HandlePx),
                    Background = ColHandle,
                    Cursor   = isH ? Cursor.ColResize : Cursor.RowResize,
                    FlexShrink = 0,
                };
                var handleHoverStyle = new StyleSheet { Background = ColHandleHov };

                var firstNode  = props.Get<DockNode>("first")!;
                var secondNode = props.Get<DockNode>("second")!;

                return UI.Box(
                    new StyleSheet
                    {
                        Display       = Display.Flex,
                        FlexDirection = isH ? FlexDirection.Row : FlexDirection.Column,
                        FlexGrow      = 1,
                        Overflow      = Overflow.Hidden,
                    },
                    UI.Box(new PropsBuilder()
                        .Style(SizeStyle(true))
                        .Children(RenderNode(firstNode, ctxLocal, firstNode.NodeId))
                        .Build()),
                    UI.Box(new PropsBuilder()
                        .Style(handleStyle)
                        .HoverStyle(handleHoverStyle)
                        .OnPointerDown(e =>
                        {
                            resizeState[0] = currentRatio;
                            resizeState[1] = isH ? e.X : e.Y;
                            resizeState[2] = 1f;
                            e.StopPropagation();
                        })
                        .OnPointerMoveCapture(e =>
                        {
                            if (resizeState[2] < 0.5f) return;
                            float delta  = (isH ? e.X : e.Y) - resizeState[1];
                            float newRatio = Math.Clamp(resizeState[0] + delta / 1000f, 0.05f, 0.95f);
                            ratioState[0] = newRatio;
                            ctxLocal.Dispatch(new DockResizeSplit { SplitNodeId = splitId, Ratio = newRatio });
                        })
                        .OnPointerUpCapture(e =>
                        {
                            resizeState[2] = 0f;
                        })
                        .Build()),
                    UI.Box(new PropsBuilder()
                        .Style(SizeStyle(false))
                        .Children(RenderNode(secondNode, ctxLocal, secondNode.NodeId))
                        .Build())
                );
            }, new PropsBuilder()
                .Set("splitId", s.NodeId)
                .Set("ratio", s.Ratio)
                .Set("first", (object)s.First)
                .Set("second", (object)s.Second)
                .Build(), splitKey);
        }

        // ── TabGroupNode ──────────────────────────────────────────────────────

        private static UINode RenderTabGroup(TabGroupNode tg, DockContextValue ctx, string? key)
        {
            return UI.Component(props =>
            {
                var groupId  = props.Get<string>("groupId")!;
                var tabs     = props.Get<List<PanelNode>>("tabs")!;
                var activeIdx = props.Get<int?>("activeIdx") ?? 0;
                var ctxLocal = Paper.Core.Hooks.Hooks.UseContext(DockContext.Context)!;

                var activePanel = tabs.Count > 0 ? tabs[Math.Clamp(activeIdx, 0, tabs.Count - 1)] : null;

                var tabBarNodes = tabs.Select((tab, i) =>
                {
                    bool isActive = i == activeIdx;
                    var tabStyle = new StyleSheet
                    {
                        Display      = Display.Flex,
                        AlignItems   = AlignItems.Center,
                        Padding      = new Thickness(Length.Px(0), Length.Px(12)),
                        Height       = Length.Px(TabBarPx),
                        Background   = isActive ? ColTabActive : PaperColour.Transparent,
                        Color        = isActive ? ColText : ColTextDim,
                        Cursor       = Cursor.Pointer,
                        BorderRadius = 0,
                        FlexShrink   = 0,
                    };
                    var tabHoverStyle = new StyleSheet { Background = isActive ? ColTabActive : ColTabHov };

                    return UI.Box(
                        new PropsBuilder()
                            .Style(tabStyle)
                            .HoverStyle(tabHoverStyle)
                            .OnClick(() => ctxLocal.Dispatch(new DockSelectTab { TabGroupNodeId = groupId, Index = i }))
                            .OnDragStart(e =>
                            {
                                e.Data = new DockDragPayload(tab.PanelId, null, false);
                                ctxLocal.SetDragging?.Invoke(true, tab.PanelId, null);
                            })
                            .OnDragEnd(e =>
                            {
                                ctxLocal.SetDragging?.Invoke(false, null, null);
                                if (e.Data is DockDragPayload { TearOff: true } payload)
                                    ctxLocal.Dispatch(new DockTearOff { SourcePanelId = payload.PanelId, X = e.X - 20, Y = e.Y - 15 });
                            })
                            .Children(UI.Text(tab.Title, new StyleSheet { Color = isActive ? ColText : ColTextDim }))
                            .Build(),
                        key: $"tab-{tab.PanelId}");
                }).ToArray();

                // Drop zone overlay on the content area (for dragging into this tab group)
                UINode contentArea = activePanel != null
                    ? UI.Box(
                        new StyleSheet { FlexGrow = 1, Display = Display.Flex, FlexDirection = FlexDirection.Column, Overflow = Overflow.Hidden, Position = Position.Relative },
                        RenderDropTarget(tg.NodeId, ctxLocal),
                        DockPanel.ContentOnly(activePanel, ctxLocal))
                    : UI.Box(new StyleSheet { FlexGrow = 1, Background = ColBg },
                        RenderDropTarget(tg.NodeId, ctxLocal));

                return UI.Box(
                    new StyleSheet
                    {
                        Display       = Display.Flex,
                        FlexDirection = FlexDirection.Column,
                        FlexGrow      = 1,
                        Overflow      = Overflow.Hidden,
                        Background    = ColBg,
                    },
                    // Tab bar
                    UI.Box(
                        new StyleSheet
                        {
                            Display       = Display.Flex,
                            FlexDirection = FlexDirection.Row,
                            FlexShrink    = 0,
                            Height        = Length.Px(TabBarPx),
                            Background    = ColHeader,
                            OverflowX     = Overflow.Hidden,
                        },
                        tabBarNodes),
                    contentArea
                );
            }, new PropsBuilder()
                .Set("groupId", tg.NodeId)
                .Set("tabs", (object)tg.Tabs)
                .Set("activeIdx", (object)(int)tg.ActiveIndex)
                .Build(), key ?? tg.NodeId);
        }

        // ── Drop zone overlay ─────────────────────────────────────────────────

        /// <summary>
        /// An absolutely-positioned transparent overlay that accepts drag-and-drop.
        /// Shows 5 quadrant targets when a panel is being dragged.
        /// </summary>
        private static UINode RenderDropTarget(string nodeId, DockContextValue ctx)
        {
            if (!ctx.IsDraggingPanel) return UI.Box(StyleSheet.Empty);

            // 5 zones: Left, Right, Top, Bottom, Center
            UINode Zone(DropZone zone, StyleSheet style) =>
                UI.Box(new PropsBuilder()
                    .Style(style.Merge(new StyleSheet
                    {
                        Position = Position.Absolute,
                        Background = zone == DropZone.Center ? ColDropCenter : ColDropZone,
                        Border = new BorderEdges(new Border(1.5f, ColDropBorder)),
                        BorderRadius = 4,
                    }))
                    .OnDragOver(e => e.StopPropagation())
                    .OnDrop(e =>
                    {
                        if (e.Data is not DockDragPayload payload) return;
                        if (payload.IsFloat)
                            ctx.Dispatch(new DockDockFloat { FloatNodeId = payload.FloatNodeId!, TargetNodeId = nodeId, Zone = zone });
                        else
                            ctx.Dispatch(new DockDrop { SourcePanelId = payload.PanelId, TargetNodeId = nodeId, Zone = zone });
                    })
                    .Build());

            const float edge = 0.25f; // fraction for L/R/T/B zones

            return UI.Box(
                new StyleSheet
                {
                    Position = Position.Absolute,
                    Left = Length.Px(0), Top = Length.Px(0),
                    Width = Length.Percent(100), Height = Length.Percent(100),
                    ZIndex = 100,
                    PointerEvents = PointerEvents.None, // outer box doesn't eat events; zones do
                },
                // Left
                Zone(DropZone.Left, new StyleSheet
                {
                    Left = Length.Px(4), Top = Length.Percent(20),
                    Width = Length.Percent(edge * 100), Height = Length.Percent(60),
                    PointerEvents = PointerEvents.Auto,
                }),
                // Right
                Zone(DropZone.Right, new StyleSheet
                {
                    Right = Length.Px(4), Top = Length.Percent(20),
                    Width = Length.Percent(edge * 100), Height = Length.Percent(60),
                    PointerEvents = PointerEvents.Auto,
                }),
                // Top
                Zone(DropZone.Top, new StyleSheet
                {
                    Top = Length.Px(4), Left = Length.Percent(20),
                    Width = Length.Percent(60), Height = Length.Percent(edge * 100),
                    PointerEvents = PointerEvents.Auto,
                }),
                // Bottom
                Zone(DropZone.Bottom, new StyleSheet
                {
                    Bottom = Length.Px(4), Left = Length.Percent(20),
                    Width = Length.Percent(60), Height = Length.Percent(edge * 100),
                    PointerEvents = PointerEvents.Auto,
                }),
                // Center (tab group)
                Zone(DropZone.Center, new StyleSheet
                {
                    Left = Length.Percent(30), Top = Length.Percent(30),
                    Width = Length.Percent(40), Height = Length.Percent(40),
                    PointerEvents = PointerEvents.Auto,
                })
            );
        }

        // ── FloatNode ─────────────────────────────────────────────────────────

        private static UINode RenderFloat(FloatNode f, DockContextValue ctx)
        {
            return UI.Component(props =>
            {
                var floatId  = props.Get<string>("floatId")!;
                var panel    = props.Get<PanelNode>("panel")!;
                var initX    = props.Get<float?>("x") ?? 100f;
                var initY    = props.Get<float?>("y") ?? 100f;
                var initW    = props.Get<float?>("w") ?? 400f;
                var initH    = props.Get<float?>("h") ?? 300f;
                var ctxLocal = Paper.Core.Hooks.Hooks.UseContext(DockContext.Context)!;

                var posState = Paper.Core.Hooks.Hooks.UseStable(() => new float[] { initX, initY, initW, initH });
                var dragState = Paper.Core.Hooks.Hooks.UseStable(() => new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f });

                float x = posState[0];
                float y = posState[1];
                float w = posState[2];
                float h = posState[3];

                var windowStyle = new StyleSheet
                {
                    Position     = Position.Absolute,
                    Left         = Length.Px(x),
                    Top          = Length.Px(y),
                    Width        = Length.Px(w),
                    Height       = Length.Px(h),
                    Background   = ColFloat,
                    Border       = new BorderEdges(new Border(1.5f, ColFloatBorder)),
                    BorderRadius = 6,
                    Display      = Display.Flex,
                    FlexDirection = FlexDirection.Column,
                    ZIndex       = 200,
                    Overflow     = Overflow.Hidden,
                };

                // Header bar (drag to move)
                var header = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet
                        {
                            Display      = Display.Flex,
                            FlexDirection = FlexDirection.Row,
                            AlignItems   = AlignItems.Center,
                            Height       = Length.Px(HeaderPx),
                            Background   = ColHeader,
                            Padding      = new Thickness(Length.Px(0), Length.Px(10)),
                            FlexShrink   = 0,
                            Cursor       = Cursor.Move,
                        })
                        .OnDragStart(e =>
                        {
                            dragState[0] = e.X;
                            dragState[1] = e.Y;
                            dragState[2] = x;
                            dragState[3] = y;
                            dragState[4] = 1f;
                            e.Data = new DockDragPayload(panel.PanelId, floatId, IsFloat: true);
                            ctxLocal.SetDragging?.Invoke(true, panel.PanelId, floatId);
                        })
                        .OnDrag(e =>
                        {
                            if (dragState[4] < 0.5f) return;
                            float nx = dragState[2] + (e.X - dragState[0]);
                            float ny = dragState[3] + (e.Y - dragState[1]);
                            posState[0] = nx;
                            posState[1] = ny;
                            ctxLocal.Dispatch(new DockMoveFloat { FloatNodeId = floatId, X = nx, Y = ny });
                        })
                        .OnDragEnd(e =>
                        {
                            dragState[4] = 0f;
                            ctxLocal.SetDragging?.Invoke(false, null, null);
                            ctxLocal.Dispatch(new DockMoveFloat { FloatNodeId = floatId, X = posState[0], Y = posState[1] });
                        })
                        .Children(
                            UI.Text(panel.Title, new StyleSheet { Color = ColText, FlexGrow = 1 }),
                            // Dock-back button
                            FloatButton("⊞", () =>
                            {
                                // Drop into root center if no specific target
                                ctxLocal.Dispatch(new DockDockFloat
                                {
                                    FloatNodeId  = floatId,
                                    TargetNodeId = ctxLocal.State.Root.NodeId,
                                    Zone         = DropZone.Center,
                                });
                            }),
                            FloatButton("✕", () => ctxLocal.Dispatch(new DockTearOff { SourcePanelId = panel.PanelId, X = -9999, Y = -9999 }))
                        )
                        .Build());

                // Content
                var content = UI.Box(
                    new StyleSheet { FlexGrow = 1, Overflow = Overflow.Hidden, Position = Position.Relative },
                    RenderDropTarget(panel.NodeId, ctxLocal),
                    DockPanel.ContentOnly(panel, ctxLocal));

                // Resize handle (bottom-right corner)
                var resizeHandle = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet
                        {
                            Position = Position.Absolute,
                            Right    = Length.Px(0),
                            Bottom   = Length.Px(0),
                            Width    = Length.Px(14),
                            Height   = Length.Px(14),
                            Cursor   = Cursor.SouthEastResize,
                        })
                        .OnPointerDown(e =>
                        {
                            dragState[0] = e.X;
                            dragState[1] = e.Y;
                            dragState[2] = w;
                            dragState[3] = h;
                            dragState[5] = 1f;
                            e.StopPropagation();
                        })
                        .OnPointerMoveCapture(e =>
                        {
                            if (dragState[5] < 0.5f) return;
                            float nw = Math.Max(150f, dragState[2] + (e.X - dragState[0]));
                            float nh = Math.Max(100f, dragState[3] + (e.Y - dragState[1]));
                            posState[2] = nw;
                            posState[3] = nh;
                        })
                        .OnPointerUpCapture(e =>
                        {
                            if (dragState[5] < 0.5f) return;
                            dragState[5] = 0f;
                            ctxLocal.Dispatch(new DockResizeFloat { FloatNodeId = floatId, Width = posState[2], Height = posState[3] });
                        })
                        .Build());

                return UI.Box(windowStyle, header, content, resizeHandle);
            }, new PropsBuilder()
                .Set("floatId", f.NodeId)
                .Set("panel", (object)f.Panel)
                .Set("x", (object)f.X)
                .Set("y", (object)f.Y)
                .Set("w", (object)f.Width)
                .Set("h", (object)f.Height)
                .Build(), f.NodeId);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static UINode FloatButton(string icon, Action onClick) =>
            UI.Box(new PropsBuilder()
                .Style(new StyleSheet
                {
                    Width        = Length.Px(22),
                    Height       = Length.Px(22),
                    Display      = Display.Flex,
                    AlignItems   = AlignItems.Center,
                    JustifyContent = JustifyContent.Center,
                    Cursor       = Cursor.Pointer,
                    BorderRadius = 3,
                    Color        = ColTextDim,
                    Margin       = new Thickness(Length.Px(0), Length.Px(2)),
                })
                .HoverStyle(new StyleSheet { Background = ColHandleHov, Color = ColText })
                .OnClick(onClick)
                .Children(UI.Text(icon, new StyleSheet { FontSize = Length.Px(12) }))
                .Build());

        private static PanelNode? FindPanel(DockNode root, string panelId) => root switch
        {
            PanelNode p    when p.PanelId == panelId => p,
            TabGroupNode tg => tg.Tabs.FirstOrDefault(t => t.PanelId == panelId),
            SplitNode s     => FindPanel(s.First, panelId) ?? FindPanel(s.Second, panelId),
            _               => null,
        };
    }

    // ── Drag payload ──────────────────────────────────────────────────────────

    /// <summary>Carried in DragEvent.Data while a panel header is being dragged.</summary>
    public sealed record DockDragPayload(string PanelId, string? FloatNodeId, bool IsFloat)
    {
        public bool TearOff { get; init; } = false;
    }
}
