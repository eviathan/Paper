using System.Linq;
using Paper.Core.Events;
using Paper.Core.Hooks;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Core.Components
{
    /// <summary>
    /// Built-in primitive components (Select, VirtualList, Tooltip, Modal, ContextMenu, etc.) for use from CSX or C#.
    /// </summary>
    public static class Primitives
    {
        // ── VirtualList ───────────────────────────────────────────────────────

        /// <summary>
        /// Stable component delegate for the list — pass to <c>UI.Component</c>.
        /// Props (set via <see cref="UI.VirtualList{T}"/>):
        /// <list type="bullet">
        ///   <item><c>items</c> — <see cref="IReadOnlyList{T}"/> (boxed as object)</item>
        ///   <item><c>itemHeight</c> — float, height of every row in pixels</item>
        ///   <item><c>containerH</c> — float, visible container height in pixels</item>
        ///   <item><c>renderItem</c> — <c>Func&lt;object, int, UINode&gt;</c></item>
        ///   <item><c>overscan</c> — int, extra rows outside the viewport (default 3)</item>
        ///   <item><c>style</c> — optional <see cref="StyleSheet"/> applied to the container</item>
        /// </list>
        /// </summary>
        public static readonly Func<Props, UINode> ListComponent = List;

        /// <summary>
        /// Renders a scrollable container that only reconciles/draws the visible items.
        /// Handles wheel events internally — no external scroll container needed.
        /// </summary>
        public static UINode List(Props p)
        {
            var items = p.Get<IReadOnlyList<object>>("items") ?? Array.Empty<object>();
            float itemH = p.Get<float?>("itemHeight") ?? 48f;
            float containerH = p.Get<float?>("containerH") ?? 400f;
            int overscan = p.Get<int?>("overscan") ?? 3;
            var render = p.Get<Func<object, int, UINode>>("renderItem");
            var style = p.Style;

            var vs = Paper.Core.Hooks.Hooks.UseVirtualScroll<object>(items, itemH, containerH, overscan);

            var itemNodes = vs.VisibleItems
                .Select(x => render != null
                    ? render(x.item, x.index)
                    : UI.Text(x.item?.ToString() ?? "", key: x.index.ToString()))
                .ToArray();

            var containerStyle = new StyleSheet
            {
                Height = Length.Px(containerH),
                Overflow = Overflow.Hidden,
                Display = Display.Block,
            }.Merge(style ?? StyleSheet.Empty);

            return UI.Box(
                new PropsBuilder()
                    .Style(containerStyle)
                    .Set("onWheel", vs.OnWheel)
                    .Children(
                        UI.Box(
                            new StyleSheet { Height = Length.Px(vs.TotalHeight), Display = Display.Block },
                            UI.Box(
                                new StyleSheet { PaddingTop = Length.Px(vs.PaddingTop), Display = Display.Block },
                                itemNodes
                            )
                        )
                    )
                    .Build()
            );
        }

        // ── Tooltip ───────────────────────────────────────────────────────────

        /// <summary>
        /// Stable component delegate for the tooltip wrapper — pass to <c>UI.Component</c>.
        /// Props:
        /// <list type="bullet">
        ///   <item><c>children</c> — the trigger element(s) to wrap</item>
        ///   <item><c>text</c> — tooltip label to show on hover</item>
        ///   <item><c>style</c> — optional style for the tooltip bubble</item>
        /// </list>
        /// Usage: <c>UI.Tooltip("Save file", children: [UI.Button("Save")])</c>
        /// </summary>
        public static readonly Func<Props, UINode> TooltipComponent = Tooltip;

        /// <summary>
        /// Shows a short text label near the trigger element when the user hovers over it.
        /// </summary>
        public static UINode Tooltip(Props p)
        {
            var text = p.Get<string>("text") ?? "";
            var children = p.Children;
            var tooltipStyle = p.Style;

            var (visible, setVisible, _) = Hooks.Hooks.UseState(false);

            var bubbleStyle = new StyleSheet
            {
                Position = Position.Absolute,
                Bottom = Length.Px(0),    // sits just above the trigger (positive y = up in screen coords)
                Left = Length.Px(0),
                Background = new PaperColour(0.08f, 0.08f, 0.12f, 0.95f),
                Color = PaperColour.White,
                Padding = new Thickness(Length.Px(4), Length.Px(8)),
                BorderRadius = 4f,
                ZIndex = 500,
                WhiteSpace = WhiteSpace.NoWrap,
                // Prevent tooltip from capturing pointer events so it doesn't flicker
                PointerEvents = PointerEvents.None,
                TranslateY = -4f, // nudge slightly above the trigger
            }.Merge(tooltipStyle ?? StyleSheet.Empty);

            var wrapperStyle = new StyleSheet
            {
                Position = Position.Relative,
                Display = Display.InlineFlex,
            };

            var wrapperProps = new PropsBuilder()
                .Style(wrapperStyle)
                .OnPointerEnter(_ => setVisible(true))
                .OnPointerLeave(_ => setVisible(false))
                .Children(
                    UI.Nodes(
                        children.ToArray(),
                        visible && !string.IsNullOrEmpty(text)
                            ? UI.Box(bubbleStyle, UI.Text(text))
                            : null
                    )
                )
                .Build();

            return UI.Box(wrapperProps);
        }

        // ── Modal ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Stable component delegate for the modal dialog — pass to <c>UI.Component</c>.
        /// Props:
        /// <list type="bullet">
        ///   <item><c>isOpen</c> — bool: whether the modal is visible</item>
        ///   <item><c>onClose</c> — Action: called when the backdrop is clicked</item>
        ///   <item><c>children</c> — modal body content</item>
        ///   <item><c>style</c> — optional style overrides for the modal panel</item>
        /// </list>
        /// </summary>
        public static readonly Func<Props, UINode> ModalComponent = Modal;

        /// <summary>
        /// Full-screen modal overlay with a centered panel. Clicking the backdrop calls onClose.
        /// </summary>
        public static UINode Modal(Props p)
        {
            bool isOpen = p.Get<bool>("isOpen");
            var onClose = p.Get<Action>("onClose");
            var children = p.Children;
            var panelStyle = p.Style;

            if (!isOpen)
                return UI.Fragment();

            var backdropStyle = new StyleSheet
            {
                Position = Position.Fixed,
                Top = Length.Px(0),
                Left = Length.Px(0),
                Width = Length.Percent(100),
                Height = Length.Percent(100),
                Background = new PaperColour(0f, 0f, 0f, 0.55f),
                ZIndex = 1000,
                Display = Display.Flex,
                JustifyContent = JustifyContent.Center,
                AlignItems = AlignItems.Center,
            };

            var defaultPanelStyle = new StyleSheet
            {
                Background = new PaperColour(0.13f, 0.13f, 0.18f, 1f),
                BorderRadius = 8f,
                Padding = new Thickness(Length.Px(24)),
                MinWidth = Length.Px(320),
                MaxWidth = Length.Px(640),
                ZIndex = 1001,
                Border = new BorderEdges(new Border(1f, new PaperColour(0.3f, 0.3f, 0.4f, 1f))),
            }.Merge(panelStyle ?? StyleSheet.Empty);

            // Clicking the backdrop closes the modal.
            // The panel stops click propagation so clicks inside the panel don't close it.
            var panelProps = new PropsBuilder()
                .Style(defaultPanelStyle)
                .OnPointerClick(e => e.StopPropagation())
                .Children(children.ToArray())
                .Build();

            var backdropProps = new PropsBuilder()
                .Style(backdropStyle)
                .OnPointerClick(_ => onClose?.Invoke())
                .Children(UI.Box(panelProps))
                .Build();

            return UI.Box(backdropProps);
        }

        // ── ContextMenu ───────────────────────────────────────────────────────

        /// <summary>
        /// Stable component delegate for the context menu — pass to <c>UI.Component</c>.
        /// Props:
        /// <list type="bullet">
        ///   <item><c>isOpen</c> — bool</item>
        ///   <item><c>x</c> / <c>y</c> — float position in window pixels</item>
        ///   <item><c>onClose</c> — Action: called after an item is selected or backdrop clicked</item>
        ///   <item><c>items</c> — <see cref="IReadOnlyList{T}"/> of <c>(string Label, Action OnSelect)</c></item>
        /// </list>
        /// </summary>
        public static readonly Func<Props, UINode> ContextMenuComponent = ContextMenu;

        /// <summary>
        /// Positioned context menu — renders at (x, y) when open, floating above all content.
        /// </summary>
        public static UINode ContextMenu(Props p)
        {
            bool isOpen = p.Get<bool>("isOpen");
            float x = p.Get<float?>("x") ?? 0f;
            float y = p.Get<float?>("y") ?? 0f;
            var onClose = p.Get<Action>("onClose");
            var items = p.Get<IReadOnlyList<(string Label, Action? OnSelect)>>("items")
                ?? Array.Empty<(string, Action?)>();

            if (!isOpen || items.Count == 0)
                return UI.Fragment();

            // Invisible full-screen backdrop to capture outside clicks
            var backdropProps = new PropsBuilder()
                .Style(new StyleSheet
                {
                    Position = Position.Fixed,
                    Top = Length.Px(0),
                    Left = Length.Px(0),
                    Width = Length.Percent(100),
                    Height = Length.Percent(100),
                    ZIndex = 999,
                    Background = new PaperColour(0f, 0f, 0f, 0f),
                })
                .OnPointerDown(_ => onClose?.Invoke())
                .Build();

            var menuStyle = new StyleSheet
            {
                Position = Position.Fixed,
                Top = Length.Px(y),
                Left = Length.Px(x),
                ZIndex = 1000,
                Background = new PaperColour(0.12f, 0.12f, 0.17f, 1f),
                BorderRadius = 6f,
                Border = new BorderEdges(new Border(1f, new PaperColour(0.3f, 0.3f, 0.4f, 1f))),
                MinWidth = Length.Px(160),
                Padding = new Thickness(Length.Px(4), Length.Px(0)),
                Display = Display.Block,
            };

            var itemNodes = items.Select((item, i) =>
            {
                var itemStyle = new StyleSheet
                {
                    Display = Display.Block,
                    Width = Length.Percent(100),
                    Padding = new Thickness(Length.Px(4), Length.Px(12)),
                    Cursor = Cursor.Pointer,
                    Color = PaperColour.White,
                };
                var hoverStyle = new StyleSheet { Background = new PaperColour(0.25f, 0.4f, 0.7f, 1f) };
                var localItem = item;
                return UI.Box(
                    new PropsBuilder()
                        .Style(itemStyle)
                        .Set("hoverStyle", hoverStyle)
                        .OnPointerClick(_ => { localItem.OnSelect?.Invoke(); onClose?.Invoke(); })
                        .Children(UI.Text(localItem.Label))
                        .Build(),
                    key: i.ToString()
                );
            }).ToArray();

            return UI.Box(
                UI.Box(backdropProps),
                UI.Box(menuStyle, itemNodes)
            );
        }

        // ── Select ────────────────────────────────────────────────────────────

        /// <summary>
        /// Select component delegate for use with UI.Component(Primitives.SelectComponent, props).
        /// Props: options (IReadOnlyList&lt;(string Value, string Label)&gt;), selectedValue (string), onSelect (Action&lt;string&gt;), style (optional).
        /// </summary>
        public static readonly Func<Props, UINode> SelectComponent = Select;

        /// <summary>
        /// Dropdown select: button showing current label; when open, list of options. No portal (menu in flow).
        /// </summary>
        public static UINode Select(Props p)
        {
            var options = p.Options ?? Array.Empty<(string Value, string Label)>();
            var selectedValue = p.SelectedValue ?? "";
            var onSelect = p.OnSelect;
            var style = p.Style ?? StyleSheet.Empty;

            var (open, setOpen, _) = Paper.Core.Hooks.Hooks.UseState(false);
            var currentLabel = options.FirstOrDefault(o => o.Value == selectedValue).Label ?? selectedValue;
            var buttonLabel = currentLabel + " \u25BC"; // ▼ dropdown indicator

            var buttonStyle = (new StyleSheet { MinWidth = Length.Px(120), JustifyContent = JustifyContent.SpaceBetween }).Merge(style);

            var children = new List<UINode>
            {
                UI.Button(buttonLabel, () => setOpen(!open), buttonStyle),
            };
            if (open && options.Count > 0)
            {
                var optionButtons = options.Select(opt => (opt, onSelect)).Select(x =>
                {
                    var (opt, onSel) = x;
                    return UI.Button(opt.Label, () =>
                    {
                        onSel?.Invoke(opt.Value);
                        setOpen(false);
                    }, new StyleSheet
                    {
                        Display = Display.Block,
                        Background = opt.Value == selectedValue ? new PaperColour(0.2f, 0.35f, 0.6f, 1f) : null,
                        Width = Length.Percent(100),
                    });
                }).ToArray();
                children.Add(UI.Box(new StyleSheet
                {
                    Position = Position.Absolute,
                    Top = Length.Px(36),
                    Left = Length.Px(0),
                    Width = Length.Percent(100),
                    Display = Display.Block,
                    Background = new PaperColour(0.12f, 0.12f, 0.16f, 1f),
                    BorderRadius = 4f,
                    ZIndex = 100,
                    Border = new BorderEdges(new Border(1f, new PaperColour(0.3f, 0.3f, 0.4f, 1f))),
                }, optionButtons));
            }

            return UI.Box(new StyleSheet { Display = Display.Flex, FlexDirection = FlexDirection.Column, Position = Position.Relative }, children.ToArray());
        }

        // ── Slider ────────────────────────────────────────────────────────────

        /// <summary>Stable delegate for the Slider component.</summary>
        public static readonly Func<Props, UINode> SliderComponent = Slider;

        /// <summary>
        /// Horizontal range slider. Props: value (float), min (float), max (float), step (float),
        /// onChange (Action&lt;float&gt;), style (optional).
        /// </summary>
        public static UINode Slider(Props p)
        {
            float value = p.Get<float?>("value") ?? 0f;
            float min = p.Get<float?>("min") ?? 0f;
            float max = p.Get<float?>("max") ?? 100f;
            float step = p.Get<float?>("step") ?? 1f;
            var onChange = p.Get<Action<float>>("onChange");
            var style = p.Style ?? StyleSheet.Empty;

            // Clamp value
            float clamped = Math.Clamp(value, min, max);
            float fraction = (max - min) > 0 ? (clamped - min) / (max - min) : 0f;

            float Snap(float raw)
            {
                float snapped = MathF.Round((raw - min) / step) * step + min;
                return Math.Clamp(snapped, min, max);
            }

            void OnTrackDown(PointerEvent e)
            {
                float w = e.TargetWidth > 1f ? e.TargetWidth : (style.Width?.Resolve(0f) is float tw && tw > 1f ? tw : 200f);
                float frac = Math.Clamp(e.LocalX / w, 0f, 1f);
                onChange?.Invoke(Snap(min + frac * (max - min)));
            }

            // Button == 0 means this is a captured move (left button held) from PaperSurface,
            // not a plain hover move — so we only seek during actual drags.
            void OnTrackMove(Paper.Core.Events.PointerEvent e)
            {
                if (e.Button != 0) return;
                float w = e.TargetWidth > 1f ? e.TargetWidth : (style.Width?.Resolve(0f) is float tw && tw > 1f ? tw : 200f);
                float frac = Math.Clamp(e.LocalX / w, 0f, 1f);
                onChange?.Invoke(Snap(min + frac * (max - min)));
            }

            void OnWheel(Paper.Core.Events.PointerEvent e)
            {
                float delta = e.WheelDeltaY > 0 ? step : -step;
                float next = Snap(clamped + delta);
                onChange?.Invoke(next);
            }

            var trackStyle = new StyleSheet
            {
                Display = Display.Flex,
                AlignItems = AlignItems.Center,
                FlexGrow = 1f,
                Height = Length.Px(24),
                Position = Position.Relative,
                Cursor = Cursor.Pointer,
                MinWidth = Length.Px(80),
            }.Merge(style);

            var railStyle = new StyleSheet
            {
                Width = Length.Percent(100),
                Height = Length.Px(4),
                Background = new PaperColour(0.3f, 0.3f, 0.4f, 1f),
                BorderRadius = 2f,
                Position = Position.Relative,
            };

            var fillStyle = new StyleSheet
            {
                Width = Length.Percent(fraction * 100f),
                Height = Length.Px(4),
                Background = new PaperColour(0.35f, 0.6f, 0.95f, 1f),
                BorderRadius = 2f,
                Position = Position.Absolute,
                Left = Length.Px(0),
            };

            float thumbOffset = fraction * 100f;
            var thumbStyle = new StyleSheet
            {
                Width = Length.Px(16),
                Height = Length.Px(16),
                Background = new PaperColour(0.35f, 0.6f, 0.95f, 1f),
                BorderRadius = 8f,
                Position = Position.Absolute,
                Left = Length.Percent(thumbOffset),
                Top = Length.Px(-6),  // centre 16px thumb in 4px rail: (4-16)/2 = -6
                TranslateX = -8f,  // centre thumb on the point
                Cursor = Cursor.Pointer,
                PointerEvents = PointerEvents.None,  // clicks fall through to the rail/track
            };

            var trackProps = new PropsBuilder()
                .Style(trackStyle)
                .OnPointerDown(OnTrackDown)
                .OnPointerMove(OnTrackMove)
                .Set("onWheel", (Action<Paper.Core.Events.PointerEvent>)OnWheel)
                .Children(
                    UI.Box(new PropsBuilder()
                        .Style(railStyle)
                        .Children(
                            UI.Box(fillStyle),
                            UI.Box(thumbStyle)
                        )
                        .Build())
                )
                .Build();

            return UI.Box(trackProps);
        }

        // ── NumberInput ───────────────────────────────────────────────────────

        /// <summary>Stable delegate for the NumberInput component.</summary>
        public static readonly Func<Props, UINode> NumberInputComponent = NumberInput;

        /// <summary>
        /// Numeric text input with increment/decrement buttons.
        /// Props: value (float), min (float?), max (float?), step (float), onChange (Action&lt;float&gt;), style.
        /// </summary>
        public static UINode NumberInput(Props p)
        {
            float value = p.Get<float?>("value") ?? 0f;
            float? min = p.Get<float?>("min");
            float? max = p.Get<float?>("max");
            float step = p.Get<float?>("step") ?? 1f;
            var onChange = p.Get<Action<float>>("onChange");
            var style = p.Style ?? StyleSheet.Empty;

            float Clamp(float v)
            {
                if (min.HasValue) v = Math.Max(v, min.Value);
                if (max.HasValue) v = Math.Min(v, max.Value);
                return v;
            }

            string FormatValue(float v) => v == MathF.Floor(v) ? ((int)v).ToString() : v.ToString("G6");

            void Decrement() => onChange?.Invoke(Clamp(value - step));
            void Increment() => onChange?.Invoke(Clamp(value + step));

            // Mutable drag state via UseStable — avoids stale-closure issues that UseState has
            // (state only updates after re-render, so Move events would always see the initial null).
            // [0] = drag start X (-1 = not dragging), [1] = value at drag start
            var drag = Hooks.Hooks.UseStable(() => new float[] { -1f, 0f });

            // Pixels of horizontal drag required to change by one step.
            const float PixelsPerStep = 4f;

            void OnScrubDown(PointerEvent e)
            {
                drag[0] = e.X;
                drag[1] = value;
            }

            void OnScrubMove(PointerEvent e)
            {
                if (e.Button != 0 || drag[0] < 0) return;
                float delta = e.X - drag[0];
                float steps = MathF.Round(delta / PixelsPerStep);
                onChange?.Invoke(Clamp(drag[1] + steps * step));
            }

            void OnScrubUp(PointerEvent e)
            {
                drag[0] = -1f;
            }

            var containerStyle = new StyleSheet
            {
                Display = Display.Flex,
                FlexDirection = FlexDirection.Row,
                AlignItems = AlignItems.Stretch,
                FlexGrow = 1f,
                MinHeight = Length.Em(2.2f),
            }.Merge(style);

            var decBtnStyle = new StyleSheet
            {
                Width = Length.Px(28),
                Display = Display.Flex,
                JustifyContent = JustifyContent.Center,
                AlignItems = AlignItems.Center,
                Background = new PaperColour(0.2f, 0.2f, 0.28f, 1f),
                BorderTopLeftRadius = 4f,
                BorderBottomLeftRadius = 4f,
                Cursor = Cursor.Pointer,
                TextAlign = TextAlign.Center,
            };
            var incBtnStyle = new StyleSheet
            {
                Width = Length.Px(28),
                Display = Display.Flex,
                JustifyContent = JustifyContent.Center,
                AlignItems = AlignItems.Center,
                Background = new PaperColour(0.2f, 0.2f, 0.28f, 1f),
                BorderTopRightRadius = 4f,
                BorderBottomRightRadius = 4f,
                Cursor = Cursor.Pointer,
                TextAlign = TextAlign.Center,
            };
            var btnHover = new StyleSheet { Background = new PaperColour(0.28f, 0.28f, 0.38f, 1f) };

            var scrubStyle = new StyleSheet
            {
                FlexGrow = 1f,
                Display = Display.Flex,
                AlignItems = AlignItems.Center,
                JustifyContent = JustifyContent.Center,
                Background = new PaperColour(0.10f, 0.10f, 0.14f, 1f),
                Cursor = Cursor.EwResize,
                Padding = new Thickness(Length.Px(4), Length.Px(6)),
            };

            return UI.Box(containerStyle,
                UI.Box(new PropsBuilder().Style(decBtnStyle).Set("hoverStyle", btnHover).OnClick(Decrement).Children(UI.Text("-")).Build()),
                UI.Box(new PropsBuilder()
                    .Style(scrubStyle)
                    .OnPointerDown(OnScrubDown)
                    .OnPointerMove(OnScrubMove)
                    .OnPointerUp(OnScrubUp)
                    .Children(UI.Text(FormatValue(value), style: new StyleSheet { PointerEvents = PointerEvents.None }))
                    .Build()),
                UI.Box(new PropsBuilder().Style(incBtnStyle).Set("hoverStyle", btnHover).OnClick(Increment).Children(UI.Text("+")).Build())
            );
        }

        // ── Tabs ──────────────────────────────────────────────────────────────

        /// <summary>Stable delegate for the Tabs component.</summary>
        public static readonly Func<Props, UINode> TabsComponent = Tabs;

        /// <summary>
        /// Tab strip with panel switching.
        /// Props: tabs (IReadOnlyList&lt;(string Id, string Label)&gt;), activeTab (string),
        ///        onTabChange (Action&lt;string&gt;), children (panels, one per tab), style.
        /// </summary>
        public static UINode Tabs(Props p)
        {
            var tabs = p.Get<IReadOnlyList<(string Id, string Label)>>("tabs") ?? Array.Empty<(string, string)>();
            var activeTab = p.Get<string>("activeTab") ?? (tabs.Count > 0 ? tabs[0].Id : "");
            var onChange = p.Get<Action<string>>("onTabChange");
            var panels = p.Children;
            var style = p.Style ?? StyleSheet.Empty;

            var stripStyle = new StyleSheet
            {
                Display = Display.Flex,
                FlexDirection = FlexDirection.Row,
                Height = Length.Px(36),
                BorderBottom = new Border(1f, new PaperColour(0.25f, 0.25f, 0.35f, 1f)),
            };

            var hoverStyle = new StyleSheet { Opacity = 0.5f };
            var tabNodes = tabs.Select((tab, i) =>
            {
                bool active = tab.Id == activeTab;
                var tabStyle = new StyleSheet
                {
                    Padding = new Thickness(Length.Px(8), Length.Px(16)),
                    Cursor = Cursor.Pointer,
                    BorderBottom = active ? new Border(2f, new PaperColour(0.35f, 0.6f, 0.95f, 1f)) : null,
                    MarginBottom = active ? Length.Px(-1) : null,
                };
                var localTab = tab;
                return UI.Box(new PropsBuilder()
                    .Style(tabStyle)
                    .Set("hoverStyle", hoverStyle)
                    .OnClick(() => onChange?.Invoke(localTab.Id))
                    .Children(UI.Text(localTab.Label, style: new StyleSheet { PointerEvents = PointerEvents.None, Display = Display.Block, TextAlign = TextAlign.Center }))
                    .Build(), key: tab.Id);
            }).ToArray();

            // Show the panel whose index matches the active tab
            int activeIndex = tabs.ToList().FindIndex(t => t.Id == activeTab);
            UINode? activePanel = (activeIndex >= 0 && activeIndex < panels.Count)
                ? panels[activeIndex]
                : UI.Fragment();

            var containerStyle = new StyleSheet
            {
                Display = Display.Flex,
                FlexDirection = FlexDirection.Column,
            }.Merge(style);

            return UI.Box(containerStyle,
                UI.Box(stripStyle, tabNodes),
                activePanel ?? UI.Fragment()
            );
        }

        // ── Popover ───────────────────────────────────────────────────────────

        /// <summary>Stable delegate for the Popover component.</summary>
        public static readonly Func<Props, UINode> PopoverComponent = Popover;

        /// <summary>
        /// Interactive floating panel anchored below its trigger element.
        /// Props: isOpen (bool), onClose (Action), trigger (UINode — the first child is used as trigger),
        ///        children (panel content), placement ('bottom'|'top'|'right'|'left'), style.
        /// </summary>
        public static UINode Popover(Props p)
        {
            bool isOpen = p.Get<bool>("isOpen");
            var onClose = p.Get<Action>("onClose");
            var children = p.Children;
            var style = p.Style ?? StyleSheet.Empty;
            var placement = p.Get<string>("placement") ?? "bottom";

            // First child = trigger, rest = panel content
            UINode trigger = children.Count > 0 ? children[0] : UI.Fragment();
            UINode[] content = children.Count > 1 ? children.Skip(1).ToArray() : Array.Empty<UINode>();

            var wrapperStyle = new StyleSheet
            {
                Position = Position.Relative,
                Display = Display.InlineFlex,
            };

            bool isCentered = string.Equals(placement, "center", StringComparison.OrdinalIgnoreCase);

            StyleSheet panelPosition = isCentered
                ? new StyleSheet { Position = Position.Fixed }  // centered via backdrop flex container
                : placement switch
                {
                    "top"   => new StyleSheet { Position = Position.Absolute, Bottom = Length.Percent(100), Left = Length.Px(0) },
                    "right" => new StyleSheet { Position = Position.Absolute, Top = Length.Px(0), Left = Length.Percent(100) },
                    "left"  => new StyleSheet { Position = Position.Absolute, Top = Length.Px(0), Right = Length.Percent(100) },
                    _       => new StyleSheet { Position = Position.Absolute, Top = Length.Percent(100), Left = Length.Px(0) },  // bottom
                };

            var panelStyle = new StyleSheet
            {
                ZIndex = 200,
                Background = new PaperColour(0.12f, 0.12f, 0.17f, 1f),
                Border = new BorderEdges(new Border(1f, new PaperColour(0.3f, 0.3f, 0.4f, 1f))),
                BorderRadius = 6f,
                Padding = new Thickness(Length.Px(8)),
                MinWidth = Length.Px(160),
                Display = Display.Flex,
                FlexDirection = FlexDirection.Column,
            }.Merge(isCentered ? new StyleSheet() : panelPosition).Merge(style);

            var children2 = new List<UINode> { trigger };
            if (isOpen)
            {
                // Backdrop captures outside clicks and (for centered) centers the panel via flex.
                var backdropStyle = new StyleSheet
                {
                    Position = Position.Fixed,
                    Top = Length.Px(0),
                    Left = Length.Px(0),
                    Width = Length.Percent(100),
                    Height = Length.Percent(100),
                    ZIndex = 199,
                    Background = new PaperColour(0f, 0f, 0f, isCentered ? 0.4f : 0f),
                    Display = isCentered ? Display.Flex : Display.Block,
                    AlignItems = isCentered ? AlignItems.Center : (AlignItems?)null,
                    JustifyContent = isCentered ? JustifyContent.Center : (JustifyContent?)null,
                };

                var panel = UI.Box(new PropsBuilder()
                    .Style(panelStyle)
                    .OnPointerDown(e => e.StopPropagation())
                    .Children(content)
                    .Build());

                var backdrop = UI.Box(new PropsBuilder()
                    .Style(backdropStyle)
                    .OnPointerDown(_ => onClose?.Invoke())
                    .Children(isCentered ? new[] { panel } : Array.Empty<UINode>())
                    .Build());

                // For centered mode, render the backdrop via a Portal so it sits above all other
                // content in both rendering and hit-testing (portals are processed after the main tree).
                children2.Add(isCentered ? UI.Portal(backdrop) : backdrop);
                if (!isCentered)
                    children2.Add(panel);
            }

            return UI.Box(new PropsBuilder()
                .Style(wrapperStyle)
                .Children(children2.ToArray())
                .Build());
        }

        // ── Toast ─────────────────────────────────────────────────────────────

        /// <summary>Stable delegate for the ToastContainer component.</summary>
        public static readonly Func<Props, UINode> ToastContainerComponent = ToastContainer;

        /// <summary>A single toast entry.</summary>
        /// <param name="Duration">Auto-dismiss after this many seconds. 0 (default) = never auto-dismiss.</param>
        public sealed record ToastEntry(string Id, string Message, string Variant = "info", float Duration = 0f);

        /// <summary>Stable delegate for rendering a single toast item (manages its own auto-dismiss timer).</summary>
        public static readonly Func<Props, UINode> ToastItemComponent = ToastItem;

        /// <summary>Renders a single toast item. Uses UseStable to schedule auto-dismiss on first render only.</summary>
        public static UINode ToastItem(Props p)
        {
            var toast = p.Get<ToastEntry>("toast")!;
            var onDismiss = p.Get<Action<string>>("onDismiss");

            // Schedule auto-dismiss exactly once (UseStable runs the factory only on the first render).
            if (toast.Duration > 0)
            {
                var capturedId = toast.Id;
                var capturedDismiss = onDismiss;
                float capturedMs = toast.Duration * 1000f;
                Hooks.Hooks.UseStable(() =>
                {
                    _ = System.Threading.Tasks.Task.Delay((int)capturedMs)
                        .ContinueWith(_ => capturedDismiss?.Invoke(capturedId));
                    return 0;
                });
            }

            PaperColour accent = toast.Variant switch
            {
                "success" => new PaperColour(0.1f, 0.55f, 0.2f, 1f),
                "error" => new PaperColour(0.7f, 0.15f, 0.15f, 1f),
                "warning" => new PaperColour(0.7f, 0.45f, 0.0f, 1f),
                _ => new PaperColour(0.15f, 0.35f, 0.65f, 1f),
            };

            var toastStyle = new StyleSheet
            {
                Display = Display.Flex,
                FlexDirection = FlexDirection.Row,
                AlignItems = AlignItems.Stretch,
                Background = new PaperColour(0.12f, 0.12f, 0.18f, 0.96f),
                Border = new BorderEdges(new Border(1f, accent)),
                BorderRadius = 6f,
                Padding = new Thickness(Length.Px(10), Length.Px(8), Length.Px(10), Length.Px(14)),
                Width = Length.Px(340),
            };

            var localId = toast.Id;
            return UI.Box(new PropsBuilder()
                .Style(toastStyle)
                .Children(
                    UI.Box(
                        new StyleSheet
                        { 
                            Display = Display.Flex,
                            FlexDirection = FlexDirection.Row,
                            AlignItems = AlignItems.Center,
                            FlexGrow = 1
                        },
                        UI.Text(toast.Message)
                    ),
                    UI.Button(
                        "❌",
                        () => onDismiss?.Invoke(localId), 
                        new StyleSheet
                        {
                            Background = new PaperColour(0f, 0f, 0f, 0.3f),
                            Color = new PaperColour(0.7f, 0.7f, 0.8f, 1f),
                            Width = Length.Px(24),
                            Height = Length.Px(24),
                            AlignSelf = AlignSelf.Center,
                            BorderRadius = 4f,
                            MarginLeft = Length.Px(12),
                            Padding = new Thickness(Length.Px(0)),
                            TextAlign = TextAlign.Center,
                        }
                    )
                )
                .Build());
        }

        /// <summary>
        /// Renders all active toasts in the top-right corner.
        /// Props: toasts (IReadOnlyList&lt;ToastEntry&gt;), onDismiss (Action&lt;string&gt;).
        /// </summary>
        public static UINode ToastContainer(Props p)
        {
            var toasts = p.Get<IReadOnlyList<ToastEntry>>("toasts") ?? Array.Empty<ToastEntry>();
            var onDismiss = p.Get<Action<string>>("onDismiss");

            if (toasts.Count == 0) return UI.Fragment();

            // Fixed width so Right-anchoring positions correctly (right-edge at viewport.right - 16).
            var containerStyle = new StyleSheet
            {
                Position = Position.Fixed,
                Top = Length.Px(16),
                Right = Length.Px(16),
                Width = Length.Px(360),
                Display = Display.Flex,
                FlexDirection = FlexDirection.Column,
                Gap = Length.Px(6),
                ZIndex = 2000,
            };

            var toastNodes = toasts.Select(t =>
                UI.Component(ToastItemComponent,
                    new PropsBuilder()
                        .Set("toast", t)
                        .Set("onDismiss", onDismiss)
                        .Build(),
                    key: t.Id)
            ).ToArray();

            return UI.Box(containerStyle, toastNodes);
        }
    }
}
