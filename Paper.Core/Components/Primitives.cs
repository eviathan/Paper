using System.Linq;
using Paper.Core.Hooks;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Core.Components
{
    /// <summary>
    /// Built-in primitive components (Select, etc.) for use from CSX or C#.
    /// </summary>
    public static class Primitives
    {
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
