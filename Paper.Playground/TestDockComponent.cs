using Paper.Core.Dock;
using Paper.Core.Hooks;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Playground;

public static class TestDockComponent
{
    public static UINode TestDock(Props props)
    {
        var panels = new PanelRegistration[]
        {
            new("hierarchy", "Hierarchy",
                () => HierarchyPanel(),
                Constraints: new DockPanelConstraints(AllowClose: true, AllowFloat: true, AllowMinimize: true, AllowMaximize: true)),

            new("scene", "Scene",
                () => ScenePanel(),
                DefaultTargetPanelId: "hierarchy",
                DefaultZone:          DropZone.Right,
                Constraints: new DockPanelConstraints(AllowClose: true, AllowFloat: true, AllowMinimize: false, AllowMaximize: true)),

            new("inspector", "Inspector",
                () => InspectorPanel(),
                DefaultTargetPanelId: "scene",
                DefaultZone:          DropZone.Right,
                Constraints: new DockPanelConstraints(AllowClose: true, AllowFloat: true, AllowMinimize: true, AllowMaximize: true,
                    MinWidth: 180)),

            new("console", "Console",
                () => ConsolePanel(),
                DefaultTargetPanelId: "hierarchy",
                DefaultZone:          DropZone.Bottom,
                Constraints: new DockPanelConstraints(AllowClose: true, AllowFloat: true, AllowMinimize: true, AllowMaximize: false,
                    MinHeight: 80)),
        };

        return DockContext.Root(panels, theme: DockTheme.Dark, children:
            UI.Component(PanelManagerShim, Props.Empty));
    }

    // ── Menu bar that lets the user re-open closed panels ─────────────────────

    private static readonly string[] _allPanelIds = { "hierarchy", "scene", "inspector", "console" };
    private static readonly Dictionary<string, string> _panelTitles = new()
    {
        { "hierarchy", "Hierarchy" },
        { "scene",     "Scene"     },
        { "inspector", "Inspector" },
        { "console",   "Console"   },
    };

    private static UINode PanelManagerShim(Props _)
    {
        var ctx = Hooks.UseContext(DockContext.Context);

        return UI.Box(new StyleSheet
        {
            Display       = Display.Flex,
            FlexDirection = FlexDirection.Column,
            FlexGrow      = 1,
            Overflow      = Overflow.Hidden,
        },
            RenderMenuBar(ctx),
            Paper.Core.Dock.DockContainer.Render(new StyleSheet { FlexGrow = 1 })
        );
    }

    private static UINode RenderMenuBar(DockContextValue? ctx)
    {
        if (ctx == null || ctx.HiddenPanels.Count == 0) return UI.Box(StyleSheet.Empty);
        var theme = ctx.Theme;

        var reopenButtons = _allPanelIds
            .Where(id => ctx.IsPanelHidden(id))
            .Select(id =>
                UI.Box(new PropsBuilder()
                    .Style(new StyleSheet
                    {
                        Padding      = new Thickness(Length.Px(3), Length.Px(10)),
                        Background   = theme.Header,
                        Border       = new BorderEdges(new Border(1f, theme.Border)),
                        BorderRadius = 3,
                        Cursor       = Cursor.Pointer,
                        Margin       = new Thickness(Length.Px(0), Length.Px(4)),
                    })
                    .HoverStyle(new StyleSheet { Background = theme.TabHover })
                    .OnClick(() => ctx.ShowPanel(id))
                    .Children(UI.Text(_panelTitles[id], new StyleSheet { Color = theme.Text, FontSize = Length.Px(11) }))
                    .Build(),
                    key: id))
            .ToArray();

        return UI.Box(new StyleSheet
        {
            Display       = Display.Flex,
            FlexDirection = FlexDirection.Row,
            AlignItems    = AlignItems.Center,
            FlexShrink    = 0,
            Padding       = new Thickness(Length.Px(4), Length.Px(8)),
            Background    = theme.Header,
            BorderBottom  = new Border(1f, theme.Border),
        },
            UI.Text("View: ", new StyleSheet
            {
                Color  = theme.TextDim,
                FontSize = Length.Px(11),
                Margin = new Thickness(Length.Px(0), Length.Px(8)),
            }),
            UI.Box(new StyleSheet { Display = Display.Flex, FlexDirection = FlexDirection.Row, FlexGrow = 1 },
                reopenButtons)
        );
    }

    // ── Panel content factories ───────────────────────────────────────────────

    private static UINode HierarchyPanel() =>
        UI.Box(new StyleSheet { FlexGrow = 1, Padding = new Thickness(Length.Px(8)) },
            UI.Text("Hierarchy", new StyleSheet { FontSize = Length.Px(13), Color = new PaperColour("#c8c8e0") }),
            UI.Text("  └ Root", new StyleSheet { Color = new PaperColour("#9090c0") }),
            UI.Text("      └ Camera", new StyleSheet { Color = new PaperColour("#9090c0") }),
            UI.Text("      └ DirectionalLight", new StyleSheet { Color = new PaperColour("#9090c0") }),
            UI.Text("      └ Cube", new StyleSheet { Color = new PaperColour("#9090c0") }));

    private static UINode ScenePanel() =>
        UI.Box(new StyleSheet
        {
            FlexGrow       = 1,
            Display        = Display.Flex,
            AlignItems     = AlignItems.Center,
            JustifyContent = JustifyContent.Center,
            Background     = new PaperColour("#0d0d1a"),
        },
            UI.Text("[ Scene View ]", new StyleSheet { Color = new PaperColour("#5050a0") }));

    private static UINode InspectorPanel() =>
        UI.Box(new StyleSheet { FlexGrow = 1, Padding = new Thickness(Length.Px(8)) },
            UI.Text("Transform", new StyleSheet { FontSize = Length.Px(13), Color = new PaperColour("#c8c8e0") }),
            UI.Text("  Position:  0, 0, 0", new StyleSheet { Color = new PaperColour("#9090c0") }),
            UI.Text("  Rotation:  0, 0, 0", new StyleSheet { Color = new PaperColour("#9090c0") }),
            UI.Text("  Scale:     1, 1, 1", new StyleSheet { Color = new PaperColour("#9090c0") }));

    private static UINode ConsolePanel() =>
        UI.Box(new StyleSheet { FlexGrow = 1, Padding = new Thickness(Length.Px(8)) },
            UI.Text("[Info]  Application started.", new StyleSheet { Color = new PaperColour("#80c080") }),
            UI.Text("[Info]  Scene loaded.", new StyleSheet { Color = new PaperColour("#80c080") }),
            UI.Text("[Warn]  Missing texture on Cube.", new StyleSheet { Color = new PaperColour("#c0c040") }));
}
