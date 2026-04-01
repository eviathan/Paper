using Paper.Core.Events;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using H = Paper.Core.Hooks.Hooks;

namespace Paper.Core.Dock
{
    // ── DockPanel ─────────────────────────────────────────────────────────────
    //
    // Renders a single leaf panel:
    //   • Draggable header with title, minimize, maximize, and close-to-float buttons
    //   • Content area that flexes to fill available space
    //   • Minimized state collapses to a thin strip showing title + restore button
    //   • Drop zone overlay while another panel is being dragged
    //   • Maximized state is handled by DockContainer (replaces tree with this panel)
    // ─────────────────────────────────────────────────────────────────────────

    public static class DockPanel
    {
        // ── Colours (match DockContainer theme) ───────────────────────────────

        private static readonly PaperColour ColBg        = new("#1a1a2e");
        private static readonly PaperColour ColHeader    = new("#252545");
        private static readonly PaperColour ColHeaderHov = new("#2d2d58");
        private static readonly PaperColour ColBorder    = new("#3a3a5a");
        private static readonly PaperColour ColText      = new("#c8c8e0");
        private static readonly PaperColour ColTextDim   = new("#7878a0");
        private static readonly PaperColour ColMinStrip  = new("#141428");
        private static readonly PaperColour ColButton    = PaperColour.Transparent;
        private static readonly PaperColour ColButtonHov = new("#404070");

        private const float HeaderPx   = 30f;
        private const float MinStripPx = 24f;

        // ── Full panel (header + content) ─────────────────────────────────────

        public static UINode Render(PanelNode panel, DockContextValue ctx, string? key = null)
        {
            // Minimized → thin strip
            if (panel.Minimized)
                return RenderMinimizedStrip(panel, ctx, key);

            // Get panel content from registry
            var factory = ctx.GetPanel(panel.PanelId);
            UINode content = factory != null 
                ? factory() 
                : UI.Text($"(no panel: {panel.PanelId})", new StyleSheet { Color = ColTextDim });

            // Header buttons
            UINode MinimizeBtn() => UI.Box(new PropsBuilder()
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
                .HoverStyle(new StyleSheet { Background = ColButtonHov, Color = ColText })
                .OnClick(() => ctx.Dispatch(new DockMinimize { PanelId = panel.PanelId, Minimized = true }))
                .Children(UI.Text("▼", new StyleSheet { FontSize = Length.Px(10) }))
                .Build());

            UINode MaximizeBtn() => UI.Box(new PropsBuilder()
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
                .HoverStyle(new StyleSheet { Background = ColButtonHov, Color = ColText })
                .OnClick(() => ctx.Dispatch(new DockMaximize { PanelId = panel.PanelId }))
                .Children(UI.Text("□", new StyleSheet { FontSize = Length.Px(10) }))
                .Build());

            UINode CloseBtn() => UI.Box(new PropsBuilder()
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
                .HoverStyle(new StyleSheet { Background = new PaperColour(0.8f, 0.2f, 0.2f, 1f), Color = ColText })
                .OnClick(() => ctx.Dispatch(new DockTearOff { SourcePanelId = panel.PanelId, X = -9999, Y = -9999 }))
                .Children(UI.Text("✕", new StyleSheet { FontSize = Length.Px(10) }))
                .Build());

            return UI.Box(new StyleSheet 
            { 
                Display = Display.Flex, 
                FlexDirection = FlexDirection.Column, 
                FlexGrow = 1, 
                Overflow = Overflow.Hidden, 
                Background = ColBg,
                Border = new BorderEdges(new Border(1f, ColBorder)),
            },
                // Header
                UI.Box(new StyleSheet 
                { 
                    Display = Display.Flex, 
                    FlexDirection = FlexDirection.Row,
                    AlignItems = AlignItems.Center,
                    Height = Length.Px(HeaderPx),
                    Background = ColHeader,
                    FlexShrink = 0,
                    Cursor = Cursor.Move,
                },
                    UI.Text(panel.Title ?? panel.PanelId, new StyleSheet 
                    { 
                        Color = ColText, 
                        FlexGrow = 1,
                        Padding = new Thickness(Length.Px(8), Length.Px(0)),
                    }),
                    MinimizeBtn(),
                    MaximizeBtn(),
                    CloseBtn()
                ),
                // Content
                UI.Box(new StyleSheet { FlexGrow = 1, Overflow = Overflow.Hidden, Padding = new Thickness(Length.Px(8)) },
                    content
                )
            );
        }

        // ── Content only (used by TabGroup + Float) ───────────────────────────

        public static UINode ContentOnly(PanelNode panel, DockContextValue ctx)
        {
            var factory = ctx.GetPanel(panel.PanelId);
            if (factory == null)
                return UI.Box(
                    new StyleSheet { FlexGrow = 1, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center },
                    UI.Text($"(no panel: {panel.PanelId})", new StyleSheet { Color = ColTextDim }));

            return UI.Box(
                new StyleSheet { FlexGrow = 1, Display = Display.Flex, FlexDirection = FlexDirection.Column, Overflow = Overflow.Hidden },
                factory());
        }

        // ── Minimized strip ───────────────────────────────────────────────────

        private static UINode RenderMinimizedStrip(PanelNode panel, DockContextValue ctx, string? key)
        {
            return UI.Box(new PropsBuilder()
                .Style(new StyleSheet
                {
                    Display       = Display.Flex,
                    FlexDirection = FlexDirection.Row,
                    AlignItems    = AlignItems.Center,
                    Height        = Length.Px(MinStripPx),
                    Background    = ColMinStrip,
                    Border        = new BorderEdges(new Border(1f, ColBorder)),
                    FlexShrink    = 0,
                    Padding       = new Thickness(Length.Px(0), Length.Px(8)),
                })
                .Children(
                    UI.Text(panel.Title, new StyleSheet
                    {
                        Color    = ColTextDim,
                        FlexGrow = 1,
                        FontSize = Length.Px(12),
                    }),
                    HeaderButton("▲", () => ctx.Dispatch(new DockMinimize { PanelId = panel.PanelId, Minimized = false })),
                    HeaderButton("□", () => ctx.Dispatch(new DockMaximize { PanelId = panel.PanelId }))
                )
                .Build(), key ?? panel.NodeId);
        }

        // ── Drop overlay (for panels being dragged into this one) ─────────────

        private static UINode RenderDropOverlay(string nodeId, DockContextValue ctx)
        {
            UINode Zone(DropZone zone, StyleSheet style) =>
                UI.Box(new PropsBuilder()
                    .Style(style.Merge(new StyleSheet
                    {
                        Position     = Position.Absolute,
                        Background   = zone == DropZone.Center
                            ? new PaperColour(0.3f, 0.8f, 0.5f, 0.22f)
                            : new PaperColour(0.3f, 0.5f, 1f, 0.22f),
                        Border       = new BorderEdges(new Border(1.5f, new PaperColour(0.4f, 0.6f, 1f, 0.7f))),
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

            return UI.Box(
                new StyleSheet
                {
                    Position      = Position.Absolute,
                    Left          = Length.Px(0), Top = Length.Px(0),
                    Width         = Length.Percent(100), Height = Length.Percent(100),
                    ZIndex        = 100,
                    PointerEvents = PointerEvents.None,
                },
                Zone(DropZone.Left,   new StyleSheet { Left = Length.Px(4),   Top = Length.Percent(20), Width = Length.Percent(25), Height = Length.Percent(60), PointerEvents = PointerEvents.Auto }),
                Zone(DropZone.Right,  new StyleSheet { Right = Length.Px(4),  Top = Length.Percent(20), Width = Length.Percent(25), Height = Length.Percent(60), PointerEvents = PointerEvents.Auto }),
                Zone(DropZone.Top,    new StyleSheet { Top = Length.Px(4),    Left = Length.Percent(20), Width = Length.Percent(60), Height = Length.Percent(25), PointerEvents = PointerEvents.Auto }),
                Zone(DropZone.Bottom, new StyleSheet { Bottom = Length.Px(4), Left = Length.Percent(20), Width = Length.Percent(60), Height = Length.Percent(25), PointerEvents = PointerEvents.Auto }),
                Zone(DropZone.Center, new StyleSheet { Left = Length.Percent(30), Top = Length.Percent(30), Width = Length.Percent(40), Height = Length.Percent(40), PointerEvents = PointerEvents.Auto })
            );
        }

        // ── Button helper ─────────────────────────────────────────────────────

        private static UINode HeaderButton(string icon, Action onClick) =>
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
                    Color          = ColTextDim,
                    Margin         = new Thickness(Length.Px(0), Length.Px(1)),
                })
                .HoverStyle(new StyleSheet { Background = ColButtonHov, Color = ColText })
                .OnClick(onClick)
                .Children(UI.Text(icon, new StyleSheet { FontSize = Length.Px(11) }))
                .Build());
    }
}
