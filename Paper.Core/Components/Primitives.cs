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
            var items      = p.Get<IReadOnlyList<object>>("items") ?? Array.Empty<object>();
            float itemH    = p.Get<float?>("itemHeight") ?? 48f;
            float containerH = p.Get<float?>("containerH") ?? 400f;
            int   overscan = p.Get<int?>("overscan") ?? 3;
            var   render   = p.Get<Func<object, int, UINode>>("renderItem");
            var   style    = p.Style;

            var vs = Paper.Core.Hooks.Hooks.UseVirtualScroll<object>(items, itemH, containerH, overscan);

            var itemNodes = vs.VisibleItems
                .Select(x => render != null
                    ? render(x.item, x.index)
                    : UI.Text(x.item?.ToString() ?? "", key: x.index.ToString()))
                .ToArray();

            var containerStyle = new StyleSheet
            {
                Height   = Length.Px(containerH),
                Overflow = Overflow.Hidden,
                Display  = Display.Block,
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
                Position   = Position.Absolute,
                Bottom     = Length.Px(0),    // sits just above the trigger (positive y = up in screen coords)
                Left       = Length.Px(0),
                Background = new PaperColour(0.08f, 0.08f, 0.12f, 0.95f),
                Color      = PaperColour.White,
                Padding    = new Thickness(Length.Px(4), Length.Px(8)),
                BorderRadius = 4f,
                ZIndex     = 500,
                WhiteSpace = WhiteSpace.NoWrap,
                // Prevent tooltip from capturing pointer events so it doesn't flicker
                PointerEvents = PointerEvents.None,
                TranslateY = -4f, // nudge slightly above the trigger
            }.Merge(tooltipStyle ?? StyleSheet.Empty);

            var wrapperStyle = new StyleSheet
            {
                Position = Position.Relative,
                Display  = Display.InlineFlex,
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
                Position   = Position.Fixed,
                Top        = Length.Px(0),
                Left       = Length.Px(0),
                Width      = Length.Percent(100),
                Height     = Length.Percent(100),
                Background = new PaperColour(0f, 0f, 0f, 0.55f),
                ZIndex     = 1000,
                Display    = Display.Flex,
                JustifyContent = JustifyContent.Center,
                AlignItems     = AlignItems.Center,
            };

            var defaultPanelStyle = new StyleSheet
            {
                Background   = new PaperColour(0.13f, 0.13f, 0.18f, 1f),
                BorderRadius = 8f,
                Padding      = new Thickness(Length.Px(24)),
                MinWidth     = Length.Px(320),
                MaxWidth     = Length.Px(640),
                ZIndex       = 1001,
                Border       = new BorderEdges(new Border(1f, new PaperColour(0.3f, 0.3f, 0.4f, 1f))),
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
                    Top      = Length.Px(0),
                    Left     = Length.Px(0),
                    Width    = Length.Percent(100),
                    Height   = Length.Percent(100),
                    ZIndex   = 999,
                    Background = new PaperColour(0f, 0f, 0f, 0f),
                })
                .OnPointerDown(_ => onClose?.Invoke())
                .Build();

            var menuStyle = new StyleSheet
            {
                Position   = Position.Fixed,
                Top        = Length.Px(y),
                Left       = Length.Px(x),
                ZIndex     = 1000,
                Background = new PaperColour(0.12f, 0.12f, 0.17f, 1f),
                BorderRadius = 6f,
                Border     = new BorderEdges(new Border(1f, new PaperColour(0.3f, 0.3f, 0.4f, 1f))),
                MinWidth   = Length.Px(160),
                Padding    = new Thickness(Length.Px(4), Length.Px(0)),
                Display    = Display.Block,
            };

            var itemNodes = items.Select((item, i) =>
            {
                var itemStyle = new StyleSheet
                {
                    Display  = Display.Block,
                    Width    = Length.Percent(100),
                    Padding  = new Thickness(Length.Px(4), Length.Px(12)),
                    Cursor   = Cursor.Pointer,
                    Color    = PaperColour.White,
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
    }
}
