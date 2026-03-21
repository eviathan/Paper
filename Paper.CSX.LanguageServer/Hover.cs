using System.Text.RegularExpressions;

namespace Paper.CSX.LanguageServer
{
    internal static class Hover
    {
        private static readonly Dictionary<string, string> ElementDocs = new(StringComparer.Ordinal)
        {
            ["Box"] = "**Box** — flex container (`display: flex` by default). Accepts any layout or visual style props.",
            ["Text"] = "**Text** — renders a text node. Supports `fontSize`, `fontWeight`, `color`, `textAlign`, etc.",
            ["Button"] = "**Button** — interactive button. Use `onClick` to handle clicks; style with `background`, `color`, `borderRadius`.",
            ["Input"] = "**Input** — single-line text input. Bind with `value` + `onChange={(v) => ...}`.",
            ["Textarea"] = "**Textarea** — multi-line text input. Bind with `value` + `onChange={(v) => ...}`. Accepts `rows`.",
            ["Scroll"] = "**Scroll** — scrollable container. Children overflow inside a scroll region.",
            ["Image"] = "**Image** — renders an image. Set `src` to a file path or URL.",
            ["Checkbox"] = "**Checkbox** — boolean toggle. Bind with `checked` + `onCheckedChange={(v) => ...}`.",
            ["Table"] = "**Table** — grid table container.",
            ["TableRow"] = "**TableRow** — a row inside a Table.",
            ["TableCell"] = "**TableCell** — a cell inside a TableRow.",
            ["RadioGroup"] = "**RadioGroup** — radio button group. Bind with `selectedValue` + `onSelect={(v) => ...}`.",
            ["RadioOption"] = "**RadioOption** — option inside a RadioGroup. Set `value`.",
            ["Fragment"] = "**Fragment** — groups children without adding a DOM node.",
            ["Viewport"] = "**Viewport** — full-screen root container.",
            ["Modal"] = "**Modal** — overlay dialog. Use `open` + `onClose` to control visibility.",
            ["Select"] = "**Select** — dropdown selector. Bind with `value` + `onChange={(v) => ...}`.",
            ["Slider"] = "**Slider** — range input. Bind with `value` + `onChange={(v) => ...}`. Set `min`, `max`, `step`.",
            ["Tabs"] = "**Tabs** — tabbed container. Bind with `activeTab` + `onTabChange={(t) => ...}`.",
            ["Tooltip"] = "**Tooltip** — floating label on hover. Set `content` for the tooltip text.",
            ["Popover"] = "**Popover** — floating panel anchored to a trigger. Use `open` + `onClose`.",
            ["Toast"] = "**Toast** — transient notification. Set `message`, `variant` (`info`|`success`|`error`|`warning`).",
            ["List"] = "**List** — virtual-scrolling list for large datasets. Set `items`, `itemHeight`, `containerH`, `renderItem`.",
            ["ContextMenu"] = "**ContextMenu** — right-click context menu. Provide `items` array.",
            ["NumberInput"] = "**NumberInput** — numeric input with increment/decrement. Bind with `value` + `onChange`.",
            ["ToastContainer"] = "**ToastContainer** — renders a stack of Toast notifications. Pass `toasts` array.",
            ["ToastItem"] = "**ToastItem** — individual toast item inside a ToastContainer.",
            ["Radio"] = "**Radio** — standalone radio button. Bind with `checked` + `onChange`.",
            ["Switch"] = "**Switch** — toggle switch. Bind with `checked` + `onChange={(v) => ...}`.",
            ["Progress"] = "**Progress** — progress bar. Set `value` (0–100) and optional `max`.",
            ["Avatar"] = "**Avatar** — user avatar. Set `src` for image or `initials` for text fallback.",
            ["Badge"] = "**Badge** — small count/status indicator. Set `count` or `dot`.",
            ["Card"] = "**Card** — elevated container with default padding and border-radius.",
            ["Divider"] = "**Divider** — horizontal or vertical separator line.",
            ["Icon"] = "**Icon** — icon element. Set `name` for the icon identifier.",
            ["ImageList"] = "**ImageList** — grid of images. Provide `items` with `src`.",
            ["ListItem"] = "**ListItem** — single row inside a List or plain container.",
            ["Accordion"] = "**Accordion** — collapsible section. Set `title` + `expanded` + `onToggle`.",
        };

        private static readonly Dictionary<string, string> HookDocs = new(StringComparer.Ordinal)
        {
            ["UseState"] = "**Hooks.UseState\\<T\\>(initial)** — Returns `(value, setState, updateState)`. Call `setState(newValue)` to trigger a re-render.",
            ["UseEffect"] = "**Hooks.UseEffect(action, deps)** — Runs `action` after render whenever a dep changes. Return a cleanup action.",
            ["UseRef"] = "**Hooks.UseRef\\<T\\>(initial)** — Mutable ref that persists across renders without causing re-renders.",
            ["UseReducer"] = "**Hooks.UseReducer(reducer, initial)** — Returns `(state, dispatch)`. Dispatch an action to update state via the reducer.",
            ["UseContext"] = "**Hooks.UseContext\\<T\\>(context)** — Reads the nearest Context provider value.",
            ["UseCallback"] = "**Hooks.UseCallback(fn, deps)** — Returns a stable function reference that only changes when deps change.",
            ["UseStable"] = "**Hooks.UseStable(factory)** — Returns the same instance every render (UseMemo with empty deps).",
            ["UseMemo"] = "**Hooks.UseMemo(factory, deps)** — Memoises an expensive computation; recomputes when deps change.",
        };

        private static readonly Dictionary<string, string> CssPropDocs;

        static Hover()
        {
            CssPropDocs = new Dictionary<string, string>(StringComparer.Ordinal);
            // Map both camelCase (CSX) and kebab-case (CSSS) to documentation
            var entries = new (string css, string detail)[]
            {
                ("display", "How the element is rendered. Common values: `flex`, `grid`, `block`, `none`."),
                ("flexDirection", "Main axis direction: `row` (default) | `column` | `row-reverse` | `column-reverse`."),
                ("flexWrap", "Whether children wrap: `nowrap` | `wrap` | `wrap-reverse`."),
                ("flex", "Shorthand for `flex-grow flex-shrink flex-basis`."),
                ("flexGrow", "How much the item grows relative to siblings. `0` = no grow (default)."),
                ("flexShrink", "How much the item shrinks. `1` = shrink (default), `0` = no shrink."),
                ("justifyContent", "Alignment on main axis: `flex-start` | `center` | `flex-end` | `space-between` | `space-around` | `space-evenly`."),
                ("alignItems", "Alignment on cross axis: `stretch` (default) | `center` | `flex-start` | `flex-end` | `baseline`."),
                ("alignSelf", "Override cross-axis alignment for this item."),
                ("alignContent", "Multi-line cross-axis alignment."),
                ("gap", "Space between flex/grid children (px)."),
                ("rowGap", "Vertical gap between rows (px)."),
                ("columnGap", "Horizontal gap between columns (px)."),
                ("gridTemplateColumns", "Grid column track definitions, e.g. `'1fr 1fr'` or `'200px auto'`."),
                ("gridTemplateRows", "Grid row track definitions."),
                ("gridColumn", "Column span, e.g. `'1 / 3'` to span columns 1–2."),
                ("gridRow", "Row span, e.g. `'1 / 3'`."),
                ("width", "Element width in px or %. `auto` = shrink-to-fit."),
                ("height", "Element height in px or %. `auto` = shrink-to-fit."),
                ("minWidth", "Minimum width constraint (px or %)."),
                ("minHeight", "Minimum height constraint (px or %)."),
                ("maxWidth", "Maximum width constraint (px or %)."),
                ("maxHeight", "Maximum height constraint (px or %)."),
                ("padding", "Inner spacing on all sides (px). Also `paddingTop/Right/Bottom/Left`."),
                ("margin", "Outer spacing on all sides (px). Also `marginTop/Right/Bottom/Left`."),
                ("background", "Background color. Accepts `#rrggbb`, `rgba(r,g,b,a)`, or a named color."),
                ("backgroundColor", "Background color alias. Accepts `#rrggbb`, `rgba(r,g,b,a)`, or a named color."),
                ("color", "Text color. Accepts `#rrggbb`, `rgba(r,g,b,a)`, or a named color."),
                ("opacity", "Element opacity, 0.0 (transparent) to 1.0 (opaque)."),
                ("fontSize", "Font size in px or em."),
                ("fontWeight", "`normal` (400) or `bold` (700)."),
                ("fontFamily", "Font family name."),
                ("lineHeight", "Line height multiplier (e.g. `1.5`)."),
                ("textAlign", "`left` | `center` | `right`."),
                ("textOverflow", "`ellipsis` — clip overflowing text with `…`. `clip` — hard clip."),
                ("whiteSpace", "`normal` | `nowrap` | `pre` | `pre-wrap`."),
                ("border", "Border shorthand: `'1px solid #ccc'`."),
                ("borderRadius", "Corner radius in px."),
                ("overflow", "`hidden` | `scroll` | `auto` | `visible` | `clip`."),
                ("position", "`relative` (offset from normal flow) | `absolute` (offset from positioned ancestor)."),
                ("zIndex", "Stacking order. Higher = drawn on top."),
                ("cursor", "`default` | `pointer` | `text` | `crosshair` | `grab` | `not-allowed`."),
                ("transform", "CSS transforms: `'translateX(10px)'`, `'scale(1.2)'`, etc."),
                ("transition", "CSS transition: e.g. `'all 0.2s ease'`."),
                ("rotate", "Rotation in degrees (`'45deg'`), radians (`'1.5rad'`), or turns (`'0.25turn'`)."),
                ("boxShadow", "Box shadow: `'x y blur spread color'`."),
                ("pointerEvents", "`auto` (receives events) | `none` (passes events through)."),
            };
            foreach (var (css, detail) in entries)
            {
                CssPropDocs[css] = detail;
                // Also register camelCase → kebab-case lookup
                var kebab = Regex.Replace(css, "([A-Z])", m => "-" + m.Value.ToLower());
                if (kebab != css) CssPropDocs[kebab] = detail;
            }
        }

        public static object? Compute(string src, int line, int ch)
        {
            if (string.IsNullOrEmpty(src)) return null;

            // Roslyn hover works for any cursor position — identifiers, keywords (string/int),
            // punctuation ([, ], (, )), operator tokens, etc. — via syntax-tree walking.
            // Don't filter by word first: that would skip [] collection expressions, new(), etc.
            var roslynHover = RoslynHover.GetHover(src, line, ch);
            if (roslynHover != null) return roslynHover;

            // Paper-specific fallback (ElementDocs, HookDocs, CssPropDocs) only applies to named
            // words in the JSX area. Extract the word now that Roslyn had its chance.
            var word = ExtractWord(src, line, ch);

            // Paper-specific docs (ElementDocs, HookDocs, CssPropDocs) are only relevant in the
            // JSX area (the return expression). In the C# preamble, names like "List", "Box", etc.
            // are C# identifiers — showing Paper element docs there is wrong and confusing.
            // If the position is before the JSX return statement, return null and show nothing.
            if (!IsInJsxArea(src, line))
                return null;

            // Fallback docs for JSX area (element names, hooks, CSS props)
            if (ElementDocs.TryGetValue(word, out var elemDoc))
                return MkHover(elemDoc);

            var hookKey = char.ToUpper(word[0]) + word[1..];
            if (HookDocs.TryGetValue(hookKey, out var hookDoc))
                return MkHover(hookDoc);

            if (CssPropDocs.TryGetValue(word, out var cssDoc))
                return MkHover($"**{word}** — {cssDoc}");

            return null;
        }

        /// <summary>
        /// Returns true if <paramref name="line"/> is at or after the JSX return statement.
        /// Paper-specific hover docs (element names, hooks, CSS props) should only appear there.
        /// </summary>
        private static bool IsInJsxArea(string src, int line)
        {
            var lines = src.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var t = lines[i].TrimStart();
                if (t.StartsWith("return (", StringComparison.Ordinal) ||
                    t.StartsWith("return (<", StringComparison.Ordinal) ||
                    t.StartsWith("return <",  StringComparison.Ordinal))
                    return line >= i;
            }
            // No explicit return — treat everything as JSX (simple/pure-JSX files)
            return true;
        }

        private static string ExtractWord(string src, int line, int ch)
        {
            var lines = src.Split('\n');
            if (line >= lines.Length) return "";
            var lineText = lines[line];
            if (ch >= lineText.Length) ch = lineText.Length - 1;
            if (ch < 0) return "";

            // Only extract a word when the cursor is on a word character.
            // For punctuation (<, >, ;, =, etc.) return empty so we don't misidentify
            // the adjacent identifier as the hover target.
            if (!IsWordChar(lineText[ch])) return "";

            int start = ch, end = ch;
            while (start > 0 && IsWordChar(lineText[start - 1])) start--;
            while (end < lineText.Length && IsWordChar(lineText[end])) end++;
            return lineText[start..end];
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

        private static object MkHover(string md) => new
        {
            contents = new { kind = "markdown", value = md },
        };
    }
}