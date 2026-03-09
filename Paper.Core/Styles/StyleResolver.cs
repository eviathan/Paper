using Paper.Core.Reconciler;
using Paper.Core.VirtualDom;

namespace Paper.Core.Styles
{
    public static class StyleResolver
    {
        public static StyleSheet Resolve(object type, Props props, StyleRegistry? registry, InteractionState state,
            Fiber? fiber = null)
        {
            var resolved = DefaultForType(type);

            // CSSS sheet matching (element, class, id, descendant selectors from .csss files).
            // Runs before the class registry and inline style so inline always wins.
            if (fiber != null && registry != null && registry.CSSSSheets.Count > 0)
            {
                foreach (var sheetObj in registry.CSSSSheets)
                {
                    if (sheetObj is ICSSSSheetMatcher matcher)
                    {
                        var csssStyle = matcher.Match(fiber, state);
                        if (csssStyle != StyleSheet.Empty)
                            resolved = resolved.Merge(csssStyle);
                    }
                }
            }

            // Class styles (component-scoped classes are already encoded in the className tokens).
            if (registry != null && props.ClassName is { Length: > 0 } cls)
            {
                foreach (var token in cls.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (registry.TryGetClass(token, out var s))
                        resolved = resolved.Merge(s);

                    // Variant selectors encoded as suffixes.
                    if (state.Hover && registry.TryGetClass(token + ":hover", out var hover))
                        resolved = resolved.Merge(hover);
                    if (state.Active && registry.TryGetClass(token + ":active", out var active))
                        resolved = resolved.Merge(active);
                    if (state.Focus && registry.TryGetClass(token + ":focus", out var focus))
                        resolved = resolved.Merge(focus);
                }
            }

            // Inline style.
            if (props.Style != null)
                resolved = resolved.Merge(props.Style);

            // Inline interaction-state styles (override inline style when state matches).
            if (state.Hover  && props.HoverStyle  != null) resolved = resolved.Merge(props.HoverStyle);
            if (state.Active && props.ActiveStyle != null) resolved = resolved.Merge(props.ActiveStyle);
            if (state.Focus  && props.FocusStyle  != null) resolved = resolved.Merge(props.FocusStyle);

            return resolved;
        }

        /// <summary>Minimal interface used to avoid Paper.Core depending on Paper.CSSS directly.</summary>
        public interface ICSSSSheetMatcher
        {
            StyleSheet Match(Fiber fiber, InteractionState state);
        }

        private static StyleSheet DefaultForType(object type)
        {
            if (type is not string t) return StyleSheet.Empty;

            return t switch
            {
                ElementTypes.Box => new StyleSheet
                {
                    Display = Display.Block,
                },
                ElementTypes.Button => new StyleSheet
                {
                    Display = Display.InlineFlex,
                    Padding = new Thickness(Length.Px(8), Length.Px(12)),
                    Background = new PaperColour(0.18f, 0.18f, 0.24f, 1f),
                    Color = PaperColour.White,
                    BorderRadius = 6f,
                    Cursor = Cursor.Pointer,
                    FlexShrink = 0f, // Size to content; don't shrink below intrinsic size
                },
                ElementTypes.Input => new StyleSheet
                {
                    Display = Display.Block,
                    Width = Length.Percent(100),
                    MinHeight = Length.Em(2.2f), // Slightly taller than one line (font-size + padding)
                    Padding = new Thickness(Length.Px(6), Length.Px(10)),
                    Background = new PaperColour(0.10f, 0.10f, 0.14f, 1f),
                    Color = PaperColour.White,
                    BorderRadius = 4f,
                    Cursor = Cursor.Text,
                },
                ElementTypes.Text => new StyleSheet
                {
                    Display = Display.Inline,
                    Color = PaperColour.White,
                },
                ElementTypes.RadioGroup => new StyleSheet
                {
                    Display = Display.Flex,
                    FlexDirection = FlexDirection.Column,
                },
                ElementTypes.RadioOption => new StyleSheet
                {
                    Display = Display.InlineFlex,
                    Padding = new Thickness(Length.Px(4), Length.Px(6)),
                    Cursor = Cursor.Pointer,
                    FlexShrink = 0f,
                    MinWidth = Length.Px(24), // Reserve space for circle so it doesn't clip
                },
                ElementTypes.Table => new StyleSheet
                {
                    Display = Display.Block,
                    MinHeight = Length.Px(40),
                    Border = new BorderEdges(new Border(1f, new PaperColour(0.35f, 0.35f, 0.4f, 1f))),
                    BorderRadius = 4f,
                },
                ElementTypes.TableRow => new StyleSheet
                {
                    Display = Display.Flex,
                    FlexDirection = FlexDirection.Row,
                    MinHeight = Length.Px(24),
                },
                ElementTypes.TableCell => new StyleSheet
                {
                    Display = Display.Block,
                    Padding = new Thickness(Length.Px(6), Length.Px(8)),
                    MinWidth = Length.Px(32),
                    FlexGrow = 1f,
                    Border = new BorderEdges(new Border(1f, new PaperColour(0.25f, 0.25f, 0.3f, 1f))),
                },
                ElementTypes.Textarea => new StyleSheet
                {
                    Display = Display.Block,
                    Width = Length.Percent(100),
                    MinHeight = Length.Em(2.2f),
                    Padding = new Thickness(Length.Px(6), Length.Px(10)),
                    Background = new PaperColour(0.10f, 0.10f, 0.14f, 1f),
                    Color = PaperColour.White,
                    BorderRadius = 4f,
                    Cursor = Cursor.Text,
                },
                ElementTypes.Checkbox => new StyleSheet
                {
                    Display = Display.InlineFlex,
                    Padding = new Thickness(Length.Px(4), Length.Px(6)),
                    Cursor = Cursor.Pointer,
                    FlexShrink = 0f,
                    MinWidth = Length.Px(24), // Reserve space for checkbox box so it doesn't clip
                },
                ElementTypes.Image => new StyleSheet
                {
                    Display = Display.Block,
                    Width = Length.Px(100),
                    Height = Length.Px(100),
                    MinWidth = Length.Px(1),
                    MinHeight = Length.Px(1),
                },
                ElementTypes.Scroll => new StyleSheet
                {
                    Display = Display.Block,
                    OverflowX = Overflow.Auto,
                    OverflowY = Overflow.Scroll,
                },
                _ => StyleSheet.Empty,
            };
        }
    }
}

