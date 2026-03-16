using System.Collections.Generic;
using System.Linq;
using Paper.Core.Components;
using Paper.Core.Context;
using Paper.Core.Hooks;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Generated;

public static partial class DemoAppComponent
{
    public static UINode DemoApp(Props props)
    {
        // Context that carries accent colour to any descendant without prop drilling
        var AccentContext = Hooks.UseStable(() => PaperContext.Create("#58a6ff"));
        Func<Props, UINode> ThemedBadge = Hooks.UseStable<Func<Props, UINode>>(() =>
            (Props props) =>
            {
                var accent = Hooks.UseContext(AccentContext);
                return UI.Box(
                    new PropsBuilder()
                        .Style(
                            new StyleSheet
                            {
                                Background = new PaperColour(accent),
                                Padding = new Thickness(Length.Px(4), Length.Px(10)),
                                BorderRadius = 12f,
                            }
                        )
                        .Children(
                            new UINode(
                                "text",
                                new PropsBuilder()
                                    .Style(
                                        new StyleSheet
                                        {
                                            FontSize = Length.Px(12),
                                            Color = new PaperColour(
                                                0.050980393f,
                                                0.06666667f,
                                                0.09019608f,
                                                1f
                                            ),
                                        }
                                    )
                                    .Text(props.Get<string>("label") ?? "")
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
        );
        var (count, setCount, updateCount) = Hooks.UseState(0);
        var (name, setName, _) = Hooks.UseState("Paper");
        var (clicks, setClicks, updateClicks) = Hooks.UseState(0);
        var (usePurple, setUsePurple, _) = Hooks.UseState(false);
        var accent = usePurple ? "#bc8cff" : "#58a6ff";
        // UseReducer demo: todo list
        var (todos, dispatch) = Hooks.UseReducer(
            (List<string> state, string action) =>
            {
                if (action == "add")
                    return state.Concat(new[] { $"Todo #{state.Count + 1}" }).ToList();
                if (action == "clear")
                    return new List<string>();
                return state;
            },
            new List<string>()
        );
        return UI.Box(
            new PropsBuilder()
                .Style(
                    new StyleSheet
                    {
                        Display = Display.Flex,
                        FlexDirection = FlexDirection.Column,
                        AlignItems = AlignItems.Center,
                        JustifyContent = JustifyContent.FlexStart,
                        Height = Length.Percent(100),
                        Background = new PaperColour(0.050980393f, 0.06666667f, 0.09019608f, 1f),
                        Padding = new Thickness(24f),
                        RowGap = Length.Px(24),
                        ColumnGap = Length.Px(24),
                    }
                )
                .Children(
                    new UINode(
                        "text",
                        new PropsBuilder()
                            .Style(
                                new StyleSheet
                                {
                                    FontSize = Length.Px(28),
                                    Color = new PaperColour(0.34509805f, 0.6509804f, 1f, 1f),
                                    PaddingBottom = Length.Px(8),
                                }
                            )
                            .Text("Paper Framework Demo")
                            .Build()
                    ),
                    new UINode(
                        "text",
                        new PropsBuilder()
                            .Style(
                                new StyleSheet
                                {
                                    FontSize = Length.Px(14),
                                    Color = new PaperColour(
                                        0.54509807f,
                                        0.5803922f,
                                        0.61960787f,
                                        1f
                                    ),
                                }
                            )
                            .Text($"Welcome, {(name)}. Clicks: {(clicks)}")
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .Style(
                                new StyleSheet
                                {
                                    Display = Display.Flex,
                                    FlexDirection = FlexDirection.Column,
                                    Background = new PaperColour(
                                        0.08627451f,
                                        0.105882354f,
                                        0.13333334f,
                                        1f
                                    ),
                                    Padding = new Thickness(20f),
                                    BorderRadius = 8f,
                                    RowGap = Length.Px(12),
                                    ColumnGap = Length.Px(12),
                                }
                            )
                            .Children(
                                new UINode(
                                    "text",
                                    new PropsBuilder()
                                        .Style(
                                            new StyleSheet
                                            {
                                                FontSize = Length.Px(18),
                                                Color = new PaperColour(
                                                    0.7882353f,
                                                    0.81960785f,
                                                    0.8509804f,
                                                    1f
                                                ),
                                            }
                                        )
                                        .Text($"Counter: {(count)}")
                                        .Build()
                                ),
                                UI.Box(
                                    new PropsBuilder()
                                        .Style(
                                            new StyleSheet
                                            {
                                                Display = Display.Flex,
                                                FlexDirection = FlexDirection.Row,
                                                RowGap = Length.Px(12),
                                                ColumnGap = Length.Px(12),
                                            }
                                        )
                                        .Children(
                                            new UINode(
                                                "button",
                                                new PropsBuilder()
                                                    .OnClick(() =>
                                                    {
                                                        updateCount(prev => prev + 1);
                                                        updateClicks(prev => prev + 1);
                                                    })
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Background = new PaperColour(
                                                                0.13725491f,
                                                                0.5254902f,
                                                                0.21176471f,
                                                                1f
                                                            ),
                                                            Color = new PaperColour(1f, 1f, 1f, 1f),
                                                            Padding = new Thickness(
                                                                Length.Px(8),
                                                                Length.Px(16)
                                                            ),
                                                            BorderRadius = 6f,
                                                            FontSize = Length.Px(14),
                                                            Transition = "background 0.15s",
                                                        }
                                                    )
                                                    .Set(
                                                        "hoverStyle",
                                                        new StyleSheet
                                                        {
                                                            Background = new PaperColour(
                                                                0.18039216f,
                                                                0.627451f,
                                                                0.2627451f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Text("+1")
                                                    .Children(
                                                        new UINode(
                                                            "text",
                                                            new PropsBuilder().Text("+1").Build()
                                                        )
                                                    )
                                                    .Build()
                                            ),
                                            new UINode(
                                                "button",
                                                new PropsBuilder()
                                                    .OnClick(() =>
                                                    {
                                                        updateCount(prev => prev - 1);
                                                        updateClicks(prev => prev + 1);
                                                    })
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Background = new PaperColour(
                                                                0.85490197f,
                                                                0.21176471f,
                                                                0.2f,
                                                                1f
                                                            ),
                                                            Color = new PaperColour(1f, 1f, 1f, 1f),
                                                            Padding = new Thickness(
                                                                Length.Px(8),
                                                                Length.Px(16)
                                                            ),
                                                            BorderRadius = 6f,
                                                            FontSize = Length.Px(14),
                                                            Transition = "background 0.15s",
                                                        }
                                                    )
                                                    .Set(
                                                        "hoverStyle",
                                                        new StyleSheet
                                                        {
                                                            Background = new PaperColour(
                                                                0.972549f,
                                                                0.31764707f,
                                                                0.28627452f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Text("-1")
                                                    .Children(
                                                        new UINode(
                                                            "text",
                                                            new PropsBuilder().Text("-1").Build()
                                                        )
                                                    )
                                                    .Build()
                                            ),
                                            new UINode(
                                                "button",
                                                new PropsBuilder()
                                                    .OnClick(() =>
                                                    {
                                                        setCount(0);
                                                        updateClicks(prev => prev + 1);
                                                    })
                                                    .Style(
                                                        new StyleSheet
                                                        {
                                                            Background = new PaperColour(
                                                                0.1882353f,
                                                                0.21176471f,
                                                                0.23921569f,
                                                                1f
                                                            ),
                                                            Color = new PaperColour(1f, 1f, 1f, 1f),
                                                            Padding = new Thickness(
                                                                Length.Px(8),
                                                                Length.Px(16)
                                                            ),
                                                            BorderRadius = 6f,
                                                            FontSize = Length.Px(14),
                                                            Transition = "background 0.15s",
                                                        }
                                                    )
                                                    .Set(
                                                        "hoverStyle",
                                                        new StyleSheet
                                                        {
                                                            Background = new PaperColour(
                                                                0.28235295f,
                                                                0.30980393f,
                                                                0.34509805f,
                                                                1f
                                                            ),
                                                        }
                                                    )
                                                    .Text("Reset")
                                                    .Children(
                                                        new UINode(
                                                            "text",
                                                            new PropsBuilder().Text("Reset").Build()
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
                            .Style(
                                new StyleSheet
                                {
                                    Display = Display.Flex,
                                    FlexDirection = FlexDirection.Column,
                                    Background = new PaperColour(
                                        0.08627451f,
                                        0.105882354f,
                                        0.13333334f,
                                        1f
                                    ),
                                    Padding = new Thickness(20f),
                                    BorderRadius = 8f,
                                    RowGap = Length.Px(8),
                                    ColumnGap = Length.Px(8),
                                    Width = Length.Percent(100),
                                }
                            )
                            .Children(
                                UI.Nodes(
                                    new UINode(
                                        "text",
                                        new PropsBuilder()
                                            .Style(
                                                new StyleSheet
                                                {
                                                    FontSize = Length.Px(16),
                                                    Color = new PaperColour(
                                                        0.7882353f,
                                                        0.81960785f,
                                                        0.8509804f,
                                                        1f
                                                    ),
                                                }
                                            )
                                            .Text($"UseReducer — Todos ({(todos.Count)})")
                                            .Build()
                                    ),
                                    todos
                                        .Select(
                                            (t, i) =>
                                                UI.Text(
                                                    t,
                                                    new StyleSheet
                                                    {
                                                        Color = new PaperColour(
                                                            0.55f,
                                                            0.65f,
                                                            0.75f,
                                                            1f
                                                        ),
                                                        FontSize = Length.Px(13),
                                                    },
                                                    $"t{i}"
                                                )
                                        )
                                        .ToArray(),
                                    UI.Box(
                                        new PropsBuilder()
                                            .Style(
                                                new StyleSheet
                                                {
                                                    Display = Display.Flex,
                                                    FlexDirection = FlexDirection.Row,
                                                    RowGap = Length.Px(8),
                                                    ColumnGap = Length.Px(8),
                                                }
                                            )
                                            .Children(
                                                new UINode(
                                                    "button",
                                                    new PropsBuilder()
                                                        .OnClick(() =>
                                                        {
                                                            dispatch("add");
                                                        })
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Background = new PaperColour(
                                                                    0.12156863f,
                                                                    0.43529412f,
                                                                    0.92156863f,
                                                                    1f
                                                                ),
                                                                Color = new PaperColour(
                                                                    1f,
                                                                    1f,
                                                                    1f,
                                                                    1f
                                                                ),
                                                                Padding = new Thickness(
                                                                    Length.Px(6),
                                                                    Length.Px(14)
                                                                ),
                                                                BorderRadius = 5f,
                                                                FontSize = Length.Px(13),
                                                                Transition = "background 0.15s",
                                                            }
                                                        )
                                                        .Set(
                                                            "hoverStyle",
                                                            new StyleSheet
                                                            {
                                                                Background = new PaperColour(
                                                                    0.21960784f,
                                                                    0.54509807f,
                                                                    0.99215686f,
                                                                    1f
                                                                ),
                                                            }
                                                        )
                                                        .Text("Add Todo")
                                                        .Children(
                                                            new UINode(
                                                                "text",
                                                                new PropsBuilder()
                                                                    .Text("Add Todo")
                                                                    .Build()
                                                            )
                                                        )
                                                        .Build()
                                                ),
                                                new UINode(
                                                    "button",
                                                    new PropsBuilder()
                                                        .OnClick(() =>
                                                        {
                                                            dispatch("clear");
                                                        })
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Background = new PaperColour(
                                                                    0.1882353f,
                                                                    0.21176471f,
                                                                    0.23921569f,
                                                                    1f
                                                                ),
                                                                Color = new PaperColour(
                                                                    0.54509807f,
                                                                    0.5803922f,
                                                                    0.61960787f,
                                                                    1f
                                                                ),
                                                                Padding = new Thickness(
                                                                    Length.Px(6),
                                                                    Length.Px(14)
                                                                ),
                                                                BorderRadius = 5f,
                                                                FontSize = Length.Px(13),
                                                                Transition = "background 0.15s",
                                                            }
                                                        )
                                                        .Set(
                                                            "hoverStyle",
                                                            new StyleSheet
                                                            {
                                                                Background = new PaperColour(
                                                                    0.28235295f,
                                                                    0.30980393f,
                                                                    0.34509805f,
                                                                    1f
                                                                ),
                                                                Color = new PaperColour(
                                                                    1f,
                                                                    1f,
                                                                    1f,
                                                                    1f
                                                                ),
                                                            }
                                                        )
                                                        .Text("Clear")
                                                        .Children(
                                                            new UINode(
                                                                "text",
                                                                new PropsBuilder()
                                                                    .Text("Clear")
                                                                    .Build()
                                                            )
                                                        )
                                                        .Build()
                                                )
                                            )
                                            .Build()
                                    )
                                )
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .Style(
                                new StyleSheet
                                {
                                    Display = Display.Flex,
                                    FlexDirection = FlexDirection.Column,
                                    Background = new PaperColour(
                                        0.08627451f,
                                        0.105882354f,
                                        0.13333334f,
                                        1f
                                    ),
                                    Padding = new Thickness(20f),
                                    BorderRadius = 8f,
                                    RowGap = Length.Px(12),
                                    ColumnGap = Length.Px(12),
                                    Width = Length.Percent(100),
                                }
                            )
                            .Children(
                                UI.Nodes(
                                    new UINode(
                                        "text",
                                        new PropsBuilder()
                                            .Style(
                                                new StyleSheet
                                                {
                                                    FontSize = Length.Px(16),
                                                    Color = new PaperColour(
                                                        0.7882353f,
                                                        0.81960785f,
                                                        0.8509804f,
                                                        1f
                                                    ),
                                                }
                                            )
                                            .Text("UseContext — Accent Theme")
                                            .Build()
                                    ),
                                    new UINode(
                                        "text",
                                        new PropsBuilder()
                                            .Style(
                                                new StyleSheet
                                                {
                                                    FontSize = Length.Px(12),
                                                    Color = new PaperColour(
                                                        0.54509807f,
                                                        0.5803922f,
                                                        0.61960787f,
                                                        1f
                                                    ),
                                                }
                                            )
                                            .Text(
                                                "Badge reads accent from context — no props passed"
                                            )
                                            .Build()
                                    ),
                                    AccentContext.Provider(
                                        accent,
                                        UI.Component(
                                            ThemedBadge,
                                            new PropsBuilder().Set("label", "Hello Context").Build()
                                        ),
                                        UI.Component(
                                            ThemedBadge,
                                            new PropsBuilder().Set("label", accent).Build()
                                        )
                                    ),
                                    new UINode(
                                        "button",
                                        new PropsBuilder()
                                            .OnClick(() =>
                                            {
                                                setUsePurple(!usePurple);
                                            })
                                            .Style(
                                                new StyleSheet
                                                {
                                                    Background = new PaperColour(
                                                        0.1882353f,
                                                        0.21176471f,
                                                        0.23921569f,
                                                        1f
                                                    ),
                                                    Color = new PaperColour(1f, 1f, 1f, 1f),
                                                    Padding = new Thickness(
                                                        Length.Px(6),
                                                        Length.Px(14)
                                                    ),
                                                    BorderRadius = 5f,
                                                    FontSize = Length.Px(13),
                                                    Transition = "background 0.15s",
                                                }
                                            )
                                            .Set(
                                                "hoverStyle",
                                                new StyleSheet
                                                {
                                                    Background = new PaperColour(
                                                        0.28235295f,
                                                        0.30980393f,
                                                        0.34509805f,
                                                        1f
                                                    ),
                                                }
                                            )
                                            .Text(
                                                string.Concat(
                                                    "Toggle Accent (",
                                                    ((usePurple ? "Purple" : "Blue"))?.ToString()
                                                        ?? "",
                                                    ")"
                                                )
                                            )
                                            .Children(
                                                UI.Nodes(
                                                    new UINode(
                                                        "text",
                                                        new PropsBuilder()
                                                            .Text("Toggle Accent (")
                                                            .Build()
                                                    ),
                                                    (usePurple ? "Purple" : "Blue"),
                                                    new UINode(
                                                        "text",
                                                        new PropsBuilder().Text(")").Build()
                                                    )
                                                )
                                            )
                                            .Build()
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
