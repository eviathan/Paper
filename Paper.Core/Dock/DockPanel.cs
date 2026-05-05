using Paper.Core.Events;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using H = Paper.Core.Hooks.Hooks;

namespace Paper.Core.Dock
{
    // ── DockPanel ─────────────────────────────────────────────────────────────
    //
    // Renders a single leaf panel:
    //   • Draggable header with title, close, minimize, maximize, and float buttons
    //   • Content area that flexes to fill available space
    //   • Minimized state collapses to a thin strip showing title + restore button
    //   • Buttons are hidden when the panel's DockPanelConstraints disallows them
    //
    // Colours come from ctx.Theme so both dark and light themes are supported.
    // ─────────────────────────────────────────────────────────────────────────

    public static class DockPanel
    {
        // ── Full panel (header + content) ─────────────────────────────────────

        public static UINode Render(PanelNode panel, DockContextValue ctx, string? key = null)
        {
            if (panel.Minimized)
                return RenderMinimizedStrip(panel, ctx, key);

            var theme       = ctx.Theme;
            var constraints = ctx.GetConstraints(panel.PanelId);
            var factory     = ctx.GetPanel(panel.PanelId);

            UINode content = factory != null
                ? factory()
                : UI.Text($"(no panel: {panel.PanelId})", new StyleSheet { Color = theme.TextDim });

            var headerChildren = new List<UINode>
            {
                UI.Text(panel.Title ?? panel.PanelId, new StyleSheet
                {
                    Color       = theme.Text,
                    FlexGrow    = 1,
                    PaddingLeft = Length.Px(8),
                }),
            };

            if (constraints?.AllowMinimize != false)
                headerChildren.Add(HeaderButton("▼", theme, () =>
                    ctx.Dispatch(new DockMinimize { PanelId = panel.PanelId, Minimized = true })));

            if (constraints?.AllowMaximize != false)
                headerChildren.Add(HeaderButton("□", theme, () =>
                    ctx.Dispatch(new DockMaximize { PanelId = panel.PanelId })));

            if (constraints?.AllowFloat != false)
                headerChildren.Add(HeaderButton("⊟", theme, () =>
                    ctx.Dispatch(new DockTearOff { SourcePanelId = panel.PanelId, X = 100, Y = 100 })));

            if (constraints?.AllowClose != false)
                headerChildren.Add(HeaderButton("✕", theme, () =>
                    ctx.Dispatch(new DockClosePanel { PanelId = panel.PanelId }),
                    hoverBackground: theme.CloseHover));

            return UI.Box(new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.Column,
                FlexGrow      = 1,
                Overflow      = Overflow.Hidden,
                Background    = theme.Bg,
                Border        = new BorderEdges(new Border(1f, theme.Border)),
            },
                UI.Box(new PropsBuilder()
                    .Style(new StyleSheet
                    {
                        Display       = Display.Flex,
                        FlexDirection = FlexDirection.Row,
                        AlignItems    = AlignItems.Center,
                        Height        = Length.Px(theme.HeaderPx),
                        Background    = theme.Header,
                        FlexShrink    = 0,
                        Cursor        = Cursor.Move,
                    })
                    .OnDragStart(e =>
                    {
                        e.Data = new DockDragPayload(panel.PanelId, null, IsFloat: false);
                        ctx.SetDragging?.Invoke(true, panel.PanelId, null);
                        if (ctx.Session != null)
                            ctx.Session.BeginCrossWindowDrag(panel.PanelId, ctx.WindowId, () =>
                            {
                                ctx.Dispatch(new DockRemovePanel { PanelId = panel.PanelId });
                                ctx.SetDragging?.Invoke(false, null, null);
                            });
                    })
                    .OnDragEnd(e =>
                    {
                        Console.WriteLine($"[DockDbg] DockPanel.OnDragEnd: outside={e.OutsideSourceWindow} hasPayload={e.Data is DockDragPayload} session={ctx.Session != null}");
                        ctx.SetDragging?.Invoke(false, null, null);
                        if (e.Data is DockDragPayload payload)
                        {
                            if (e.OutsideSourceWindow)
                                ctx.Dispatch(new DockEjectToNewWindow { PanelId = payload.PanelId, X = e.X, Y = e.Y, ScreenX = e.ScreenX, ScreenY = e.ScreenY });
                            else
                                ctx.Session?.CancelCrossWindowDrag();
                        }
                        else
                        {
                            ctx.Session?.CancelCrossWindowDrag();
                        }
                    })
                    .Children(headerChildren.ToArray())
                    .Build()),
                UI.Box(new StyleSheet
                {
                    FlexGrow      = 1,
                    Position      = Position.Relative,
                    Display       = Display.Flex,
                    FlexDirection = FlexDirection.Column,
                    Overflow      = Overflow.Hidden,
                },
                    UI.Box(new StyleSheet
                    {
                        FlexGrow      = 1,
                        Display       = Display.Flex,
                        FlexDirection = FlexDirection.Column,
                        Overflow      = Overflow.Hidden,
                    }, content),
                    DockContainer.RenderDropTarget(panel.NodeId, ctx))
            );
        }

        // ── Content only (used by TabGroup + Float) ───────────────────────────

        public static UINode ContentOnly(PanelNode panel, DockContextValue ctx)
        {
            var factory = ctx.GetPanel(panel.PanelId);
            if (factory == null)
                return UI.Box(
                    new StyleSheet
                    {
                        FlexGrow       = 1,
                        Display        = Display.Flex,
                        AlignItems     = AlignItems.Center,
                        JustifyContent = JustifyContent.Center,
                    },
                    UI.Text($"(no panel: {panel.PanelId})", new StyleSheet { Color = ctx.Theme.TextDim }));

            return UI.Box(
                new StyleSheet
                {
                    FlexGrow      = 1,
                    Display       = Display.Flex,
                    FlexDirection = FlexDirection.Column,
                    Overflow      = Overflow.Hidden,
                },
                factory());
        }

        // ── Minimized strip ───────────────────────────────────────────────────

        private static UINode RenderMinimizedStrip(PanelNode panel, DockContextValue ctx, string? key)
        {
            var theme = ctx.Theme;
            return UI.Box(new PropsBuilder()
                .Style(new StyleSheet
                {
                    Display       = Display.Flex,
                    FlexDirection = FlexDirection.Row,
                    AlignItems    = AlignItems.Center,
                    Height        = Length.Px(theme.MinStripPx),
                    Background    = theme.MinStrip,
                    Border        = new BorderEdges(new Border(1f, theme.Border)),
                    FlexShrink    = 0,
                    Padding       = new Thickness(Length.Px(0), Length.Px(8)),
                })
                .Children(
                    UI.Text(panel.Title, new StyleSheet
                    {
                        Color    = theme.TextDim,
                        FlexGrow = 1,
                        FontSize = Length.Px(12),
                    }),
                    HeaderButton("▲", theme, () => ctx.Dispatch(new DockMinimize { PanelId = panel.PanelId, Minimized = false })),
                    HeaderButton("□", theme, () => ctx.Dispatch(new DockMaximize { PanelId = panel.PanelId }))
                )
                .Build(), key ?? panel.NodeId);
        }

        // ── Button helper ─────────────────────────────────────────────────────

        internal static UINode HeaderButton(
            string       icon,
            DockTheme    theme,
            Action       onClick,
            PaperColour? hoverBackground = null) =>
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
                    Margin         = new Thickness(Length.Px(0), Length.Px(1)),
                })
                .HoverStyle(new StyleSheet { Background = hoverBackground ?? theme.ButtonHover, Color = theme.Text })
                .OnClick(onClick)
                .Children(UI.Text(icon, new StyleSheet { FontSize = Length.Px(11) }))
                .Build());
    }
}
