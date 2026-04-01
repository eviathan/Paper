using Paper.Core.Dock;
using Paper.Core.VirtualDom;
using Paper.Core.Styles;
using Paper.Core.Components;
using Paper.Core.Hooks;
using Paper.Core.Events;
using System.Collections.Generic;

namespace Paper.Playground;

public static class TestDockComponent
{
    public static UINode TestDock(Props props)
    {
        var bg1a1a2e = new PaperColour(0.102f, 0.102f, 0.180f, 1f);
        var bgDark = new PaperColour(0.1f, 0.1f, 0.15f, 1f);
        var colorC8C8E0 = new PaperColour(0.784f, 0.784f, 0.878f, 1f);
        var color7878A0 = new PaperColour(0.471f, 0.471f, 0.627f, 1f);
        var color5050A0 = new PaperColour(0.314f, 0.314f, 0.627f, 1f);
        
        var (leftWidth, setLeftWidth, _) = Hooks.UseState(200f);
        var (rightWidth, setRightWidth, _) = Hooks.UseState(150f);
        
        var (panelPositions, updatePanelPositions, _) = Hooks.UseState(Helpers.MakeDict(new (string, string)[] {
            ("left", "left"),
            ("center", "center"),
            ("right", "right")
        }));
        
        var (activeDemo, setActiveDemo, _) = Hooks.UseState("Slider");
        
        var (minimizedPanels, updateMinimizedPanels, _) = Hooks.UseState(new List<string>());
        
        var (maximizedPanel, setMaximizedPanel, _) = Hooks.UseState<string?>(null);
        
        var resizeState = Hooks.UseStable(() => new object[] { null, 0f, 0f, false });
        var dragState = Hooks.UseStable(() => new object[] { null, 0f });
        
        var demoNames = new[] { 
            "Slider", "NumberInput", "Tabs", "Popover", "Toast", "FontStyle", "AspectRatio",
            "DragDrop", "TextInput", "Textarea", "Markdown", "Checkbox", "Radio",
            "FlexGrow", "Justify", "AlignItems", "FlexWrap", "RowReverse", "LambdaJSX", 
            "Column", "List", "FileDialog" 
        };
        
        void HandleResizeMove(float x) {
            if (!(bool)resizeState[3]) return;
            string mode = resizeState[0] as string ?? "";
            float startX = (float)resizeState[1];
            float startWidth = (float)resizeState[2];
            
            float delta = x - startX;
            if (mode == "left") {
                float newWidth = Math.Clamp(startWidth + delta, 100f, 400f);
                setLeftWidth(newWidth);
            } else if (mode == "right") {
                float newWidth = Math.Clamp(startWidth - delta, 100f, 400f);
                setRightWidth(newWidth);
            }
        }
        
        void HandleResizeEnd() {
            resizeState[3] = false;
        }
        
        void ToggleMinimize(string panelId) {
            var copy = new List<string>(minimizedPanels ?? new List<string>());
            if (copy.Contains(panelId)) {
                copy.Remove(panelId);
            } else {
                copy.Add(panelId);
            }
            updateMinimizedPanels(copy);
        }
        
        void ToggleMaximize(string panelId) {
            if (maximizedPanel == panelId) {
                setMaximizedPanel(null);
            } else {
                setMaximizedPanel(panelId);
            }
        }
        
        void HandlePanelDragMove(float x, float containerLeft, float containerWidth) {
            if (dragState[0] == null) return;
            string panelId = dragState[0] as string ?? "";
            
            float relX = x - containerLeft;
            float third = containerWidth / 3f;
            string newPos;
            if (relX < third) newPos = "left";
            else if (relX > third * 2) newPos = "right";
            else newPos = "center";
            
            var newPositions = new Dictionary<string, string>(panelPositions ?? new Dictionary<string, string>());
            newPositions[panelId] = newPos;
            updatePanelPositions(newPositions);
        }
        
        void HandlePanelDragEnd() {
            dragState[0] = null;
        }
        
        UINode CreateSidebarItem(string name, bool isSelected) {
            var itemStyle = new StyleSheet {
                Padding = new Thickness(12, 10, 12, 10),
                Background = isSelected ? new PaperColour(0.2f, 0.2f, 0.35f, 1f) : new PaperColour(0f, 0f, 0f, 0f),
                Cursor = Paper.Core.Styles.Cursor.Pointer,
                Display = Display.Flex,
                AlignItems = AlignItems.Center,
                MinHeight = 36f,
                BorderBottom = new Border(1f, new PaperColour(0.15f, 0.15f, 0.2f, 1f)),
            };
            return UI.Box(
                new PropsBuilder()
                    .Style(itemStyle)
                    .OnClick(() => setActiveDemo(name))
                    .Children(UI.Text(name, new StyleSheet {
                        Color = isSelected ? colorC8C8E0 : color7878A0,
                        FontSize = 13f,
                    }))
                    .Build()
            );
        }
        
        var leftPanelContent = new UINode[demoNames.Length];
        for (int i = 0; i < demoNames.Length; i++) {
            leftPanelContent[i] = CreateSidebarItem(demoNames[i], demoNames[i] == activeDemo);
        }
        
        UINode BuildLeftPanel() {
            var isMin = minimizedPanels?.Contains("left") ?? false;
            var isMax = maximizedPanel == "left";
            
            UINode headerButtons = UI.Box(new PropsBuilder()
                .Style(new StyleSheet { Display = Display.Flex, Gap = 2f, MarginRight = 4f })
                .Children(
                    UI.Box(new PropsBuilder()
                        .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                        .OnClick(() => ToggleMinimize("left"))
                        .Children(UI.Text("▼", new StyleSheet { FontSize = 10f }))
                        .Build()),
                    UI.Box(new PropsBuilder()
                        .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                        .OnClick(() => ToggleMaximize("left"))
                        .Children(UI.Text("□", new StyleSheet { FontSize = 10f }))
                        .Build()),
                    UI.Box(new PropsBuilder()
                        .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                        .OnClick(() => ToggleMinimize("left"))
                        .Children(UI.Text("✕", new StyleSheet { FontSize = 10f }))
                        .Build())
                )
                .Build());
            
            UINode leftHeader = UI.Box(
                new PropsBuilder()
                    .Style(new StyleSheet {
                        Height = 30f,
                        Background = new PaperColour(0.145f, 0.145f, 0.27f, 1f),
                        Display = Display.Flex,
                        AlignItems = AlignItems.Center,
                        Padding = new Thickness(8, 0, 0, 0),
                        Cursor = Cursor.Grab,
                    })
                    .OnDragStart(e => { dragState[0] = "left"; dragState[1] = e.X; e.StopPropagation(); })
                    .OnDrag(e => { HandlePanelDragMove(e.X, leftWidth + rightWidth + 100f, 800f); })
                    .OnDragEnd(e => { HandlePanelDragEnd(); })
                    .Children(
                        UI.Text("Components", new StyleSheet { Color = colorC8C8E0, FlexGrow = 1f, FontSize = 13f }),
                        isMin ? null : headerButtons
                    )
                    .Build()
            );
            
            if (isMin) {
                return UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet {
                            Width = Length.Px(leftWidth),
                            Height = 24f,
                            Background = new PaperColour(0.08f, 0.08f, 0.16f, 1f),
                            Border = new BorderEdges(new Border(1f, new PaperColour(0.3f, 0.3f, 0.4f, 1f))),
                            Display = Display.Flex,
                            AlignItems = AlignItems.Center,
                            Padding = new Thickness(8, 0, 8, 0),
                            Cursor = Cursor.Pointer,
                        })
                        .OnClick(() => ToggleMinimize("left"))
                        .Children(
                            UI.Text("Components", new StyleSheet { Color = color7878A0, FlexGrow = 1f, FontSize = 12f }),
                            UI.Box(new PropsBuilder()
                                .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                                .OnClick(() => ToggleMaximize("left"))
                                .Children(UI.Text("□", new StyleSheet { FontSize = 10f }))
                                .Build())
                        )
                        .Build()
                );
            }
            
            UINode resizeHandle = UI.Box(
                new PropsBuilder()
                    .Style(new StyleSheet {
                        Width = 8f,
                        Position = Position.Absolute,
                        Right = -4f,
                        Top = 0f,
                        Bottom = 0f,
                        Background = new PaperColour(0.25f, 0.25f, 0.35f, 1f),
                        Cursor = Cursor.EwResize,
                        ZIndex = 10,
                    })
                    .OnPointerDown(e => { resizeState[0] = "left"; resizeState[1] = e.X; resizeState[2] = leftWidth; resizeState[3] = true; e.StopPropagation(); })
                    .Build()
            );
            
            return UI.Box(
                new PropsBuilder()
                    .Style(new StyleSheet {
                        Width = Length.Px(leftWidth),
                        Display = Display.Flex,
                        FlexDirection = FlexDirection.Column,
                        Background = new PaperColour(0.12f, 0.12f, 0.18f, 1f),
                        Border = new BorderEdges(new Border(1f, new PaperColour(0.3f, 0.3f, 0.4f, 1f))),
                        Position = Position.Relative,
                    })
                    .Children(
                        leftHeader,
                        UI.Box(
                            new PropsBuilder()
                                .Style(new StyleSheet { FlexGrow = 1f, Overflow = Overflow.Scroll })
                                .Children(leftPanelContent)
                                .Build()
                        ),
                        resizeHandle
                    )
                    .Build()
            );
        }
        
        UINode BuildRightPanel() {
            var isMin = minimizedPanels?.Contains("right") ?? false;
            var isMax = maximizedPanel == "right";
            
            UINode headerButtons = UI.Box(new PropsBuilder()
                .Style(new StyleSheet { Display = Display.Flex, Gap = 2f, MarginRight = 4f })
                .Children(
                    UI.Box(new PropsBuilder()
                        .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                        .OnClick(() => ToggleMinimize("right"))
                        .Children(UI.Text("▼", new StyleSheet { FontSize = 10f }))
                        .Build()),
                    UI.Box(new PropsBuilder()
                        .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                        .OnClick(() => ToggleMaximize("right"))
                        .Children(UI.Text("□", new StyleSheet { FontSize = 10f }))
                        .Build()),
                    UI.Box(new PropsBuilder()
                        .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                        .OnClick(() => ToggleMinimize("right"))
                        .Children(UI.Text("✕", new StyleSheet { FontSize = 10f }))
                        .Build())
                )
                .Build());
            
            UINode rightHeader = UI.Box(
                new PropsBuilder()
                    .Style(new StyleSheet {
                        Height = 30f,
                        Background = new PaperColour(0.145f, 0.145f, 0.27f, 1f),
                        Display = Display.Flex,
                        AlignItems = AlignItems.Center,
                        Padding = new Thickness(8, 0, 0, 0),
                        Cursor = Cursor.Grab,
                    })
                    .OnDragStart(e => { dragState[0] = "right"; dragState[1] = e.X; e.StopPropagation(); })
                    .OnDrag(e => { HandlePanelDragMove(e.X, leftWidth + rightWidth + 100f, 800f); })
                    .OnDragEnd(e => { HandlePanelDragEnd(); })
                    .Children(
                        UI.Text("Info", new StyleSheet { Color = colorC8C8E0, FlexGrow = 1f, FontSize = 13f }),
                        isMin ? null : headerButtons
                    )
                    .Build()
            );
            
            if (isMin) {
                return UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet {
                            Width = Length.Px(rightWidth),
                            Height = 24f,
                            Background = new PaperColour(0.08f, 0.08f, 0.16f, 1f),
                            Border = new BorderEdges(new Border(1f, new PaperColour(0.3f, 0.3f, 0.4f, 1f))),
                            Display = Display.Flex,
                            AlignItems = AlignItems.Center,
                            Padding = new Thickness(8, 0, 8, 0),
                            Cursor = Cursor.Pointer,
                        })
                        .OnClick(() => ToggleMinimize("right"))
                        .Children(
                            UI.Text("Info", new StyleSheet { Color = color7878A0, FlexGrow = 1f, FontSize = 12f }),
                            UI.Box(new PropsBuilder()
                                .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                                .OnClick(() => ToggleMaximize("right"))
                                .Children(UI.Text("□", new StyleSheet { FontSize = 10f }))
                                .Build())
                        )
                        .Build()
                );
            }
            
            UINode resizeHandle = UI.Box(
                new PropsBuilder()
                    .Style(new StyleSheet {
                        Width = 8f,
                        Position = Position.Absolute,
                        Left = -4f,
                        Top = 0f,
                        Bottom = 0f,
                        Background = new PaperColour(0.25f, 0.25f, 0.35f, 1f),
                        Cursor = Cursor.EwResize,
                        ZIndex = 10,
                    })
                    .OnPointerDown(e => { resizeState[0] = "right"; resizeState[1] = e.X; resizeState[2] = rightWidth; resizeState[3] = true; e.StopPropagation(); })
                    .Build()
            );
            
            return UI.Box(
                new PropsBuilder()
                    .Style(new StyleSheet {
                        Width = Length.Px(rightWidth),
                        Display = Display.Flex,
                        FlexDirection = FlexDirection.Column,
                        Background = new PaperColour(0.12f, 0.12f, 0.18f, 1f),
                        Border = new BorderEdges(new Border(1f, new PaperColour(0.3f, 0.3f, 0.4f, 1f))),
                        Position = Position.Relative,
                    })
                    .Children(
                        rightHeader,
                        UI.Box(
                            new PropsBuilder()
                                .Style(new StyleSheet { FlexGrow = 1f, Padding = 12f })
                                .Children(
                                    UI.Text("Active Demo:", new StyleSheet { Color = color7878A0, FontSize = 12f }),
                                    UI.Text(activeDemo, new StyleSheet { Color = colorC8C8E0, FontSize = 14f, FontWeight = FontWeight.Bold }),
                                    UI.Box(new PropsBuilder().Style(new StyleSheet { Height = 16f }).Build()),
                                    UI.Text("Drag header to reposition", new StyleSheet { Color = color5050A0, FontSize = 11f }),
                                    UI.Text("Drag edge to resize", new StyleSheet { Color = color5050A0, FontSize = 11f, MarginTop = 4f })
                                )
                                .Build()
                        ),
                        resizeHandle
                    )
                    .Build()
            );
        }
        
        UINode BuildCenterPanel() {
            var isMin = minimizedPanels?.Contains("center") ?? false;
            var isMax = maximizedPanel == "center";
            
            UINode headerButtons = UI.Box(new PropsBuilder()
                .Style(new StyleSheet { Display = Display.Flex, Gap = 2f, MarginRight = 4f })
                .Children(
                    UI.Box(new PropsBuilder()
                        .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                        .OnClick(() => ToggleMinimize("center"))
                        .Children(UI.Text("▼", new StyleSheet { FontSize = 10f }))
                        .Build()),
                    UI.Box(new PropsBuilder()
                        .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                        .OnClick(() => ToggleMaximize("center"))
                        .Children(UI.Text("□", new StyleSheet { FontSize = 10f }))
                        .Build()),
                    UI.Box(new PropsBuilder()
                        .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                        .OnClick(() => ToggleMinimize("center"))
                        .Children(UI.Text("✕", new StyleSheet { FontSize = 10f }))
                        .Build())
                )
                .Build());
            
            UINode demoContent;
            if (activeDemo == "Slider") {
                var (sliderVal, setSliderVal, _) = Hooks.UseState(50f);
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Slider Demo", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, FlexDirection = FlexDirection.Row, Gap = 12f, AlignItems = AlignItems.Center })
                                    .Children(
                                        UI.Text("Value:", new StyleSheet { Color = color7878A0 }),
                                        UI.Slider(sliderVal, 0f, 100f, 1f, setSliderVal, new StyleSheet { Width = 200f }),
                                        UI.Text(((int)sliderVal).ToString(), new StyleSheet { Color = colorC8C8E0, MinWidth = 40f })
                                    )
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "NumberInput") {
                var (numVal, setNumVal, _) = Hooks.UseState(5f);
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("NumberInput Demo", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, FlexDirection = FlexDirection.Row, Gap = 12f, AlignItems = AlignItems.Center })
                                    .Children(
                                        UI.Text("Quantity:", new StyleSheet { Color = color7878A0 }),
                                        UI.NumberInput(numVal, 1f, 99f, 1f, setNumVal)
                                    )
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "Tabs") {
                var (tab, setTab, _) = Hooks.UseState("overview");
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Tabs Demo", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Tabs(new[] { ("overview", "Overview"), ("details", "Details"), ("logs", "Logs") }, tab, setTab, null, null,
                                UI.Box(
                                    new PropsBuilder()
                                        .Style(new StyleSheet { Padding = 16f, Background = new PaperColour(0.15f, 0.15f, 0.2f, 1f) })
                                        .Children(UI.Text("Content for " + tab + " tab", new StyleSheet { Color = color7878A0 }))
                                        .Build()
                                )
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "Popover") {
                var (popOpen, setPopOpen, _) = Hooks.UseState(false);
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Popover Demo", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Popover(popOpen, () => setPopOpen(false), "bottom", null, null,
                                UI.Button("Open Popover", () => setPopOpen(true)),
                                UI.Box(
                                    new PropsBuilder()
                                        .Style(new StyleSheet { Padding = 12f, MinWidth = 180f })
                                        .Children(
                                            UI.Text("Popover Content", new StyleSheet { Color = colorC8C8E0, FontWeight = FontWeight.Bold }),
                                            UI.Text("Click outside to close", new StyleSheet { Color = color7878A0, FontSize = 12f, MarginTop = 8f })
                                        )
                                        .Build()
                                )
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "Toast") {
                var (toasts, setToasts, _) = Hooks.UseState(new List<Primitives.ToastEntry>());
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Toast Demo", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, Gap = 8f })
                                    .Children(
                                        UI.Button("Show Success", () => {
                                            var newToasts = new List<Primitives.ToastEntry>(toasts);
                                            newToasts.Add(new Primitives.ToastEntry("t" + System.Environment.TickCount64, "Success!", "success"));
                                            setToasts(newToasts);
                                        }),
                                        UI.Button("Show Error", () => {
                                            var newToasts = new List<Primitives.ToastEntry>(toasts);
                                            newToasts.Add(new Primitives.ToastEntry("t" + System.Environment.TickCount64, "Error occurred", "error"));
                                            setToasts(newToasts);
                                        }),
                                        UI.Button("Show Info", () => {
                                            var newToasts = new List<Primitives.ToastEntry>(toasts);
                                            newToasts.Add(new Primitives.ToastEntry("t" + System.Environment.TickCount64, "Info message", "info"));
                                            setToasts(newToasts);
                                        })
                                    )
                                    .Build()
                            ),
                            UI.ToastContainer(toasts, id => setToasts(toasts.Where(t => t.Id != id).ToList()))
                        )
                        .Build()
                );
            }
            else if (activeDemo == "TextInput") {
                var (textVal, setTextVal, _) = Hooks.UseState("Hello!");
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("TextInput Demo", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, FlexDirection = FlexDirection.Column, Gap = 12f })
                                    .Children(
                                        UI.Text("Type:", new StyleSheet { Color = color7878A0 }),
                                        UI.Input(textVal, setTextVal, new StyleSheet { Width = 250f }),
                                        UI.Text("Value: " + textVal, new StyleSheet { Color = colorC8C8E0 })
                                    )
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "Textarea") {
                var (areaVal, setAreaVal, _) = Hooks.UseState("Multi-line\ntext here");
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Textarea Demo", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Textarea(areaVal, setAreaVal, 4, new StyleSheet { Width = 300f, Height = 100f })
                        )
                        .Build()
                );
            }
            else if (activeDemo == "Markdown") {
                var (md, setMd, _) = Hooks.UseState("# Hello\n\n**Bold** and *italic*");
                var (preview, setPreview, _) = Hooks.UseState(false);
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Markdown Demo", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, Gap = 8f, MarginBottom = 12f })
                                    .Children(
                                        UI.Button("Edit", () => setPreview(false)),
                                        UI.Button("Preview", () => setPreview(true))
                                    )
                                    .Build()
                            ),
                            preview 
                                ? UI.MarkdownPreview(md, new StyleSheet { Width = 400f, MinHeight = 200f })
                                : UI.MarkdownEditor(md, setMd, 10, new StyleSheet { Width = 400f })
                        )
                        .Build()
                );
            }
            else if (activeDemo == "Checkbox") {
                var (check1, setCheck1, _) = Hooks.UseState(true);
                var (check2, setCheck2, _) = Hooks.UseState(false);
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Checkbox Demo", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Checkbox(check1, setCheck1, "Enable feature A"),
                            UI.Checkbox(check2, setCheck2, "Enable feature B"),
                            UI.Text("State: " + (check1 ? "A " : "") + (check2 ? "B" : ""), new StyleSheet { Color = color7878A0, MarginTop = 12f })
                        )
                        .Build()
                );
            }
            else if (activeDemo == "Radio") {
                var (radio, setRadio, _) = Hooks.UseState("opt1");
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Radio Demo", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.RadioGroup(new[] { ("opt1", "Option One"), ("opt2", "Option Two"), ("opt3", "Option Three") }, radio, setRadio),
                            UI.Text("Selected: " + radio, new StyleSheet { Color = color7878A0, MarginTop = 12f })
                        )
                        .Build()
                );
            }
            else if (activeDemo == "List") {
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("List Demo (Virtualized)", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.List(Enumerable.Range(1, 100).ToArray(), 32f, 200f,
                                (item, idx) => UI.Box(
                                    new PropsBuilder()
                                        .Style(new StyleSheet { Height = 32f, Padding = new Thickness(8, 0, 8, 0), Border = new BorderEdges(new Border(0f, new PaperColour(0.2f, 0.2f, 0.25f, 1f))) })
                                        .Children(UI.Text("Item " + item, new StyleSheet { Color = colorC8C8E0 }))
                                        .Build()
                                )
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "DragDrop") {
                var (dragged, setDragged, _) = Hooks.UseState<string?>(null);
                var (dropped, setDropped, _) = Hooks.UseState<string?>(null);
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Drag & Drop Demo", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Text(dragged != null ? "Dragging: " + dragged : (dropped != null ? "Dropped on: " + dropped : "Drag a card"), new StyleSheet { Color = color7878A0, MarginBottom = 12f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, Gap = 8f, MarginBottom = 16f })
                                    .Children(
                                        UI.Box(
                                            new PropsBuilder()
                                                .OnDragStart(e => setDragged("A"))
                                                .OnDragEnd(e => setDragged(null))
                                                .Style(new StyleSheet { Padding = 12f, Background = new PaperColour(0.2f, 0.2f, 0.3f, 1f), BorderRadius = 6f })
                                                .Children(UI.Text("A", new StyleSheet { Color = colorC8C8E0 }))
                                                .Build()
                                        ),
                                        UI.Box(
                                            new PropsBuilder()
                                                .OnDragStart(e => setDragged("B"))
                                                .OnDragEnd(e => setDragged(null))
                                                .Style(new StyleSheet { Padding = 12f, Background = new PaperColour(0.2f, 0.2f, 0.3f, 1f), BorderRadius = 6f })
                                                .Children(UI.Text("B", new StyleSheet { Color = colorC8C8E0 }))
                                                .Build()
                                        ),
                                        UI.Box(
                                            new PropsBuilder()
                                                .OnDragStart(e => setDragged("C"))
                                                .OnDragEnd(e => setDragged(null))
                                                .Style(new StyleSheet { Padding = 12f, Background = new PaperColour(0.2f, 0.2f, 0.3f, 1f), BorderRadius = 6f })
                                                .Children(UI.Text("C", new StyleSheet { Color = colorC8C8E0 }))
                                                .Build()
                                        )
                                    )
                                    .Build()
                            ),
                            UI.Box(
                                new PropsBuilder()
                                    .OnDrop(e => { setDropped(dragged ?? "item"); setDragged(null); })
                                    .OnDragOver(e => {})
                                    .Style(new StyleSheet { 
                                        Width = 120f, Height = 60f, 
                                        Padding = 12f, 
                                        Background = dragged != null ? new PaperColour(0.1f, 0.2f, 0.1f, 1f) : new PaperColour(0.15f, 0.15f, 0.2f, 1f),
                                        Border = new BorderEdges(new Border(2f, new PaperColour(0.4f, 0.4f, 0.5f, 1f))),
                                        BorderRadius = 8f,
                                        Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center
                                    })
                                    .Children(UI.Text(dragged != null ? "Drop!" : "Target", new StyleSheet { Color = dragged != null ? new PaperColour(0.5f, 0.9f, 0.5f, 1f) : color7878A0 }))
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "FontStyle") {
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("FontStyle & TextTransform", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, FlexDirection = FlexDirection.Column, Gap = 10f })
                                    .Children(
                                        UI.Text("Italic text", new StyleSheet { Color = colorC8C8E0, FontStyle = FontStyle.Italic }),
                                        UI.Text("UPPERCASE TEXT", new StyleSheet { Color = colorC8C8E0, TextTransform = TextTransform.Uppercase }),
                                        UI.Text("lowercase text", new StyleSheet { Color = colorC8C8E0, TextTransform = TextTransform.Lowercase }),
                                        UI.Text("Capitalize Every Word", new StyleSheet { Color = colorC8C8E0, TextTransform = TextTransform.Capitalize })
                                    )
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "AspectRatio") {
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("AspectRatio", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, Gap = 12f, AlignItems = AlignItems.Center })
                                    .Children(
                                        UI.Box(
                                            new PropsBuilder()
                                                .Style(new StyleSheet { Width = 160f, AspectRatio = 16f/9f, Padding = 10f, Background = new PaperColour(0.39f, 0.39f, 0.95f, 1f), BorderRadius = 6f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center })
                                                .Children(UI.Text("16:9", new StyleSheet { Color = PaperColour.White }))
                                                .Build()
                                        ),
                                        UI.Box(
                                            new PropsBuilder()
                                                .Style(new StyleSheet { Width = 80f, AspectRatio = 1f, Padding = 10f, Background = new PaperColour(0.13f, 0.77f, 0.33f, 1f), BorderRadius = 6f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center })
                                                .Children(UI.Text("1:1", new StyleSheet { Color = PaperColour.White }))
                                                .Build()
                                        ),
                                        UI.Box(
                                            new PropsBuilder()
                                                .Style(new StyleSheet { Width = 60f, AspectRatio = 9f/16f, Padding = 10f, Background = new PaperColour(0.98f, 0.45f, 0.09f, 1f), BorderRadius = 6f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center })
                                                .Children(UI.Text("9:16", new StyleSheet { Color = PaperColour.White }))
                                                .Build()
                                        )
                                    )
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "FlexGrow") {
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("flexGrow (1, 2, 1)", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, Gap = 8f, Height = 72f })
                                    .Children(
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { FlexGrow = 1f, Background = new PaperColour(0.94f, 0.27f, 0.27f, 1f) }).Children(UI.Text("1", new StyleSheet { Color = PaperColour.White })).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { FlexGrow = 2f, Background = new PaperColour(0.98f, 0.45f, 0.09f, 1f) }).Children(UI.Text("2", new StyleSheet { Color = PaperColour.White })).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { FlexGrow = 1f, Background = new PaperColour(0.13f, 0.77f, 0.33f, 1f) }).Children(UI.Text("1", new StyleSheet { Color = PaperColour.White })).Build())
                                    )
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "Justify") {
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("justifyContent: space-between", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, JustifyContent = JustifyContent.SpaceBetween, Gap = 8f, Height = 64f })
                                    .Children(
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 60f, Height = 48f, Background = new PaperColour(0.39f, 0.39f, 0.95f, 1f) }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 60f, Height = 48f, Background = new PaperColour(0.55f, 0.36f, 0.96f, 1f) }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 60f, Height = 48f, Background = new PaperColour(0.66f, 0.33f, 0.97f, 1f) }).Build())
                                    )
                                    .Build()
                            ),
                            UI.Text("justifyContent: center", new StyleSheet { Color = colorC8C8E0, FontSize = 16f, MarginTop = 16f, MarginBottom = 8f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, JustifyContent = JustifyContent.Center, Gap = 8f, Height = 64f })
                                    .Children(
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 60f, Height = 48f, Background = new PaperColour(0.93f, 0.29f, 0.6f, 1f) }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 60f, Height = 48f, Background = new PaperColour(0.96f, 0.25f, 0.37f, 1f) }).Build())
                                    )
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "AlignItems") {
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("alignItems: stretch", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, AlignItems = AlignItems.Stretch, Gap = 8f, Height = 80f })
                                    .Children(
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 40f, Background = new PaperColour(0.92f, 0.7f, 0.03f, 1f) }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 40f, Background = new PaperColour(0.52f, 0.8f, 0.09f, 1f), MinHeight = 32f }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 40f, Background = new PaperColour(0.4f, 0.64f, 0.05f, 1f) }).Build())
                                    )
                                    .Build()
                            ),
                            UI.Text("alignItems: center", new StyleSheet { Color = colorC8C8E0, FontSize = 16f, MarginTop = 16f, MarginBottom = 8f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, AlignItems = AlignItems.Center, Gap = 8f, Height = 80f })
                                    .Children(
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 50f, Height = 40f, Background = new PaperColour(0.055f, 0.65f, 0.56f, 1f) }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 50f, Height = 24f, Background = new PaperColour(0.22f, 0.74f, 0.97f, 1f) }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 50f, Height = 40f, Background = new PaperColour(0.01f, 0.52f, 0.78f, 1f) }).Build())
                                    )
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "FlexWrap") {
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("flexWrap: wrap", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, FlexWrap = FlexWrap.Wrap, Gap = 8f, MinHeight = 80f })
                                    .Children(
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 70f, Height = 36f, Background = new PaperColour(0.96f, 0.62f, 0.04f, 1f) }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 70f, Height = 36f, Background = new PaperColour(0.85f, 0.47f, 0.02f, 1f) }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 70f, Height = 36f, Background = new PaperColour(0.71f, 0.33f, 0.04f, 1f) }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 70f, Height = 36f, Background = new PaperColour(0.57f, 0.25f, 0.05f, 1f) }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 70f, Height = 36f, Background = new PaperColour(0.47f, 0.21f, 0.06f, 1f) }).Build())
                                    )
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "RowReverse") {
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("flexDirection: row-reverse", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, FlexDirection = FlexDirection.RowReverse, Gap = 8f })
                                    .Children(
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 64f, Height = 48f, Background = new PaperColour(0.49f, 0.23f, 0.93f, 1f) }).Children(UI.Text("A", new StyleSheet { Color = PaperColour.White })).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 64f, Height = 48f, Background = new PaperColour(0.43f, 0.16f, 0.85f, 1f) }).Children(UI.Text("B", new StyleSheet { Color = PaperColour.White })).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 64f, Height = 48f, Background = new PaperColour(0.36f, 0.13f, 0.71f, 1f) }).Children(UI.Text("C", new StyleSheet { Color = PaperColour.White })).Build())
                                    )
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "LambdaJSX") {
                var colors = new[] { "#ef4444", "#f97316", "#eab308", "#22c55e", "#06b6d4", "#6366f1", "#a855f7", "#ec4899" };
                var colorBoxes = new UINode[colors.Length];
                for (int i = 0; i < colors.Length; i++) {
                    var c = colors[i];
                    colorBoxes[i] = UI.Box(new PropsBuilder().Style(new StyleSheet { Width = 36f, Height = 36f, Background = new PaperColour(0, 0, 0, 1f) }).Build());
                }
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Lambda JSX — mapping array to elements", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, FlexWrap = FlexWrap.Wrap, Gap = 6f, MinHeight = 40f })
                                    .Children(colorBoxes)
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "Column") {
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Column: fixed header/footer + flexGrow middle", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, FlexDirection = FlexDirection.Column, Gap = 6f, FlexGrow = 1f, MinHeight = 120f })
                                    .Children(
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Height = 28f, Background = new PaperColour(0.86f, 0.15f, 0.15f, 1f) }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { FlexGrow = 1f, Background = new PaperColour(0.15f, 0.39f, 0.92f, 1f), MinHeight = 20f }).Build()),
                                        UI.Box(new PropsBuilder().Style(new StyleSheet { Height = 28f, Background = new PaperColour(0.09f, 0.64f, 0.29f, 1f) }).Build())
                                    )
                                    .Build()
                            )
                        )
                        .Build()
                );
            }
            else if (activeDemo == "FileDialog") {
                var (openedPath, setOpenedPath, _) = Hooks.UseState("(none)");
                var (savedPath, setSavedPath, _) = Hooks.UseState("(none)");
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(
                            UI.Text("Native File Dialogs", new StyleSheet { Color = colorC8C8E0, FontSize = 18f, MarginBottom = 16f }),
                            UI.Box(
                                new PropsBuilder()
                                    .Style(new StyleSheet { Display = Display.Flex, Gap = 8f })
                                    .Children(
                                        UI.Button("Open File", () => {}),
                                        UI.Button("Save File", () => {})
                                    )
                                    .Build()
                            ),
                            UI.Text("Opened: " + openedPath, new StyleSheet { Color = color7878A0, MarginTop = 12f }),
                            UI.Text("Saved: " + savedPath, new StyleSheet { Color = color7878A0, MarginTop = 4f })
                        )
                        .Build()
                );
            }
            else {
                demoContent = UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet { Padding = 20f })
                        .Children(UI.Text("Select a demo from the sidebar", new StyleSheet { Color = color7878A0 }))
                        .Build()
                );
            }
            
            UINode centerHeader = UI.Box(
                new PropsBuilder()
                    .Style(new StyleSheet {
                        Height = 30f,
                        Background = new PaperColour(0.145f, 0.145f, 0.27f, 1f),
                        Display = Display.Flex,
                        AlignItems = AlignItems.Center,
                        Padding = new Thickness(8, 0, 0, 0),
                        Cursor = Cursor.Grab,
                    })
                    .OnDragStart(e => { dragState[0] = "center"; dragState[1] = e.X; e.StopPropagation(); })
                    .OnDrag(e => { HandlePanelDragMove(e.X, leftWidth + rightWidth + 100f, 800f); })
                    .OnDragEnd(e => { HandlePanelDragEnd(); })
                    .Children(
                        UI.Text("Content", new StyleSheet { Color = colorC8C8E0, FlexGrow = 1f, FontSize = 13f }),
                        isMin ? null : headerButtons
                    )
                    .Build()
            );
            
            if (isMin) {
                return UI.Box(
                    new PropsBuilder()
                        .Style(new StyleSheet {
                            FlexGrow = 1f,
                            Height = 24f,
                            Background = new PaperColour(0.08f, 0.08f, 0.16f, 1f),
                            Border = new BorderEdges(new Border(1f, new PaperColour(0.3f, 0.3f, 0.4f, 1f))),
                            Display = Display.Flex,
                            AlignItems = AlignItems.Center,
                            Padding = new Thickness(8, 0, 8, 0),
                            Cursor = Cursor.Pointer,
                        })
                        .OnClick(() => ToggleMinimize("center"))
                        .Children(
                            UI.Text("Content", new StyleSheet { Color = color7878A0, FlexGrow = 1f, FontSize = 12f }),
                            UI.Box(new PropsBuilder()
                                .Style(new StyleSheet { Width = 22f, Height = 22f, Display = Display.Flex, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Cursor = Cursor.Pointer, BorderRadius = 3f, Color = color7878A0 })
                                .OnClick(() => ToggleMaximize("center"))
                                .Children(UI.Text("□", new StyleSheet { FontSize = 10f }))
                                .Build())
                        )
                        .Build()
                );
            }
            
            return UI.Box(
                new PropsBuilder()
                    .Style(new StyleSheet {
                        FlexGrow = 1f,
                        Display = Display.Flex,
                        FlexDirection = FlexDirection.Column,
                        Background = new PaperColour(0.08f, 0.08f, 0.12f, 1f),
                    })
                    .Children(
                        centerHeader,
                        UI.Box(
                            new PropsBuilder()
                                .Style(new StyleSheet { FlexGrow = 1f, Overflow = Overflow.Scroll })
                                .Children(demoContent)
                                .Build()
                        )
                    )
                    .Build()
            );
        }
        
        var orderedPanels = new List<(string id, UINode node)>();
        
        string leftPos = "left";
        string centerPos = "center";
        string rightPos = "right";
        
        if (panelPositions != null) {
            if (panelPositions.TryGetValue("left", out var lp)) leftPos = lp;
            if (panelPositions.TryGetValue("center", out var cp)) centerPos = cp;
            if (panelPositions.TryGetValue("right", out var rp)) rightPos = rp;
        }
        
        if (leftPos == "left") orderedPanels.Add(("left", BuildLeftPanel()));
        if (centerPos == "left") orderedPanels.Add(("center", BuildCenterPanel()));
        if (rightPos == "left") orderedPanels.Add(("right", BuildRightPanel()));
        
        if (leftPos == "center") orderedPanels.Add(("left", BuildLeftPanel()));
        if (centerPos == "center") orderedPanels.Add(("center", BuildCenterPanel()));
        if (rightPos == "center") orderedPanels.Add(("right", BuildRightPanel()));
        
        if (leftPos == "right") orderedPanels.Add(("left", BuildLeftPanel()));
        if (centerPos == "right") orderedPanels.Add(("center", BuildCenterPanel()));
        if (rightPos == "right") orderedPanels.Add(("right", BuildRightPanel()));
        
        var panelNodes = new UINode[orderedPanels.Count];
        for (int i = 0; i < orderedPanels.Count; i++) {
            panelNodes[i] = orderedPanels[i].node;
        }
        
        return UI.Box(
            new PropsBuilder()
                .Style(new StyleSheet { 
                    Display = Display.Flex, 
                    FlexDirection = FlexDirection.Column, 
                    Height = Length.Percent(100),
                    Width = Length.Percent(100),
                    Position = Position.Relative,
                })
                .Children(
                    UI.Box(
                        new PropsBuilder()
                            .Style(new StyleSheet { 
                                Height = 40f, 
                                Background = bg1a1a2e,
                                Position = Position.Absolute,
                                Top = 0f,
                                Left = 0f,
                                Right = 0f,
                            })
                            .Children(UI.Text("Paper UI", new StyleSheet { Color = colorC8C8E0, Padding = 16f }))
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .Style(new StyleSheet {
                                Position = Position.Absolute,
                                Top = 40f,
                                Bottom = 28f,
                                Left = 0f,
                                Right = 0f,
                                Display = Display.Flex,
                                FlexDirection = FlexDirection.Row,
                                Background = bgDark,
                            })
                            .OnPointerDownCapture(e => {
                                if ((bool)resizeState[3]) {
                                    resizeState[1] = e.X;
                                }
                            })
                            .OnPointerMoveCapture(e => {
                                HandleResizeMove(e.X);
                            })
                            .OnPointerUpCapture(e => {
                                HandleResizeEnd();
                            })
                            .Children(
                                panelNodes.Length > 0 ? panelNodes[0] : UI.Box(new PropsBuilder().Build()),
                                panelNodes.Length > 1 ? panelNodes[1] : UI.Box(new PropsBuilder().Build()),
                                panelNodes.Length > 2 ? panelNodes[2] : UI.Box(new PropsBuilder().Build())
                            )
                            .Build()
                    ),
                    UI.Box(
                        new PropsBuilder()
                            .Style(new StyleSheet { 
                                Height = 28f, 
                                Background = bg1a1a2e,
                                Position = Position.Absolute,
                                Bottom = 0f,
                                Left = 0f,
                                Right = 0f,
                            })
                            .Children(UI.Text("Status: Ready", new StyleSheet { Color = color7878A0, Padding = 16f }))
                            .Build()
                    )
                )
                .Build()
        );
    }
}

public static class Helpers
{
    public static Dictionary<K, V> MakeDict<K, V>((K key, V value)[] items) {
        var d = new Dictionary<K, V>();
        foreach (var item in items) {
            d[item.key] = item.value;
        }
        return d;
    }
}
