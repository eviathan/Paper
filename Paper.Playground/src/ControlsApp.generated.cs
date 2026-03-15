using System.Collections.Generic;
using System.Linq;
using Paper.Core.Context;
using Paper.Core.Hooks;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Generated;

public static partial class ControlsAppComponent
{
    public static UINode ControlsApp(Props props)
    {
        var (check1, setCheck1, _) = Hooks.UseState(false);
        var (check2, setCheck2, _) = Hooks.UseState(true);
        var (inputVal, setInputVal, _) = Hooks.UseState("");
        var (textareaVal, setTextareaVal, _) = Hooks.UseState("Line 1\nLine 2");
        var (radioVal, setRadioVal, _) = Hooks.UseState("two");
        var (selectVal, setSelectVal, _) = Hooks.UseState("two");
        var options = new (string, string)[] { ("one", "One"), ("two", "Two"), ("three", "Three") };
        var listItems = new string[] { "Apple", "Banana", "Cherry" };
        var listNodes = UI.Map(
            listItems,
            x => x,
            x =>
                UI.Box(
                    UI.Text(
                        x,
                        new StyleSheet
                        {
                            Padding = new Thickness(Length.Px(6)),
                            Background = new PaperColour(0.2f, 0.2f, 0.25f, 1f),
                            BorderRadius = 4,
                        }
                    )
                )
        );
        return UI.Scroll(
            new StyleSheet
            {
                Display = Display.Flex,
                FlexDirection = FlexDirection.Column,
                Height = Length.Percent(100),
                Padding = new Thickness(24f),
                RowGap = Length.Px(24),
                ColumnGap = Length.Px(24),
                Background = new PaperColour(0.11764706f, 0.16078432f, 0.23137255f, 1f),
                Overflow = Overflow.Scroll,
            },
            UI.Box(
                new PropsBuilder()
                    .Style(
                        new StyleSheet
                        {
                            Display = Display.Flex,
                            FlexDirection = FlexDirection.Column,
                            RowGap = Length.Px(6),
                            ColumnGap = Length.Px(6),
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
                                            0.5803922f,
                                            0.6392157f,
                                            0.72156864f,
                                            1f
                                        ),
                                        FontSize = Length.Px(16),
                                    }
                                )
                                .Text("Image")
                                .Build()
                        ),
                        UI.Box(
                            new PropsBuilder()
                                .Style(
                                    new StyleSheet
                                    {
                                        Display = Display.Flex,
                                        FlexDirection = FlexDirection.Column,
                                        RowGap = Length.Px(8),
                                        ColumnGap = Length.Px(8),
                                        Padding = new Thickness(12f),
                                        Background = new PaperColour(
                                            0.2f,
                                            0.25490198f,
                                            0.33333334f,
                                            1f
                                        ),
                                        BorderRadius = 8f,
                                    }
                                )
                                .Children(
                                    UI.Image(
                                        "Assets/test.png",
                                        new StyleSheet
                                        {
                                            Width = Length.Px(100),
                                            Height = Length.Px(80),
                                        }
                                    ),
                                    new UINode(
                                        "text",
                                        new PropsBuilder()
                                            .Style(
                                                new StyleSheet
                                                {
                                                    Color = new PaperColour(
                                                        0.5803922f,
                                                        0.6392157f,
                                                        0.72156864f,
                                                        1f
                                                    ),
                                                    FontSize = Length.Px(12),
                                                }
                                            )
                                            .Text("Placeholder if Assets/test.png missing")
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
                            RowGap = Length.Px(6),
                            ColumnGap = Length.Px(6),
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
                                            0.5803922f,
                                            0.6392157f,
                                            0.72156864f,
                                            1f
                                        ),
                                        FontSize = Length.Px(16),
                                    }
                                )
                                .Text("Checkbox")
                                .Build()
                        ),
                        UI.Box(
                            new PropsBuilder()
                                .Style(
                                    new StyleSheet
                                    {
                                        Display = Display.Flex,
                                        FlexDirection = FlexDirection.Column,
                                        RowGap = Length.Px(8),
                                        ColumnGap = Length.Px(8),
                                        Padding = new Thickness(12f),
                                        Background = new PaperColour(
                                            0.2f,
                                            0.25490198f,
                                            0.33333334f,
                                            1f
                                        ),
                                        BorderRadius = 8f,
                                    }
                                )
                                .Children(
                                    UI.Checkbox(
                                        check1,
                                        (bool b) => setCheck1(b),
                                        "Option A",
                                        StyleSheet.Empty
                                    ),
                                    UI.Checkbox(
                                        check2,
                                        (bool b) => setCheck2(b),
                                        "Option B",
                                        StyleSheet.Empty
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
                            RowGap = Length.Px(6),
                            ColumnGap = Length.Px(6),
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
                                            0.5803922f,
                                            0.6392157f,
                                            0.72156864f,
                                            1f
                                        ),
                                        FontSize = Length.Px(16),
                                    }
                                )
                                .Text("RadioGroup")
                                .Build()
                        ),
                        UI.Box(
                            new PropsBuilder()
                                .Style(
                                    new StyleSheet
                                    {
                                        Display = Display.Flex,
                                        FlexDirection = FlexDirection.Column,
                                        RowGap = Length.Px(8),
                                        ColumnGap = Length.Px(8),
                                        Padding = new Thickness(12f),
                                        Background = new PaperColour(
                                            0.2f,
                                            0.25490198f,
                                            0.33333334f,
                                            1f
                                        ),
                                        BorderRadius = 8f,
                                    }
                                )
                                .Children(
                                    UI.RadioGroup(options, radioVal, setRadioVal, StyleSheet.Empty)
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
                            RowGap = Length.Px(6),
                            ColumnGap = Length.Px(6),
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
                                            0.5803922f,
                                            0.6392157f,
                                            0.72156864f,
                                            1f
                                        ),
                                        FontSize = Length.Px(16),
                                    }
                                )
                                .Text("Text input (single line)")
                                .Build()
                        ),
                        UI.Box(
                            new PropsBuilder()
                                .Style(
                                    new StyleSheet
                                    {
                                        Display = Display.Flex,
                                        FlexDirection = FlexDirection.Column,
                                        RowGap = Length.Px(8),
                                        ColumnGap = Length.Px(8),
                                        Padding = new Thickness(12f),
                                        Background = new PaperColour(
                                            0.2f,
                                            0.25490198f,
                                            0.33333334f,
                                            1f
                                        ),
                                        BorderRadius = 8f,
                                    }
                                )
                                .Children(
                                    UI.Input(
                                        inputVal,
                                        setInputVal,
                                        new StyleSheet { Width = Length.Px(200) }
                                    ),
                                    new UINode(
                                        "text",
                                        new PropsBuilder()
                                            .Style(
                                                new StyleSheet
                                                {
                                                    Color = new PaperColour(
                                                        0.39215687f,
                                                        0.45490196f,
                                                        0.54509807f,
                                                        1f
                                                    ),
                                                    FontSize = Length.Px(12),
                                                }
                                            )
                                            .Text("Click here then type")
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
                            RowGap = Length.Px(6),
                            ColumnGap = Length.Px(6),
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
                                            0.5803922f,
                                            0.6392157f,
                                            0.72156864f,
                                            1f
                                        ),
                                        FontSize = Length.Px(16),
                                    }
                                )
                                .Text("Textarea (multi-line)")
                                .Build()
                        ),
                        UI.Box(
                            new PropsBuilder()
                                .Style(
                                    new StyleSheet
                                    {
                                        Display = Display.Flex,
                                        FlexDirection = FlexDirection.Column,
                                        RowGap = Length.Px(8),
                                        ColumnGap = Length.Px(8),
                                        Padding = new Thickness(12f),
                                        Background = new PaperColour(
                                            0.2f,
                                            0.25490198f,
                                            0.33333334f,
                                            1f
                                        ),
                                        BorderRadius = 8f,
                                    }
                                )
                                .Children(
                                    UI.Textarea(
                                        textareaVal,
                                        setTextareaVal,
                                        3,
                                        new StyleSheet
                                        {
                                            Width = Length.Px(200),
                                            MinHeight = Length.Px(60),
                                        }
                                    ),
                                    new UINode(
                                        "text",
                                        new PropsBuilder()
                                            .Style(
                                                new StyleSheet
                                                {
                                                    Color = new PaperColour(
                                                        0.39215687f,
                                                        0.45490196f,
                                                        0.54509807f,
                                                        1f
                                                    ),
                                                    FontSize = Length.Px(12),
                                                }
                                            )
                                            .Text("Click here then type (Enter for new line)")
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
                            RowGap = Length.Px(6),
                            ColumnGap = Length.Px(6),
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
                                            0.5803922f,
                                            0.6392157f,
                                            0.72156864f,
                                            1f
                                        ),
                                        FontSize = Length.Px(16),
                                    }
                                )
                                .Text("Table")
                                .Build()
                        ),
                        UI.Box(
                            new PropsBuilder()
                                .Style(
                                    new StyleSheet
                                    {
                                        Display = Display.Flex,
                                        FlexDirection = FlexDirection.Column,
                                        RowGap = Length.Px(8),
                                        ColumnGap = Length.Px(8),
                                        Padding = new Thickness(12f),
                                        Background = new PaperColour(
                                            0.2f,
                                            0.25490198f,
                                            0.33333334f,
                                            1f
                                        ),
                                        BorderRadius = 8f,
                                    }
                                )
                                .Children(
                                    UI.Table(
                                        StyleSheet.Empty,
                                        null,
                                        UI.TableRow(
                                            StyleSheet.Empty,
                                            null,
                                            UI.TableCell(
                                                StyleSheet.Empty,
                                                null,
                                                new UINode(
                                                    "text",
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Color = new PaperColour(
                                                                    1f,
                                                                    1f,
                                                                    1f,
                                                                    1f
                                                                ),
                                                            }
                                                        )
                                                        .Text("A1")
                                                        .Build()
                                                )
                                            ),
                                            UI.TableCell(
                                                StyleSheet.Empty,
                                                null,
                                                new UINode(
                                                    "text",
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Color = new PaperColour(
                                                                    1f,
                                                                    1f,
                                                                    1f,
                                                                    1f
                                                                ),
                                                            }
                                                        )
                                                        .Text("A2")
                                                        .Build()
                                                )
                                            )
                                        ),
                                        UI.TableRow(
                                            StyleSheet.Empty,
                                            null,
                                            UI.TableCell(
                                                StyleSheet.Empty,
                                                null,
                                                new UINode(
                                                    "text",
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Color = new PaperColour(
                                                                    1f,
                                                                    1f,
                                                                    1f,
                                                                    1f
                                                                ),
                                                            }
                                                        )
                                                        .Text("B1")
                                                        .Build()
                                                )
                                            ),
                                            UI.TableCell(
                                                StyleSheet.Empty,
                                                null,
                                                new UINode(
                                                    "text",
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Color = new PaperColour(
                                                                    1f,
                                                                    1f,
                                                                    1f,
                                                                    1f
                                                                ),
                                                            }
                                                        )
                                                        .Text("B2")
                                                        .Build()
                                                )
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
                    .Style(
                        new StyleSheet
                        {
                            Display = Display.Flex,
                            FlexDirection = FlexDirection.Column,
                            RowGap = Length.Px(6),
                            ColumnGap = Length.Px(6),
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
                                            0.5803922f,
                                            0.6392157f,
                                            0.72156864f,
                                            1f
                                        ),
                                        FontSize = Length.Px(16),
                                    }
                                )
                                .Text("List (Scroll + Map)")
                                .Build()
                        ),
                        UI.Box(
                            new PropsBuilder()
                                .Style(
                                    new StyleSheet
                                    {
                                        Display = Display.Flex,
                                        FlexDirection = FlexDirection.Column,
                                        RowGap = Length.Px(8),
                                        ColumnGap = Length.Px(8),
                                        Padding = new Thickness(12f),
                                        Background = new PaperColour(
                                            0.2f,
                                            0.25490198f,
                                            0.33333334f,
                                            1f
                                        ),
                                        BorderRadius = 8f,
                                    }
                                )
                                .Children(
                                    UI.Scroll(
                                        new StyleSheet
                                        {
                                            MaxHeight = Length.Px(120),
                                            Overflow = Overflow.Scroll,
                                        },
                                        UI.Box(
                                            new PropsBuilder()
                                                .Style(
                                                    new StyleSheet
                                                    {
                                                        Display = Display.Flex,
                                                        FlexDirection = FlexDirection.Column,
                                                        RowGap = Length.Px(4),
                                                        ColumnGap = Length.Px(4),
                                                    }
                                                )
                                                .Children(UI.Nodes(listNodes))
                                                .Build()
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
                    .Style(
                        new StyleSheet
                        {
                            Display = Display.Flex,
                            FlexDirection = FlexDirection.Column,
                            RowGap = Length.Px(6),
                            ColumnGap = Length.Px(6),
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
                                            0.5803922f,
                                            0.6392157f,
                                            0.72156864f,
                                            1f
                                        ),
                                        FontSize = Length.Px(16),
                                    }
                                )
                                .Text("Select (dropdown — click to open, pick option)")
                                .Build()
                        ),
                        UI.Box(
                            new PropsBuilder()
                                .Style(
                                    new StyleSheet
                                    {
                                        Display = Display.Flex,
                                        FlexDirection = FlexDirection.Column,
                                        RowGap = Length.Px(8),
                                        ColumnGap = Length.Px(8),
                                        Padding = new Thickness(12f),
                                        Background = new PaperColour(
                                            0.2f,
                                            0.25490198f,
                                            0.33333334f,
                                            1f
                                        ),
                                        BorderRadius = 8f,
                                    }
                                )
                                .Children(
                                    UI.Component(
                                        Paper.Core.Components.Primitives.SelectComponent,
                                        new PropsBuilder()
                                            .Set("options", options)
                                            .Set("selectedValue", selectVal)
                                            .Set("onSelect", setSelectVal)
                                            .Style(StyleSheet.Empty)
                                            .Build()
                                    )
                                )
                                .Build()
                        )
                    )
                    .Build()
            )
        );
    }
}
