using System.Collections.Generic;
using System.Linq;
using Paper.Core.Components;
using Paper.Core.Context;
using Paper.Core.Hooks;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Generated
{
    public static partial class AppComponent
    {
        public static UINode App(Props props)
        {
            List<string> test = [];

            // ── Slider state ─────────────────────────────────────────────────────────
            var (volume, setVolume, _) = Hooks.UseState(40f);
            var (opacity, setOpacity, _) = Hooks.UseState(80f);
            string volText = ((int)volume).ToString();
            string opacText = ((int)opacity).ToString() + "%";

            // ── NumberInput state ─────────────────────────────────────────────────────
            var (qty, setQty, _) = Hooks.UseState(5f);
            var (price, setPrice, _) = Hooks.UseState(9.99f);

            // ── Tabs state ────────────────────────────────────────────────────────────
            var (activeTab, setActiveTab, _) = Hooks.UseState("overview");

            // ── Popover state ─────────────────────────────────────────────────────────
            var (popOpen, setPopOpen, _) = Hooks.UseState(false);
            string popBtnLabel = popOpen ? "Close Popover" : "Open Popover";
            void TogglePop()
            {
                setPopOpen(!popOpen);
            }

            // ── Toast state ───────────────────────────────────────────────────────────
            var (toasts, setToasts, updateToasts) = Hooks.UseState(
                new List<Primitives.ToastEntry>
                {
                    new Primitives.ToastEntry("t0", "App started successfully!", "success", 4f),
                    new Primitives.ToastEntry("t1", "Welcome to Paper UI.", "info", 6f),
                }
            );
            void AddToast(string msg, string variant)
            {
                updateToasts(prev => new List<Primitives.ToastEntry>(prev)
                {
                    new Primitives.ToastEntry(
                        "t" + System.Environment.TickCount64.ToString(),
                        msg,
                        variant
                    ),
                });
            }
            void DismissToast(string id)
            {
                // Use functional update so the timer callback always filters against current state,
                // not the stale closure captured at first render.
                updateToasts(prev => prev.Where(t => t.Id != id).ToList());
            }

            // ── TextInput state ───────────────────────────────────────────────────────
            var (inputText, setInputText, _) = Hooks.UseState("Hello Paper!");
            var (passwordText, setPasswordText, _) = Hooks.UseState("");

            // ── Textarea state ────────────────────────────────────────────────────────
            var (textareaText, setTextareaText, _) = Hooks.UseState(
                "Multi-line\ntext goes here.\nEdit me!"
            );

            // ── MarkdownEditor state ──────────────────────────────────────────────────
            var (mdText, setMdText, _) = Hooks.UseState(
                "# Hello Markdown\n\nThis is **bold**, *italic*, and `inline code`.\n\n> Blockquote here\n\n- Item one\n- Item two\n\n```\ncode block\n```\n\n---\n\nPlain prose paragraph."
            );
            var (mdPreview, setMdPreview, _) = Hooks.UseState(false);
            string editBtnBg = mdPreview ? "#1a1a28" : "#4f46e5";
            string prevBtnBg = mdPreview ? "#4f46e5" : "#1a1a28";
            var mdContent = mdPreview
                ? UI.MarkdownPreview(
                    mdText,
                    new Paper.Core.Styles.StyleSheet
                    {
                        Width = Paper.Core.Styles.Length.Px(480),
                        MinHeight = Paper.Core.Styles.Length.Px(260),
                    }
                )
                : UI.MarkdownEditor(
                    mdText,
                    setMdText,
                    12,
                    new Paper.Core.Styles.StyleSheet { Width = Paper.Core.Styles.Length.Px(480) }
                );

            // ── Checkbox state ────────────────────────────────────────────────────────
            var (checkA, setCheckA, _) = Hooks.UseState(true);
            var (checkB, setCheckB, _) = Hooks.UseState(false);
            var (checkC, setCheckC, _) = Hooks.UseState(false);

            // ── Radio state ───────────────────────────────────────────────────────────
            var (radioVal, setRadioVal, _) = Hooks.UseState("option1");

            // ── Drag and drop state ───────────────────────────────────────────────────
            var (draggedItem, setDraggedItem, _) = Hooks.UseState<string?>(null);
            var (droppedOn, setDroppedOn, _) = Hooks.UseState<string?>(null);
            string dragStatus =
                draggedItem != null
                    ? ("Dragging: " + draggedItem)
                    : (
                        droppedOn != null
                            ? ("Last dropped on: " + droppedOn)
                            : "Drag a card onto the target zone"
                    );
            string dropBg = draggedItem != null ? "#1a2a1a" : "#1a1a2a";
            string dropTextColor = draggedItem != null ? "#22c55e" : "#5a5a7a";
            string dropText = draggedItem != null ? "Drop here!" : "Target Zone";
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
                                            .Text("Slider")
                                            .Build()
                                    ),
                                    UI.Box(
                                        new PropsBuilder()
                                            .ClassName("demo-col-panel")
                                            .Style(
                                                new StyleSheet
                                                {
                                                    RowGap = Length.Px(12),
                                                    ColumnGap = Length.Px(12),
                                                    Padding = new Thickness(12f),
                                                }
                                            )
                                            .Children(
                                                UI.Box(
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Display = Display.Flex,
                                                                FlexDirection = FlexDirection.Row,
                                                                RowGap = Length.Px(12),
                                                                ColumnGap = Length.Px(12),
                                                                AlignItems = AlignItems.Center,
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
                                                                            Width = Length.Px(64),
                                                                        }
                                                                    )
                                                                    .Text("Volume")
                                                                    .Build()
                                                            ),
                                                            UI.Slider(
                                                                volume,
                                                                0f,
                                                                100f,
                                                                1f,
                                                                setVolume,
                                                                new StyleSheet
                                                                {
                                                                    Width = Length.Px(200),
                                                                }
                                                            ),
                                                            new UINode(
                                                                "text",
                                                                new PropsBuilder()
                                                                    .Style(
                                                                        new StyleSheet
                                                                        {
                                                                            Color = new PaperColour(
                                                                                0.3882353f,
                                                                                0.4f,
                                                                                0.94509804f,
                                                                                1f
                                                                            ),
                                                                            Width = Length.Px(36),
                                                                        }
                                                                    )
                                                                    .Text(volText)
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
                                                                FlexDirection = FlexDirection.Row,
                                                                RowGap = Length.Px(12),
                                                                ColumnGap = Length.Px(12),
                                                                AlignItems = AlignItems.Center,
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
                                                                            Width = Length.Px(64),
                                                                        }
                                                                    )
                                                                    .Text("Opacity")
                                                                    .Build()
                                                            ),
                                                            UI.Slider(
                                                                opacity,
                                                                0f,
                                                                100f,
                                                                5f,
                                                                setOpacity,
                                                                new StyleSheet
                                                                {
                                                                    Width = Length.Px(200),
                                                                }
                                                            ),
                                                            new UINode(
                                                                "text",
                                                                new PropsBuilder()
                                                                    .Style(
                                                                        new StyleSheet
                                                                        {
                                                                            Color = new PaperColour(
                                                                                0.3882353f,
                                                                                0.4f,
                                                                                0.94509804f,
                                                                                1f
                                                                            ),
                                                                            Width = Length.Px(36),
                                                                        }
                                                                    )
                                                                    .Text(opacText)
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
                                            .Text("NumberInput")
                                            .Build()
                                    ),
                                    UI.Box(
                                        new PropsBuilder()
                                            .ClassName("demo-col-panel")
                                            .Style(
                                                new StyleSheet
                                                {
                                                    RowGap = Length.Px(12),
                                                    ColumnGap = Length.Px(12),
                                                    Padding = new Thickness(12f),
                                                }
                                            )
                                            .Children(
                                                UI.Box(
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Display = Display.Flex,
                                                                FlexDirection = FlexDirection.Row,
                                                                RowGap = Length.Px(12),
                                                                ColumnGap = Length.Px(12),
                                                                AlignItems = AlignItems.Center,
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
                                                                            Width = Length.Px(80),
                                                                        }
                                                                    )
                                                                    .Text("Quantity")
                                                                    .Build()
                                                            ),
                                                            UI.NumberInput(
                                                                qty,
                                                                1f,
                                                                99f,
                                                                1f,
                                                                setQty,
                                                                null
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
                                                                FlexDirection = FlexDirection.Row,
                                                                RowGap = Length.Px(12),
                                                                ColumnGap = Length.Px(12),
                                                                AlignItems = AlignItems.Center,
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
                                                                            Width = Length.Px(80),
                                                                        }
                                                                    )
                                                                    .Text("Price ($)")
                                                                    .Build()
                                                            ),
                                                            UI.NumberInput(
                                                                price,
                                                                0f,
                                                                null,
                                                                0.5f,
                                                                setPrice,
                                                                null
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
                                            .Text("Tabs")
                                            .Build()
                                    ),
                                    UI.Tabs(
                                        new[]
                                        {
                                            ("overview", "Overview"),
                                            ("details", "Details"),
                                            ("logs", "Logs"),
                                        },
                                        activeTab,
                                        setActiveTab,
                                        null,
                                        null,
                                        UI.Box(
                                            new PropsBuilder()
                                                .Style(
                                                    new StyleSheet
                                                    {
                                                        Padding = new Thickness(16f),
                                                        Background = new PaperColour(
                                                            0.2f,
                                                            0.25490198f,
                                                            0.33333334f,
                                                            1f
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
                                                            .Text(
                                                                "Overview panel — high-level summary goes here."
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
                                                        Padding = new Thickness(16f),
                                                        Background = new PaperColour(
                                                            0.2f,
                                                            0.25490198f,
                                                            0.33333334f,
                                                            1f
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
                                                            .Text(
                                                                "Details panel — in-depth information goes here."
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
                                                        Padding = new Thickness(16f),
                                                        Background = new PaperColour(
                                                            0.2f,
                                                            0.25490198f,
                                                            0.33333334f,
                                                            1f
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
                                                            .Text(
                                                                "Logs panel — recent activity listed here."
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
                                .ClassName("section")
                                .Children(
                                    new UINode(
                                        "text",
                                        new PropsBuilder()
                                            .ClassName("section-label")
                                            .Text("Popover")
                                            .Build()
                                    ),
                                    UI.Box(
                                        new PropsBuilder()
                                            .ClassName("demo-panel")
                                            .Style(
                                                new StyleSheet
                                                {
                                                    Padding = new Thickness(16f),
                                                    MinHeight = Length.Px(140),
                                                }
                                            )
                                            .Children(
                                                UI.Popover(
                                                    popOpen,
                                                    TogglePop,
                                                    "center",
                                                    null,
                                                    null,
                                                    UI.Button(
                                                        popBtnLabel,
                                                        TogglePop,
                                                        StyleSheet.Empty
                                                    ),
                                                    UI.Box(
                                                        new PropsBuilder()
                                                            .Style(
                                                                new StyleSheet
                                                                {
                                                                    Padding = new Thickness(12f),
                                                                    Display = Display.Flex,
                                                                    FlexDirection =
                                                                        FlexDirection.Column,
                                                                    RowGap = Length.Px(8),
                                                                    ColumnGap = Length.Px(8),
                                                                    MinWidth = Length.Px(200),
                                                                }
                                                            )
                                                            .Children(
                                                                new UINode(
                                                                    "text",
                                                                    new PropsBuilder()
                                                                        .Style(
                                                                            new StyleSheet
                                                                            {
                                                                                Color =
                                                                                    new PaperColour(
                                                                                        0.8784314f,
                                                                                        0.8784314f,
                                                                                        0.9411765f,
                                                                                        1f
                                                                                    ),
                                                                                FontWeight =
                                                                                    FontWeight.SemiBold,
                                                                            }
                                                                        )
                                                                        .Text("Popover Title")
                                                                        .Build()
                                                                ),
                                                                new UINode(
                                                                    "text",
                                                                    new PropsBuilder()
                                                                        .Style(
                                                                            new StyleSheet
                                                                            {
                                                                                Color =
                                                                                    new PaperColour(
                                                                                        0.627451f,
                                                                                        0.627451f,
                                                                                        0.72156864f,
                                                                                        1f
                                                                                    ),
                                                                            }
                                                                        )
                                                                        .Text(
                                                                            "Floats anchored to the trigger button."
                                                                        )
                                                                        .Build()
                                                                ),
                                                                UI.Button(
                                                                    "Dismiss",
                                                                    TogglePop,
                                                                    StyleSheet.Empty
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
                        ),
                        UI.Box(
                            new PropsBuilder()
                                .ClassName("section")
                                .Children(
                                    new UINode(
                                        "text",
                                        new PropsBuilder()
                                            .ClassName("section-label")
                                            .Text("Toast Notifications (two shown on start)")
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
                                                    Padding = new Thickness(12f),
                                                }
                                            )
                                            .Children(
                                                UI.Button(
                                                    "Success",
                                                    () =>
                                                    {
                                                        AddToast("Operation succeeded!", "success");
                                                    },
                                                    StyleSheet.Empty
                                                ),
                                                UI.Button(
                                                    "Error",
                                                    () =>
                                                    {
                                                        AddToast("Something went wrong.", "error");
                                                    },
                                                    StyleSheet.Empty
                                                ),
                                                UI.Button(
                                                    "Info",
                                                    () =>
                                                    {
                                                        AddToast("Heads up!", "info");
                                                    },
                                                    StyleSheet.Empty
                                                ),
                                                UI.Button(
                                                    "Warning",
                                                    () =>
                                                    {
                                                        AddToast(
                                                            "Proceed with caution.",
                                                            "warning"
                                                        );
                                                    },
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
                                .ClassName("section")
                                .Children(
                                    new UINode(
                                        "text",
                                        new PropsBuilder()
                                            .ClassName("section-label")
                                            .Text("FontStyle & TextTransform")
                                            .Build()
                                    ),
                                    new UINode("text", new PropsBuilder().Text("Another").Build()),
                                    UI.Box(
                                        new PropsBuilder()
                                            .ClassName("demo-col-panel")
                                            .Style(
                                                new StyleSheet
                                                {
                                                    RowGap = Length.Px(10),
                                                    ColumnGap = Length.Px(10),
                                                    Padding = new Thickness(12f),
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
                                                                    0.8784314f,
                                                                    0.8784314f,
                                                                    0.9411765f,
                                                                    1f
                                                                ),
                                                                FontStyle = FontStyle.Italic,
                                                            }
                                                        )
                                                        .Text("Italic — fontStyle: italic")
                                                        .Build()
                                                ),
                                                new UINode(
                                                    "text",
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Color = new PaperColour(
                                                                    0.8784314f,
                                                                    0.8784314f,
                                                                    0.9411765f,
                                                                    1f
                                                                ),
                                                                TextTransform =
                                                                    TextTransform.Uppercase,
                                                            }
                                                        )
                                                        .Text("uppercase text")
                                                        .Build()
                                                ),
                                                new UINode(
                                                    "text",
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Color = new PaperColour(
                                                                    0.8784314f,
                                                                    0.8784314f,
                                                                    0.9411765f,
                                                                    1f
                                                                ),
                                                                TextTransform =
                                                                    TextTransform.Lowercase,
                                                            }
                                                        )
                                                        .Text("LOWERCASE TEXT")
                                                        .Build()
                                                ),
                                                new UINode(
                                                    "text",
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Color = new PaperColour(
                                                                    0.8784314f,
                                                                    0.8784314f,
                                                                    0.9411765f,
                                                                    1f
                                                                ),
                                                                TextTransform =
                                                                    TextTransform.Capitalize,
                                                            }
                                                        )
                                                        .Text("capitalize every word")
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
                                            .Text("AspectRatio")
                                            .Build()
                                    ),
                                    UI.Box(
                                        new PropsBuilder()
                                            .ClassName("demo-panel")
                                            .Style(
                                                new StyleSheet
                                                {
                                                    RowGap = Length.Px(12),
                                                    ColumnGap = Length.Px(12),
                                                    Padding = new Thickness(12f),
                                                    AlignItems = AlignItems.FlexStart,
                                                }
                                            )
                                            .Children(
                                                UI.Box(
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Width = Length.Px(160),
                                                                AspectRatio = 1.7777777777777777f,
                                                                Padding = new Thickness(10f),
                                                                Background = new PaperColour(
                                                                    0.3882353f,
                                                                    0.4f,
                                                                    0.94509804f,
                                                                    1f
                                                                ),
                                                                BorderRadius = 6f,
                                                                AlignItems = AlignItems.Center,
                                                                JustifyContent =
                                                                    JustifyContent.Center,
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
                                                                                1f,
                                                                                1f,
                                                                                1f,
                                                                                1f
                                                                            ),
                                                                        }
                                                                    )
                                                                    .Text("16:9")
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
                                                                Width = Length.Px(80),
                                                                AspectRatio = 1f,
                                                                Padding = new Thickness(10f),
                                                                Background = new PaperColour(
                                                                    0.13333334f,
                                                                    0.77254903f,
                                                                    0.36862746f,
                                                                    1f
                                                                ),
                                                                BorderRadius = 6f,
                                                                AlignItems = AlignItems.Center,
                                                                JustifyContent =
                                                                    JustifyContent.Center,
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
                                                                                1f,
                                                                                1f,
                                                                                1f,
                                                                                1f
                                                                            ),
                                                                        }
                                                                    )
                                                                    .Text("1:1")
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
                                                                Width = Length.Px(60),
                                                                AspectRatio = 0.5625f,
                                                                Padding = new Thickness(10f),
                                                                Background = new PaperColour(
                                                                    0.9764706f,
                                                                    0.4509804f,
                                                                    0.08627451f,
                                                                    1f
                                                                ),
                                                                BorderRadius = 6f,
                                                                AlignItems = AlignItems.Center,
                                                                JustifyContent =
                                                                    JustifyContent.Center,
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
                                                                                1f,
                                                                                1f,
                                                                                1f,
                                                                                1f
                                                                            ),
                                                                        }
                                                                    )
                                                                    .Text("9:16")
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
                                            .Text("Drag and Drop")
                                            .Build()
                                    ),
                                    UI.Box(
                                        new PropsBuilder()
                                            .ClassName("demo-col-panel")
                                            .Style(
                                                new StyleSheet
                                                {
                                                    RowGap = Length.Px(12),
                                                    ColumnGap = Length.Px(12),
                                                    Padding = new Thickness(12f),
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
                                                        .Text(dragStatus)
                                                        .Build()
                                                ),
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
                                                            UI.Nodes(
                                                                new[]
                                                                {
                                                                    "Card A",
                                                                    "Card B",
                                                                    "Card C",
                                                                }.Select(label =>
                                                                    UI.Box(
                                                                        new PropsBuilder()
                                                                            .OnDragStart(
                                                                                (e) =>
                                                                                    setDraggedItem(
                                                                                        label
                                                                                    )
                                                                            )
                                                                            .OnDragEnd(
                                                                                (e) =>
                                                                                    setDraggedItem(
                                                                                        null
                                                                                    )
                                                                            )
                                                                            .Style(
                                                                                new StyleSheet
                                                                                {
                                                                                    Padding =
                                                                                        new Thickness(
                                                                                            10f
                                                                                        ),
                                                                                    Background =
                                                                                        new PaperColour(
                                                                                            0.16470589f,
                                                                                            0.16470589f,
                                                                                            0.22745098f,
                                                                                            1f
                                                                                        ),
                                                                                    BorderRadius =
                                                                                        6f,
                                                                                    Cursor =
                                                                                        Cursor.Pointer,
                                                                                    Border =
                                                                                        new BorderEdges(
                                                                                            new Border(
                                                                                                1f,
                                                                                                new PaperColour(
                                                                                                    0.22745098f,
                                                                                                    0.22745098f,
                                                                                                    0.33333334f,
                                                                                                    1f
                                                                                                )
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
                                                                                                Color =
                                                                                                    new PaperColour(
                                                                                                        0.8784314f,
                                                                                                        0.8784314f,
                                                                                                        0.9411765f,
                                                                                                        1f
                                                                                                    ),
                                                                                            }
                                                                                        )
                                                                                        .Text(label)
                                                                                        .Build()
                                                                                )
                                                                            )
                                                                            .Build(),
                                                                        label
                                                                    )
                                                                )
                                                            )
                                                        )
                                                        .Build()
                                                ),
                                                UI.Box(
                                                    new PropsBuilder()
                                                        .OnDrop(
                                                            (e) =>
                                                            {
                                                                setDroppedOn(draggedItem);
                                                                setDraggedItem(null);
                                                            }
                                                        )
                                                        .OnDragOver((e) => { })
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Width = Length.Px(160),
                                                                Height = Length.Px(64),
                                                                Padding = new Thickness(12f),
                                                                Background = new PaperColour(
                                                                    dropBg
                                                                ),
                                                                Border = new BorderEdges(
                                                                    new Border(
                                                                        2f,
                                                                        new PaperColour(
                                                                            0.22745098f,
                                                                            0.22745098f,
                                                                            0.33333334f,
                                                                            1f
                                                                        )
                                                                    )
                                                                ),
                                                                BorderRadius = 8f,
                                                                AlignItems = AlignItems.Center,
                                                                JustifyContent =
                                                                    JustifyContent.Center,
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
                                                                                dropTextColor
                                                                            ),
                                                                        }
                                                                    )
                                                                    .Text(dropText)
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
                                            .Text("TextInput")
                                            .Build()
                                    ),
                                    UI.Box(
                                        new PropsBuilder()
                                            .ClassName("demo-col-panel")
                                            .Style(
                                                new StyleSheet
                                                {
                                                    RowGap = Length.Px(12),
                                                    ColumnGap = Length.Px(12),
                                                    Padding = new Thickness(12f),
                                                }
                                            )
                                            .Children(
                                                UI.Box(
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Display = Display.Flex,
                                                                FlexDirection = FlexDirection.Row,
                                                                RowGap = Length.Px(12),
                                                                ColumnGap = Length.Px(12),
                                                                AlignItems = AlignItems.Center,
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
                                                                            Width = Length.Px(80),
                                                                        }
                                                                    )
                                                                    .Text("Text")
                                                                    .Build()
                                                            ),
                                                            UI.Input(
                                                                inputText,
                                                                setInputText,
                                                                new StyleSheet
                                                                {
                                                                    Width = Length.Px(220),
                                                                }
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
                                                                FlexDirection = FlexDirection.Row,
                                                                RowGap = Length.Px(12),
                                                                ColumnGap = Length.Px(12),
                                                                AlignItems = AlignItems.Center,
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
                                                                            Width = Length.Px(80),
                                                                        }
                                                                    )
                                                                    .Text("Second")
                                                                    .Build()
                                                            ),
                                                            UI.Input(
                                                                passwordText,
                                                                setPasswordText,
                                                                new StyleSheet
                                                                {
                                                                    Width = Length.Px(220),
                                                                }
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
                                                                FlexDirection = FlexDirection.Row,
                                                                RowGap = Length.Px(12),
                                                                ColumnGap = Length.Px(12),
                                                                AlignItems = AlignItems.Center,
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
                                                                            Width = Length.Px(80),
                                                                        }
                                                                    )
                                                                    .Text("Value:")
                                                                    .Build()
                                                            ),
                                                            new UINode(
                                                                "text",
                                                                new PropsBuilder()
                                                                    .Style(
                                                                        new StyleSheet
                                                                        {
                                                                            Color = new PaperColour(
                                                                                0.3882353f,
                                                                                0.4f,
                                                                                0.94509804f,
                                                                                1f
                                                                            ),
                                                                        }
                                                                    )
                                                                    .Text(inputText)
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
                                            .Text("Textarea")
                                            .Build()
                                    ),
                                    UI.Box(
                                        new PropsBuilder()
                                            .ClassName("demo-col-panel")
                                            .Style(
                                                new StyleSheet
                                                {
                                                    RowGap = Length.Px(12),
                                                    ColumnGap = Length.Px(12),
                                                    Padding = new Thickness(12f),
                                                }
                                            )
                                            .Children(
                                                UI.Textarea(
                                                    textareaText,
                                                    setTextareaText,
                                                    3,
                                                    new StyleSheet { Width = Length.Px(320) }
                                                ),
                                                UI.Box(
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Display = Display.Flex,
                                                                FlexDirection = FlexDirection.Row,
                                                                RowGap = Length.Px(8),
                                                                ColumnGap = Length.Px(8),
                                                                AlignItems = AlignItems.Center,
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
                                                                    .Text("Lines:")
                                                                    .Build()
                                                            ),
                                                            new UINode(
                                                                "text",
                                                                new PropsBuilder()
                                                                    .Style(
                                                                        new StyleSheet
                                                                        {
                                                                            Color = new PaperColour(
                                                                                0.3882353f,
                                                                                0.4f,
                                                                                0.94509804f,
                                                                                1f
                                                                            ),
                                                                        }
                                                                    )
                                                                    .Text(
                                                                        textareaText
                                                                            .Split('\n')
                                                                            .Length.ToString()
                                                                    )
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
                                            .Text("Markdown Editor")
                                            .Build()
                                    ),
                                    UI.Box(
                                        new PropsBuilder()
                                            .ClassName("demo-col-panel")
                                            .Style(
                                                new StyleSheet
                                                {
                                                    RowGap = Length.Px(10),
                                                    ColumnGap = Length.Px(10),
                                                    Padding = new Thickness(12f),
                                                }
                                            )
                                            .Children(
                                                UI.Nodes(
                                                    UI.Box(
                                                        new PropsBuilder()
                                                            .Style(
                                                                new StyleSheet
                                                                {
                                                                    Display = Display.Flex,
                                                                    FlexDirection =
                                                                        FlexDirection.Row,
                                                                    RowGap = Length.Px(6),
                                                                    ColumnGap = Length.Px(6),
                                                                }
                                                            )
                                                            .Children(
                                                                UI.Button(
                                                                    "Edit",
                                                                    () =>
                                                                    {
                                                                        setMdPreview(false);
                                                                    },
                                                                    new StyleSheet
                                                                    {
                                                                        Padding = new Thickness(
                                                                            Length.Px(4),
                                                                            Length.Px(14)
                                                                        ),
                                                                        Background =
                                                                            new PaperColour(
                                                                                editBtnBg
                                                                            ),
                                                                        Color = new PaperColour(
                                                                            1f,
                                                                            1f,
                                                                            1f,
                                                                            1f
                                                                        ),
                                                                        BorderRadius = 4f,
                                                                        Border = new BorderEdges(
                                                                            new Border(
                                                                                1f,
                                                                                new PaperColour(
                                                                                    0.30980393f,
                                                                                    0.27450982f,
                                                                                    0.8980392f,
                                                                                    1f
                                                                                )
                                                                            )
                                                                        ),
                                                                    }
                                                                ),
                                                                UI.Button(
                                                                    "Preview",
                                                                    () =>
                                                                    {
                                                                        setMdPreview(true);
                                                                    },
                                                                    new StyleSheet
                                                                    {
                                                                        Padding = new Thickness(
                                                                            Length.Px(4),
                                                                            Length.Px(14)
                                                                        ),
                                                                        Background =
                                                                            new PaperColour(
                                                                                prevBtnBg
                                                                            ),
                                                                        Color = new PaperColour(
                                                                            1f,
                                                                            1f,
                                                                            1f,
                                                                            1f
                                                                        ),
                                                                        BorderRadius = 4f,
                                                                        Border = new BorderEdges(
                                                                            new Border(
                                                                                1f,
                                                                                new PaperColour(
                                                                                    0.30980393f,
                                                                                    0.27450982f,
                                                                                    0.8980392f,
                                                                                    1f
                                                                                )
                                                                            )
                                                                        ),
                                                                    }
                                                                )
                                                            )
                                                            .Build()
                                                    ),
                                                    mdContent,
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
                                                                    FontSize = Length.Px(12),
                                                                }
                                                            )
                                                            .Text(
                                                                $"{mdText.Length} chars · {mdText.Split('\n').Length} lines"
                                                            )
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
                                .ClassName("section")
                                .Children(
                                    new UINode(
                                        "text",
                                        new PropsBuilder()
                                            .ClassName("section-label")
                                            .Text("Checkboxes")
                                            .Build()
                                    ),
                                    UI.Box(
                                        new PropsBuilder()
                                            .ClassName("demo-col-panel")
                                            .Style(
                                                new StyleSheet
                                                {
                                                    RowGap = Length.Px(10),
                                                    ColumnGap = Length.Px(10),
                                                    Padding = new Thickness(12f),
                                                }
                                            )
                                            .Children(
                                                UI.Checkbox(
                                                    checkA,
                                                    (bool b) => setCheckA(b),
                                                    "Enable notifications",
                                                    StyleSheet.Empty
                                                ),
                                                UI.Checkbox(
                                                    checkB,
                                                    (bool b) => setCheckB(b),
                                                    "Dark mode",
                                                    StyleSheet.Empty
                                                ),
                                                UI.Checkbox(
                                                    checkC,
                                                    (bool b) => setCheckC(b),
                                                    "Auto-save",
                                                    StyleSheet.Empty
                                                ),
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
                                                                FontSize = Length.Px(12),
                                                            }
                                                        )
                                                        .Text(
                                                            $"Notifications: {(checkA ? "on" : "off")}  Dark mode: {(checkB ? "on" : "off")}"
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
                                            .Text("Radio Buttons")
                                            .Build()
                                    ),
                                    UI.Box(
                                        new PropsBuilder()
                                            .ClassName("demo-col-panel")
                                            .Style(
                                                new StyleSheet
                                                {
                                                    RowGap = Length.Px(12),
                                                    ColumnGap = Length.Px(12),
                                                    Padding = new Thickness(12f),
                                                }
                                            )
                                            .Children(
                                                UI.RadioGroup(
                                                    new[]
                                                    {
                                                        ("option1", "Option One"),
                                                        ("option2", "Option Two"),
                                                        ("option3", "Option Three"),
                                                    },
                                                    radioVal,
                                                    setRadioVal,
                                                    StyleSheet.Empty
                                                ),
                                                new UINode(
                                                    "text",
                                                    new PropsBuilder()
                                                        .Style(
                                                            new StyleSheet
                                                            {
                                                                Color = new PaperColour(
                                                                    0.3882353f,
                                                                    0.4f,
                                                                    0.94509804f,
                                                                    1f
                                                                ),
                                                                FontSize = Length.Px(13),
                                                            }
                                                        )
                                                        .Text($"Selected: {radioVal}")
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
                                                    Padding = new Thickness(8f),
                                                    Height = Length.Px(72),
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
                                                    Padding = new Thickness(8f),
                                                    Height = Length.Px(64),
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
                                                    Padding = new Thickness(8f),
                                                    Height = Length.Px(64),
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
                                                    Padding = new Thickness(8f),
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
                                                    Padding = new Thickness(8f),
                                                    Height = Length.Px(80),
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
                                            .Text(
                                                "alignItems: center + alignSelf: flex-end on middle"
                                            )
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
                                                    Padding = new Thickness(8f),
                                                    Height = Length.Px(80),
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
                                                UI.Nodes(
                                                    new[]
                                                    {
                                                        "#f59e0b",
                                                        "#d97706",
                                                        "#b45309",
                                                        "#92400e",
                                                        "#78350f",
                                                        "#f59e0b",
                                                        "#d97706",
                                                        "#b45309",
                                                        "#92400e",
                                                        "#78350f",
                                                        "#f59e0b",
                                                        "#d97706",
                                                        "#b45309",
                                                        "#92400e",
                                                        "#78350f",
                                                        "#f59e0b",
                                                        "#d97706",
                                                        "#b45309",
                                                        "#92400e",
                                                        "#78350f",
                                                    }.Select(
                                                        (c, i) =>
                                                            UI.Box(
                                                                new PropsBuilder()
                                                                    .Style(
                                                                        new StyleSheet
                                                                        {
                                                                            Width = Length.Px(70),
                                                                            Height = Length.Px(36),
                                                                            Background =
                                                                                new PaperColour(c),
                                                                        }
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
                                                    Padding = new Thickness(8f),
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
                                                                    .ClassName(
                                                                        "white-text text-center"
                                                                    )
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
                                                                    .ClassName(
                                                                        "white-text text-center"
                                                                    )
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
                                                                    .ClassName(
                                                                        "white-text text-center"
                                                                    )
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
                                                                                new PaperColour(
                                                                                    color
                                                                                ),
                                                                        }
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
                        ),
                        UI.Box(
                            new PropsBuilder()
                                .ClassName("section")
                                .Style(new StyleSheet { MinHeight = Length.Px(170) })
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
                                                    Padding = new Thickness(8f),
                                                    FlexGrow = 1f,
                                                    MinHeight = Length.Px(120),
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
                                                                    .Text($"Item {n}")
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
                        ),
                        UI.Box(
                            new PropsBuilder()
                                .Style(new StyleSheet { MinHeight = Length.Px(48) })
                                .Build()
                        ),
                        UI.ToastContainer(toasts, DismissToast)
                    )
                    .Build()
            );
        }
    }
}
