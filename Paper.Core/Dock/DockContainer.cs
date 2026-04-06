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
    // During drag:
    //   • Per-panel drop zones rendered as transparent overlays inside each panel
    //   • Screen-edge drop zones rendered as an outer absolute overlay
    //   • Auto-hide strips rendered as thin bars along each occupied edge
    //
    // Floating panels are rendered via Portal on top of everything.
    // ─────────────────────────────────────────────────────────────────────────

    public static class DockContainer
    {
        // ── Public entry points ───────────────────────────────────────────────

        /// <summary>
        /// Renders the full dock layout reading state from DockContext.
        /// Place this inside a <c>DockContext.Root(...)</c> wrapper.
        /// </summary>
        public static UINode Render(StyleSheet? style = null) =>
            UI.Component(RootComponent, new PropsBuilder().Style(style ?? StyleSheet.Empty).Build());

        // ── Root component ────────────────────────────────────────────────────

        private static UINode RootComponent(Props p)
        {
            var ctx = H.UseContext(DockContext.Context);
            if (ctx == null)
                return UI.Box(p.Style ?? StyleSheet.Empty,
                    UI.Text("NO CONTEXT — wrap with DockContext.Root()",
                        new StyleSheet { Color = new PaperColour(1, 0, 1, 1) }));

            var state = ctx.State;
            var theme = ctx.Theme;

            // ── Tiled area ────────────────────────────────────────────────────
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

            // ── Auto-hide strips ──────────────────────────────────────────────
            var autoHideNodes = new List<UINode>();
            if (state.AutoHidePanels.Count > 0)
            {
                foreach (AutoHideEdge edge in Enum.GetValues<AutoHideEdge>())
                {
                    var edgePanels = state.AutoHidePanels.Where(e => e.Edge == edge).ToList();
                    if (edgePanels.Count > 0)
                        autoHideNodes.Add(RenderAutoHideStrip(edge, edgePanels, ctx));
                }
            }

            // ── Float windows ─────────────────────────────────────────────────
            var floatNodes = state.Floats.Count == 0
                ? Array.Empty<UINode>()
                : state.Floats.Select(f => RenderFloat(f, ctx)).ToArray();

            // ── Outer edge drop zones (shown while dragging) ──────────────────
            var outerStyle = new StyleSheet
            {
                Display  = Display.Flex,
                Width    = Length.Percent(100),
                Height   = Length.Percent(100),
                Position = Position.Relative,
                Overflow = Overflow.Hidden,
                FlexGrow = 1f,
            }.Merge(p.Style ?? StyleSheet.Empty);

            var children = new List<UINode> { tileArea };
            children.AddRange(autoHideNodes);
            if (ctx.IsDraggingPanel)
                children.Add(RenderOuterDropZones(ctx));

            if (floatNodes.Length > 0)
                children.Add(UI.Portal(floatNodes));

            return UI.Box(outerStyle, children.ToArray());
        }

        // ── Recursive node renderer ───────────────────────────────────────────

        internal static UINode RenderNode(DockNode node, DockContextValue ctx, string? key = null) =>
            node switch
            {
                SplitNode    s  => RenderSplit(s, ctx, key),
                TabGroupNode tg => RenderTabGroup(tg, ctx, key),
                PanelNode    p  => DockPanel.Render(p, ctx, key),
                _               => UI.Box(new StyleSheet { FlexGrow = 1, Background = ctx.Theme.Bg }),
            };

        // ── SplitNode ─────────────────────────────────────────────────────────

        private static UINode RenderSplit(SplitNode s, DockContextValue ctx, string? key)
        {
            bool isH      = s.Direction == DockDirection.Horizontal;
            string splitKey = key ?? s.NodeId;

            return UI.Component(props =>
            {
                var splitId     = props.Get<string>("splitId")!;
                var ctxLocal    = H.UseContext(DockContext.Context)!;
                var theme       = ctxLocal.Theme;
                var ratioState  = H.UseStable(() => new float[] { props.Get<float?>("ratio") ?? 0.5f });
                var resizeState = H.UseStable(() => new float[] { 0f, 0f, 0f, 0f });

                float currentRatio = ratioState[0];

                StyleSheet SizeStyle(bool first) => isH
                    ? new StyleSheet
                    {
                        Width         = Length.Percent(first ? currentRatio * 100f : (1f - currentRatio) * 100f),
                        Display       = Display.Flex,
                        FlexDirection = FlexDirection.Column,
                        Overflow      = Overflow.Hidden,
                    }
                    : new StyleSheet
                    {
                        Height        = Length.Percent(first ? currentRatio * 100f : (1f - currentRatio) * 100f),
                        Display       = Display.Flex,
                        FlexDirection = FlexDirection.Column,
                        Overflow      = Overflow.Hidden,
                    };

                var handleStyle = new StyleSheet
                {
                    Width      = isH ? Length.Px(theme.HandlePx) : Length.Percent(100),
                    Height     = isH ? Length.Percent(100)        : Length.Px(theme.HandlePx),
                    Background = theme.Handle,
                    Cursor     = isH ? Cursor.ColResize : Cursor.RowResize,
                    FlexShrink = 0,
                };

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
                        .HoverStyle(new StyleSheet { Background = theme.HandleHover })
                        .OnPointerDown(e =>
                        {
                            resizeState[0] = currentRatio;
                            resizeState[1] = isH ? e.X : e.Y;
                            resizeState[2] = 1f;
                            resizeState[3] = currentRatio > 0.05f
                                ? (isH ? e.X : e.Y) / currentRatio
                                : 800f;
                            e.StopPropagation();
                        })
                        .OnPointerMoveCapture(e =>
                        {
                            if (resizeState[2] < 0.5f) return;
                            float delta         = (isH ? e.X : e.Y) - resizeState[1];
                            float containerSize = Math.Max(100f, resizeState[3]);
                            float newRatio      = Math.Clamp(resizeState[0] + delta / containerSize, 0.05f, 0.95f);
                            ratioState[0] = newRatio;
                            ctxLocal.Dispatch(new DockResizeSplit { SplitNodeId = splitId, Ratio = newRatio });
                        })
                        .OnPointerUpCapture(e => resizeState[2] = 0f)
                        .Build()),
                    UI.Box(new PropsBuilder()
                        .Style(SizeStyle(false))
                        .Children(RenderNode(secondNode, ctxLocal, secondNode.NodeId))
                        .Build())
                );
            }, new PropsBuilder()
                .Set("splitId", s.NodeId)
                .Set("ratio",   s.Ratio)
                .Set("first",   (object)s.First)
                .Set("second",  (object)s.Second)
                .Build(), splitKey);
        }

        // ── TabGroupNode ──────────────────────────────────────────────────────

        private static UINode RenderTabGroup(TabGroupNode tg, DockContextValue ctx, string? key)
        {
            return UI.Component(props =>
            {
                var groupId   = props.Get<string>("groupId")!;
                var tabs      = props.Get<List<PanelNode>>("tabs")!;
                var activeIdx = props.Get<int?>("activeIdx") ?? 0;
                var ctxLocal  = H.UseContext(DockContext.Context)!;
                var theme     = ctxLocal.Theme;

                var activePanel = tabs.Count > 0 ? tabs[Math.Clamp(activeIdx, 0, tabs.Count - 1)] : null;

                var tabBarNodes = tabs.Select((tab, i) =>
                {
                    bool isActive = i == activeIdx;
                    var constraints = ctxLocal.GetConstraints(tab.PanelId);
                    var tabChildren = new List<UINode>
                    {
                        UI.Text(tab.Title, new StyleSheet { Color = isActive ? theme.Text : theme.TextDim }),
                    };
                    if (constraints?.AllowClose != false)
                        tabChildren.Add(UI.Box(new PropsBuilder()
                            .Style(new StyleSheet
                            {
                                Width          = Length.Px(16),
                                Height         = Length.Px(16),
                                Display        = Display.Flex,
                                AlignItems     = AlignItems.Center,
                                JustifyContent = JustifyContent.Center,
                                BorderRadius   = 3,
                                MarginLeft     = Length.Px(4),
                                Color          = theme.TextDim,
                            })
                            .HoverStyle(new StyleSheet { Background = theme.CloseHover, Color = theme.Text })
                            .OnClick(() => ctxLocal.Dispatch(new DockClosePanel { PanelId = tab.PanelId }))
                            .Children(UI.Text("✕", new StyleSheet { FontSize = Length.Px(9) }))
                            .Build()));

                    return UI.Box(
                        new PropsBuilder()
                            .Style(new StyleSheet
                            {
                                Display      = Display.Flex,
                                AlignItems   = AlignItems.Center,
                                Padding      = new Thickness(Length.Px(0), Length.Px(10)),
                                Height       = Length.Px(theme.TabBarPx),
                                Background   = isActive ? theme.TabActive : PaperColour.Transparent,
                                Color        = isActive ? theme.Text : theme.TextDim,
                                Cursor       = Cursor.Pointer,
                                BorderRadius = 0,
                                FlexShrink   = 0,
                            })
                            .HoverStyle(new StyleSheet { Background = isActive ? theme.TabActive : theme.TabHover })
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
                            .Children(tabChildren.ToArray())
                            .Build(),
                        key: $"tab-{tab.PanelId}");
                }).ToArray();

                UINode contentArea = activePanel != null
                    ? UI.Box(
                        new StyleSheet { FlexGrow = 1, Display = Display.Flex, FlexDirection = FlexDirection.Column, Overflow = Overflow.Hidden, Position = Position.Relative },
                        RenderDropTarget(tg.NodeId, ctxLocal),
                        DockPanel.ContentOnly(activePanel, ctxLocal))
                    : UI.Box(
                        new StyleSheet { FlexGrow = 1, Background = theme.Bg },
                        RenderDropTarget(tg.NodeId, ctxLocal));

                return UI.Box(
                    new StyleSheet
                    {
                        Display       = Display.Flex,
                        FlexDirection = FlexDirection.Column,
                        FlexGrow      = 1,
                        Overflow      = Overflow.Hidden,
                        Background    = theme.Bg,
                    },
                    UI.Box(
                        new StyleSheet
                        {
                            Display       = Display.Flex,
                            FlexDirection = FlexDirection.Row,
                            FlexShrink    = 0,
                            Height        = Length.Px(theme.TabBarPx),
                            Background    = theme.Header,
                            OverflowX     = Overflow.Hidden,
                        },
                        tabBarNodes),
                    contentArea
                );
            }, new PropsBuilder()
                .Set("groupId",   tg.NodeId)
                .Set("tabs",      (object)tg.Tabs)
                .Set("activeIdx", (object)(int)tg.ActiveIndex)
                .Build(), key ?? tg.NodeId);
        }

        // ── Per-panel drop zone overlay ───────────────────────────────────────

        /// <summary>
        /// Absolutely-positioned overlay showing local drop zones while a panel is dragged.
        /// Only visible while <see cref="DockContextValue.IsDraggingPanel"/> is true.
        /// </summary>
        internal static UINode RenderDropTarget(string nodeId, DockContextValue ctx)
        {
            if (!ctx.IsDraggingPanel) return UI.Box(StyleSheet.Empty);

            var theme       = ctx.Theme;
            var constraints = ctx.GetConstraints(nodeId);
            var allowed     = constraints?.AllowedDropZones;

            bool IsAllowed(DropZone zone) => allowed == null || allowed.Contains(zone);

            UINode Zone(DropZone zone, StyleSheet style) =>
                UI.Box(new PropsBuilder()
                    .Style(style.Merge(new StyleSheet
                    {
                        Position     = Position.Absolute,
                        Background   = zone == DropZone.Center ? theme.DropCenter : theme.DropZone,
                        Border       = new BorderEdges(new Border(1.5f, theme.DropBorder)),
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

            const float edgeFraction = 0.25f;

            var zones = new List<UINode>();
            if (IsAllowed(DropZone.Left))
                zones.Add(Zone(DropZone.Left, new StyleSheet
                {
                    Left = Length.Px(4), Top = Length.Percent(20),
                    Width = Length.Percent(edgeFraction * 100), Height = Length.Percent(60),
                    PointerEvents = PointerEvents.Auto,
                }));
            if (IsAllowed(DropZone.Right))
                zones.Add(Zone(DropZone.Right, new StyleSheet
                {
                    Right = Length.Px(4), Top = Length.Percent(20),
                    Width = Length.Percent(edgeFraction * 100), Height = Length.Percent(60),
                    PointerEvents = PointerEvents.Auto,
                }));
            if (IsAllowed(DropZone.Top))
                zones.Add(Zone(DropZone.Top, new StyleSheet
                {
                    Top = Length.Px(4), Left = Length.Percent(20),
                    Width = Length.Percent(60), Height = Length.Percent(edgeFraction * 100),
                    PointerEvents = PointerEvents.Auto,
                }));
            if (IsAllowed(DropZone.Bottom))
                zones.Add(Zone(DropZone.Bottom, new StyleSheet
                {
                    Bottom = Length.Px(4), Left = Length.Percent(20),
                    Width = Length.Percent(60), Height = Length.Percent(edgeFraction * 100),
                    PointerEvents = PointerEvents.Auto,
                }));
            if (IsAllowed(DropZone.Center))
                zones.Add(Zone(DropZone.Center, new StyleSheet
                {
                    Left = Length.Percent(30), Top = Length.Percent(30),
                    Width = Length.Percent(40), Height = Length.Percent(40),
                    PointerEvents = PointerEvents.Auto,
                }));

            return UI.Box(
                new StyleSheet
                {
                    Position      = Position.Absolute,
                    Left          = Length.Px(0),
                    Top           = Length.Px(0),
                    Width         = Length.Percent(100),
                    Height        = Length.Percent(100),
                    ZIndex        = 100,
                    PointerEvents = PointerEvents.None,
                },
                zones.ToArray());
        }

        // ── Screen-edge outer drop zones ──────────────────────────────────────

        /// <summary>
        /// Full-surface overlay rendered during drag. Shows four thin strips at the
        /// screen edges that let the user wrap the entire layout in a new split.
        /// </summary>
        private static UINode RenderOuterDropZones(DockContextValue ctx)
        {
            var theme = ctx.Theme;
            const float stripPx = 20f;

            UINode OuterZone(DropZone zone, StyleSheet style) =>
                UI.Box(new PropsBuilder()
                    .Style(style.Merge(new StyleSheet
                    {
                        Position      = Position.Absolute,
                        Background    = theme.DropZone,
                        Border        = new BorderEdges(new Border(1.5f, theme.DropBorder)),
                        PointerEvents = PointerEvents.Auto,
                        ZIndex        = 90,
                    }))
                    .OnDragOver(e => e.StopPropagation())
                    .OnDrop(e =>
                    {
                        if (e.Data is not DockDragPayload payload) return;
                        if (payload.IsFloat)
                            ctx.Dispatch(new DockDockFloatOuter { FloatNodeId = payload.FloatNodeId!, Zone = zone });
                        else
                            ctx.Dispatch(new DockDropOuter { SourcePanelId = payload.PanelId, Zone = zone });
                    })
                    .Build());

            return UI.Box(
                new StyleSheet
                {
                    Position      = Position.Absolute,
                    Left          = Length.Px(0),
                    Top           = Length.Px(0),
                    Width         = Length.Percent(100),
                    Height        = Length.Percent(100),
                    ZIndex        = 89,
                    PointerEvents = PointerEvents.None,
                },
                OuterZone(DropZone.Left, new StyleSheet
                {
                    Left = Length.Px(0), Top = Length.Px(0),
                    Width = Length.Px(stripPx), Height = Length.Percent(100),
                }),
                OuterZone(DropZone.Right, new StyleSheet
                {
                    Right = Length.Px(0), Top = Length.Px(0),
                    Width = Length.Px(stripPx), Height = Length.Percent(100),
                }),
                OuterZone(DropZone.Top, new StyleSheet
                {
                    Top = Length.Px(0), Left = Length.Px(0),
                    Width = Length.Percent(100), Height = Length.Px(stripPx),
                }),
                OuterZone(DropZone.Bottom, new StyleSheet
                {
                    Bottom = Length.Px(0), Left = Length.Px(0),
                    Width = Length.Percent(100), Height = Length.Px(stripPx),
                })
            );
        }

        // ── Auto-hide strips ──────────────────────────────────────────────────

        private static UINode RenderAutoHideStrip(AutoHideEdge edge, List<AutoHideEntry> entries, DockContextValue ctx)
        {
            return UI.Component(props =>
            {
                var edgeLocal    = props.Get<AutoHideEdge?>("edge") ?? AutoHideEdge.Bottom;
                var entriesLocal = props.Get<List<AutoHideEntry>>("entries")!;
                var ctxLocal     = H.UseContext(DockContext.Context)!;
                var theme        = ctxLocal.Theme;

                var (hoveredId, setHoveredId, _) = H.UseState<string?>(null);

                bool isH      = edgeLocal == AutoHideEdge.Left || edgeLocal == AutoHideEdge.Right;
                bool isEnd    = edgeLocal == AutoHideEdge.Right || edgeLocal == AutoHideEdge.Bottom;
                const float stripPx = 24f;

                var stripStyle = new StyleSheet
                {
                    Position      = Position.Absolute,
                    Background    = theme.MinStrip,
                    Border        = new BorderEdges(new Border(1f, theme.Border)),
                    Display       = Display.Flex,
                    FlexDirection = isH ? FlexDirection.Column : FlexDirection.Row,
                    ZIndex        = 150,
                };

                switch (edgeLocal)
                {
                    case AutoHideEdge.Left:
                        stripStyle = stripStyle.Merge(new StyleSheet { Left = Length.Px(0), Top = Length.Px(0), Width = Length.Px(stripPx), Height = Length.Percent(100) });
                        break;
                    case AutoHideEdge.Right:
                        stripStyle = stripStyle.Merge(new StyleSheet { Right = Length.Px(0), Top = Length.Px(0), Width = Length.Px(stripPx), Height = Length.Percent(100) });
                        break;
                    case AutoHideEdge.Top:
                        stripStyle = stripStyle.Merge(new StyleSheet { Top = Length.Px(0), Left = Length.Px(0), Width = Length.Percent(100), Height = Length.Px(stripPx) });
                        break;
                    default:
                        stripStyle = stripStyle.Merge(new StyleSheet { Bottom = Length.Px(0), Left = Length.Px(0), Width = Length.Percent(100), Height = Length.Px(stripPx) });
                        break;
                }

                var tabs = entriesLocal.Select(entry =>
                    UI.Box(new PropsBuilder()
                        .Style(new StyleSheet
                        {
                            Display      = Display.Flex,
                            AlignItems   = AlignItems.Center,
                            Padding      = new Thickness(Length.Px(4), Length.Px(8)),
                            Background   = hoveredId == entry.Panel.PanelId ? theme.TabHover : PaperColour.Transparent,
                            Cursor       = Cursor.Pointer,
                            FlexShrink   = 0,
                        })
                        .HoverStyle(new StyleSheet { Background = theme.TabHover })
                        .OnMouseEnter(() => setHoveredId(entry.Panel.PanelId))
                        .OnMouseLeave(() => setHoveredId(null))
                        .OnClick(() => ctxLocal.Dispatch(new DockSetAutoHide { PanelId = entry.Panel.PanelId, Edge = null }))
                        .Children(UI.Text(entry.Panel.Title, new StyleSheet { Color = theme.TextDim, FontSize = Length.Px(11) }))
                        .Build(),
                        key: entry.NodeId)
                ).ToArray();

                return UI.Box(stripStyle, tabs);
            }, new PropsBuilder()
                .Set("edge",    (object)edge)
                .Set("entries", (object)entries)
                .Build(), $"autohide-{edge}");
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
                var ctxLocal = H.UseContext(DockContext.Context)!;
                var theme    = ctxLocal.Theme;

                var posState  = H.UseStable(() => new float[] { initX, initY, initW, initH });
                var dragState = H.UseStable(() => new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f });

                float x = posState[0], y = posState[1], w = posState[2], h = posState[3];

                var windowStyle = new StyleSheet
                {
                    Position      = Position.Absolute,
                    Left          = Length.Px(x),
                    Top           = Length.Px(y),
                    Width         = Length.Px(w),
                    Height        = Length.Px(h),
                    Background    = theme.Float,
                    Border        = new BorderEdges(new Border(1.5f, theme.FloatBorder)),
                    BorderRadius  = 6,
                    Display       = Display.Flex,
                    FlexDirection = FlexDirection.Column,
                    ZIndex        = 200,
                    Overflow      = Overflow.Hidden,
                };

                var constraints = ctxLocal.GetConstraints(panel.PanelId);

                var headerChildren = new List<UINode>
                {
                    UI.Text(panel.Title, new StyleSheet { Color = theme.Text, FlexGrow = 1 }),
                };

                // Dock back into tiled layout
                headerChildren.Add(FloatButton("⊞", theme, () => ctxLocal.Dispatch(new DockDockFloat
                {
                    FloatNodeId  = floatId,
                    TargetNodeId = ctxLocal.State.Root.NodeId,
                    Zone         = DropZone.Center,
                })));

                if (constraints?.AllowClose != false)
                    headerChildren.Add(FloatButton("✕", theme, () =>
                        ctxLocal.Dispatch(new DockClosePanel { PanelId = panel.PanelId }),
                        hoverBackground: theme.CloseHover));

                var header = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet
                        {
                            Display       = Display.Flex,
                            FlexDirection = FlexDirection.Row,
                            AlignItems    = AlignItems.Center,
                            Height        = Length.Px(theme.HeaderPx),
                            Background    = theme.Header,
                            Padding       = new Thickness(Length.Px(0), Length.Px(10)),
                            FlexShrink    = 0,
                            Cursor        = Cursor.Move,
                        })
                        .OnDragStart(e =>
                        {
                            dragState[0] = e.X; dragState[1] = e.Y;
                            dragState[2] = x;   dragState[3] = y;
                            dragState[4] = 1f;
                            e.Data = new DockDragPayload(panel.PanelId, floatId, IsFloat: true);
                            ctxLocal.SetDragging?.Invoke(true, panel.PanelId, floatId);
                        })
                        .OnDrag(e =>
                        {
                            if (dragState[4] < 0.5f) return;
                            float nx = dragState[2] + (e.X - dragState[0]);
                            float ny = dragState[3] + (e.Y - dragState[1]);
                            posState[0] = nx; posState[1] = ny;
                            ctxLocal.Dispatch(new DockMoveFloat { FloatNodeId = floatId, X = nx, Y = ny });
                        })
                        .OnDragEnd(e =>
                        {
                            dragState[4] = 0f;
                            ctxLocal.SetDragging?.Invoke(false, null, null);
                            ctxLocal.Dispatch(new DockMoveFloat { FloatNodeId = floatId, X = posState[0], Y = posState[1] });
                        })
                        .Children(headerChildren.ToArray())
                        .Build());

                var content = UI.Box(
                    new StyleSheet { FlexGrow = 1, Overflow = Overflow.Hidden, Position = Position.Relative },
                    RenderDropTarget(panel.NodeId, ctxLocal),
                    DockPanel.ContentOnly(panel, ctxLocal));

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
                            dragState[0] = e.X; dragState[1] = e.Y;
                            dragState[2] = w;   dragState[3] = h;
                            dragState[5] = 1f;
                            e.StopPropagation();
                        })
                        .OnPointerMoveCapture(e =>
                        {
                            if (dragState[5] < 0.5f) return;
                            posState[2] = Math.Max(150f, dragState[2] + (e.X - dragState[0]));
                            posState[3] = Math.Max(100f, dragState[3] + (e.Y - dragState[1]));
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
                .Set("panel",   (object)f.Panel)
                .Set("x",       (object)f.X)
                .Set("y",       (object)f.Y)
                .Set("w",       (object)f.Width)
                .Set("h",       (object)f.Height)
                .Build(), f.NodeId);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static UINode FloatButton(string icon, DockTheme theme, Action onClick, PaperColour? hoverBackground = null) =>
            UI.Box(new PropsBuilder()
                .Style(new StyleSheet
                {
                    Width          = Length.Px(22),
                    Height         = Length.Px(22),
                    Display        = Display.Flex,
                    AlignItems     = AlignItems.Center,
                    JustifyContent = JustifyContent.Center,
                    Cursor         = Cursor.Pointer,
                    BorderRadius   = 3,
                    Color          = theme.TextDim,
                    Margin         = new Thickness(Length.Px(0), Length.Px(2)),
                })
                .HoverStyle(new StyleSheet { Background = hoverBackground ?? theme.ButtonHover, Color = theme.Text })
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
