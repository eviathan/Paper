using System.Linq;
using Paper.Core.Hooks;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Core.Components
{
    /// <summary>
    /// Built-in primitive components (Select, VirtualList, etc.) for use from CSX or C#.
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
