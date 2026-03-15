using System.Collections.Generic;
using System.Linq;
using Paper.Core.Context;
using Paper.Core.Hooks;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Generated;

public static partial class AppComponent
{
    public static UINode App(Props props)
    {
        List<string> test = ["test"];
        return UI.Box(
            new PropsBuilder()
                .ClassName("root")
                .Children(
                    UI.Box(
                        new PropsBuilder()
                            .ClassName("section")
                            .Children(
                                new UINode(
                                    "text",
                                    new PropsBuilder()
                                        .ClassName("section-label")
                                        .Text("flexGrow (1, 2, 1)")
                                        .Build()
                                ),
                                UI.Box(
                                    new PropsBuilder()
                                        .ClassName("demo-panel")
                                        .Style(
                                            new StyleSheet
                                            {
                                                RowGap = Length.Px(8),
                                                ColumnGap = Length.Px(8),
                                                Height = Length.Px(56),
                                            }
                                        )
                                        .Children(
                                            UI.Box(
                                                new PropsBuilder()
                                                    .ClassName("demo-item")
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            FlexGrow = 1f,
                                                            Background = new PaperColour(
                                                                0.9372549f,
                                                                0.26666668f,
                                                                0.26666668f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Children(
                                                        new UINode(
                                                            "text",
                                                            new PropsBuilder()
                                                                .ClassName("white-text")
                                                                .Text("1")
                                                                .Build()
                                                        )
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .ClassName("demo-item")
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            FlexGrow = 2f,
                                                            Background = new PaperColour(
                                                                0.9764706f,
                                                                0.4509804f,
                                                                0.08627451f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Children(
                                                        new UINode(
                                                            "text",
                                                            new PropsBuilder()
                                                                .ClassName("white-text")
                                                                .Text("2")
                                                                .Build()
                                                        )
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .ClassName("demo-item")
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            FlexGrow = 1f,
                                                            Background = new PaperColour(
                                                                0.13333334f,
                                                                0.77254903f,
                                                                0.36862746f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Children(
                                                        new UINode(
                                                            "text",
                                                            new PropsBuilder()
                                                                .ClassName("white-text")
                                                                .Text("1")
                                                                .Build()
                                                        )
                                                    )
                                                    .Build()
                                            )
                                        )
                                        .Build()
                                )
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .ClassName("section")
                            .Children(
                                new UINode(
                                    "text",
                                    new PropsBuilder()
                                        .ClassName("section-label")
                                        .Text("justifyContent: space-between")
                                        .Build()
                                ),
                                UI.Box(
                                    new PropsBuilder()
                                        .ClassName("demo-panel")
                                        .Style(
                                            new StyleSheet
                                            {
                                                JustifyContent = JustifyContent.SpaceBetween,
                                                RowGap = Length.Px(8),
                                                ColumnGap = Length.Px(8),
                                                Height = Length.Px(48),
                                            }
                                        )
                                        .Children(
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(60),
                                                            MinWidth = Length.Px(60),
                                                            Height = Length.Px(48),
                                                            Background = new PaperColour(
                                                                0.3882353f,
                                                                0.4f,
                                                                0.94509804f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(60),
                                                            MinWidth = Length.Px(60),
                                                            Height = Length.Px(48),
                                                            Background = new PaperColour(
                                                                0.54509807f,
                                                                0.36078432f,
                                                                0.9647059f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(60),
                                                            MinWidth = Length.Px(60),
                                                            Height = Length.Px(48),
                                                            Background = new PaperColour(
                                                                0.65882355f,
                                                                0.33333334f,
                                                                0.96862745f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            )
                                        )
                                        .Build()
                                )
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .ClassName("section")
                            .Children(
                                new UINode(
                                    "text",
                                    new PropsBuilder()
                                        .ClassName("section-label")
                                        .Text("justifyContent: center")
                                        .Build()
                                ),
                                UI.Box(
                                    new PropsBuilder()
                                        .ClassName("demo-panel")
                                        .Style(
                                            new StyleSheet
                                            {
                                                JustifyContent = JustifyContent.Center,
                                                RowGap = Length.Px(8),
                                                ColumnGap = Length.Px(8),
                                                Height = Length.Px(48),
                                            }
                                        )
                                        .Children(
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(60),
                                                            MinWidth = Length.Px(60),
                                                            Height = Length.Px(48),
                                                            Background = new PaperColour(
                                                                0.9254902f,
                                                                0.28235295f,
                                                                0.6f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(60),
                                                            MinWidth = Length.Px(60),
                                                            Height = Length.Px(48),
                                                            Background = new PaperColour(
                                                                0.95686275f,
                                                                0.24705882f,
                                                                0.36862746f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            )
                                        )
                                        .Build()
                                )
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .ClassName("section")
                            .Children(
                                new UINode(
                                    "text",
                                    new PropsBuilder()
                                        .ClassName("section-label")
                                        .Text("justifyContent: space-around")
                                        .Build()
                                ),
                                UI.Box(
                                    new PropsBuilder()
                                        .ClassName("demo-panel")
                                        .Style(
                                            new StyleSheet
                                            {
                                                JustifyContent = JustifyContent.SpaceAround,
                                                Height = Length.Px(48),
                                            }
                                        )
                                        .Children(
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(50),
                                                            MinWidth = Length.Px(50),
                                                            Height = Length.Px(48),
                                                            Background = new PaperColour(
                                                                0.078431375f,
                                                                0.72156864f,
                                                                0.6509804f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(50),
                                                            MinWidth = Length.Px(50),
                                                            Height = Length.Px(48),
                                                            Background = new PaperColour(
                                                                0.023529412f,
                                                                0.7137255f,
                                                                0.83137256f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            )
                                        )
                                        .Build()
                                )
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .ClassName("section")
                            .Children(
                                new UINode(
                                    "text",
                                    new PropsBuilder()
                                        .ClassName("section-label")
                                        .Text("alignItems: stretch (default)")
                                        .Build()
                                ),
                                UI.Box(
                                    new PropsBuilder()
                                        .ClassName("demo-panel")
                                        .Style(
                                            new StyleSheet
                                            {
                                                AlignItems = AlignItems.Stretch,
                                                RowGap = Length.Px(8),
                                                ColumnGap = Length.Px(8),
                                                Height = Length.Px(64),
                                            }
                                        )
                                        .Children(
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(40),
                                                            Background = new PaperColour(
                                                                0.91764706f,
                                                                0.7019608f,
                                                                0.03137255f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(40),
                                                            Background = new PaperColour(
                                                                0.5176471f,
                                                                0.8f,
                                                                0.08627451f,
                                                                1f
                                                            ),
                                                            AlignSelf = AlignSelf.Center,
                                                            MinHeight = Length.Px(32),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(40),
                                                            Background = new PaperColour(
                                                                0.39607844f,
                                                                0.6392157f,
                                                                0.050980393f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            )
                                        )
                                        .Build()
                                )
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .ClassName("section")
                            .Children(
                                new UINode(
                                    "text",
                                    new PropsBuilder()
                                        .ClassName("section-label")
                                        .Text("alignItems: center + alignSelf: flex-end on middle")
                                        .Build()
                                ),
                                UI.Box(
                                    new PropsBuilder()
                                        .ClassName("demo-panel")
                                        .Style(
                                            new StyleSheet
                                            {
                                                AlignItems = AlignItems.Center,
                                                RowGap = Length.Px(8),
                                                ColumnGap = Length.Px(8),
                                                Height = Length.Px(64),
                                            }
                                        )
                                        .Children(
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(50),
                                                            Height = Length.Px(40),
                                                            Background = new PaperColour(
                                                                0.05490196f,
                                                                0.64705884f,
                                                                0.9137255f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(50),
                                                            Height = Length.Px(24),
                                                            Background = new PaperColour(
                                                                0.21960784f,
                                                                0.7411765f,
                                                                0.972549f,
                                                                1f
                                                            ),
                                                            AlignSelf = AlignSelf.FlexEnd,
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(50),
                                                            Height = Length.Px(40),
                                                            Background = new PaperColour(
                                                                0.007843138f,
                                                                0.5176471f,
                                                                0.78039217f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            )
                                        )
                                        .Build()
                                )
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .ClassName("section")
                            .Children(
                                new UINode(
                                    "text",
                                    new PropsBuilder()
                                        .ClassName("section-label")
                                        .Text("flexWrap: wrap + gap")
                                        .Build()
                                ),
                                UI.Box(
                                    new PropsBuilder()
                                        .ClassName("demo-panel")
                                        .Style(
                                            new StyleSheet
                                            {
                                                FlexWrap = FlexWrap.Wrap,
                                                RowGap = Length.Px(8),
                                                ColumnGap = Length.Px(8),
                                                Padding = new Thickness(8f),
                                                MinHeight = Length.Px(80),
                                            }
                                        )
                                        .Children(
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.9607843f,
                                                                0.61960787f,
                                                                0.043137256f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.8509804f,
                                                                0.46666667f,
                                                                0.023529412f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.7058824f,
                                                                0.3254902f,
                                                                0.03529412f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.57254905f,
                                                                0.2509804f,
                                                                0.05490196f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.47058824f,
                                                                0.20784314f,
                                                                0.05882353f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.9607843f,
                                                                0.61960787f,
                                                                0.043137256f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.8509804f,
                                                                0.46666667f,
                                                                0.023529412f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.7058824f,
                                                                0.3254902f,
                                                                0.03529412f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.57254905f,
                                                                0.2509804f,
                                                                0.05490196f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.47058824f,
                                                                0.20784314f,
                                                                0.05882353f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.9607843f,
                                                                0.61960787f,
                                                                0.043137256f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.8509804f,
                                                                0.46666667f,
                                                                0.023529412f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.7058824f,
                                                                0.3254902f,
                                                                0.03529412f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.57254905f,
                                                                0.2509804f,
                                                                0.05490196f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.47058824f,
                                                                0.20784314f,
                                                                0.05882353f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.9607843f,
                                                                0.61960787f,
                                                                0.043137256f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.8509804f,
                                                                0.46666667f,
                                                                0.023529412f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.7058824f,
                                                                0.3254902f,
                                                                0.03529412f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.57254905f,
                                                                0.2509804f,
                                                                0.05490196f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(70),
                                                            Height = Length.Px(36),
                                                            Background = new PaperColour(
                                                                0.47058824f,
                                                                0.20784314f,
                                                                0.05882353f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            )
                                        )
                                        .Build()
                                )
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .ClassName("section")
                            .Children(
                                new UINode(
                                    "text",
                                    new PropsBuilder()
                                        .ClassName("section-label")
                                        .Text("flexDirection: row-reverse")
                                        .Build()
                                ),
                                UI.Box(
                                    new PropsBuilder()
                                        .ClassName("demo-panel")
                                        .Style(
                                            new StyleSheet
                                            {
                                                FlexDirection = FlexDirection.RowReverse,
                                                RowGap = Length.Px(8),
                                                ColumnGap = Length.Px(8),
                                                Height = Length.Px(44),
                                            }
                                        )
                                        .Children(
                                            UI.Box(
                                                new PropsBuilder()
                                                    .ClassName("demo-item")
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(64),
                                                            Background = new PaperColour(
                                                                0.4862745f,
                                                                0.22745098f,
                                                                0.92941177f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Children(
                                                        new UINode(
                                                            "text",
                                                            new PropsBuilder()
                                                                .ClassName("white-text text-center")
                                                                .Text("A")
                                                                .Build()
                                                        )
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .ClassName("demo-item")
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(64),
                                                            Background = new PaperColour(
                                                                0.42745098f,
                                                                0.15686275f,
                                                                0.8509804f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Children(
                                                        new UINode(
                                                            "text",
                                                            new PropsBuilder()
                                                                .ClassName("white-text text-center")
                                                                .Text("B")
                                                                .Build()
                                                        )
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .ClassName("demo-item")
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Width = Length.Px(64),
                                                            Background = new PaperColour(
                                                                0.35686275f,
                                                                0.12941177f,
                                                                0.7137255f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Children(
                                                        new UINode(
                                                            "text",
                                                            new PropsBuilder()
                                                                .ClassName("white-text text-center")
                                                                .Text("C")
                                                                .Build()
                                                        )
                                                    )
                                                    .Build()
                                            )
                                        )
                                        .Build()
                                )
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .ClassName("section")
                            .Children(
                                new UINode(
                                    "text",
                                    new PropsBuilder()
                                        .ClassName("section-label")
                                        .Text("Lambda JSX — mapping array items to elements")
                                        .Build()
                                ),
                                UI.Box(
                                    new PropsBuilder()
                                        .ClassName("demo-panel")
                                        .Style(
                                            new StyleSheet
                                            {
                                                FlexWrap = FlexWrap.Wrap,
                                                RowGap = Length.Px(6),
                                                ColumnGap = Length.Px(6),
                                                Padding = new Thickness(8f),
                                                MinHeight = Length.Px(40),
                                            }
                                        )
                                        .Children(
                                            UI.Nodes(
                                                new[]
                                                {
                                                    "#ef4444",
                                                    "#f97316",
                                                    "#eab308",
                                                    "#22c55e",
                                                    "#06b6d4",
                                                    "#6366f1",
                                                    "#a855f7",
                                                    "#ec4899",
                                                }.Select(
                                                    (color, i) =>
                                                        UI.Box(
                                                            new PropsBuilder()
                                                                .Style(
                                                                    new StyleSheet
                                                                    {
                                                                        Width = Length.Px(36),
                                                                        Height = Length.Px(36),
                                                                        Background =
                                                                            new PaperColour(color),
                                                                    }
                                                                )
                                                                .Build(),
                                                            $"swatch-{i}"
                                                        )
                                                )
                                            )
                                        )
                                        .Build()
                                )
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .ClassName("section")
                            .Style(new StyleSheet { FlexGrow = 1f, MinHeight = Length.Px(170) })
                            .Children(
                                new UINode(
                                    "text",
                                    new PropsBuilder()
                                        .ClassName("section-label")
                                        .Text("Column: fixed header/footer + flexGrow middle")
                                        .Build()
                                ),
                                UI.Box(
                                    new PropsBuilder()
                                        .ClassName("demo-col-panel")
                                        .Style(
                                            new StyleSheet
                                            {
                                                RowGap = Length.Px(6),
                                                ColumnGap = Length.Px(6),
                                                Height = Length.Px(120),
                                                Padding = new Thickness(8f),
                                                FlexGrow = 1f,
                                                MinHeight = Length.Px(20),
                                            }
                                        )
                                        .Children(
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Height = Length.Px(28),
                                                            Background = new PaperColour(
                                                                0.8627451f,
                                                                0.14901961f,
                                                                0.14901961f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .ClassName("demo-item")
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            FlexGrow = 1f,
                                                            Background = new PaperColour(
                                                                0.14509805f,
                                                                0.3882353f,
                                                                0.92156863f,
                                                                1f
                                                            ),
                                                            MinHeight = Length.Px(20),
                                                        }
                                                    )
                                                    .Build()
                                            ),
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Height = Length.Px(28),
                                                            Background = new PaperColour(
                                                                0.08627451f,
                                                                0.6392157f,
                                                                0.2901961f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Build()
                                            )
                                        )
                                        .Build()
                                )
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .ClassName("section")
                            .Children(
                                UI.Nodes(
                                    new UINode(
                                        "text",
                                        new PropsBuilder()
                                            .ClassName("section-label")
                                            .Text("UI.List — 200 items, virtualized")
                                            .Build()
                                    ),
                                    UI.List(
                                        Enumerable.Range(1, 200).ToArray(),
                                        itemHeight: 36,
                                        containerH: 180,
                                        renderItem: (n, i) =>
                                            UI.Box(
                                                new PropsBuilder()
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Height = Length.Px(36),
                                                            Padding = new Thickness(8f),
                                                            Background = new PaperColour(
                                                                0.101960786f,
                                                                0.101960786f,
                                                                0.13333334f,
                                                                1f
                                                            ),
                                                            BorderBottom = new Border(
                                                                1f,
                                                                new PaperColour(
                                                                    0.16470589f,
                                                                    0.16470589f,
                                                                    0.20784314f,
                                                                    1f
                                                                )
                                                            ),
                                                        }
                                                    )
                                                    .Children(
                                                        new UINode(
                                                            "text",
                                                            new PropsBuilder()
                                                                .Style(
                                                                    new StyleSheet
                                                                    {
                                                                        Color = new PaperColour(
                                                                            0.627451f,
                                                                            0.627451f,
                                                                            0.72156864f,
                                                                            1f
                                                                        ),
                                                                    }
                                                                )
                                                                .Text($"{$"Item {n}"}")
                                                                .Build()
                                                        )
                                                    )
                                                    .Build(),
                                                i.ToString()
                                            )
                                    )
                                )
                            )
                            .Build()
                    )
                )
                .Build()
        );
    }
}
