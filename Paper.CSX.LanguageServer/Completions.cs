using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Paper.CSX.LanguageServer.Enums;

namespace Paper.CSX.LanguageServer
{
    internal static class Completions
    {
        // ── Paper elements ────────────────────────────────────────────────────────

        private static readonly string[] Elements =
        [
            "Box", "Text", "Button", "Input", "Scroll", "Image",
            "Textarea", "Checkbox", "Table", "TableRow", "TableCell",
            "RadioGroup", "RadioOption", "Fragment", "Viewport",
        ];

        // ── Props that apply to every element ─────────────────────────────────────

        private static readonly (string name, string insert, string detail, int kind)[] UniversalProps =
        [
            ("style",           "style={{$0}}",                        "Inline StyleSheet",         10),
            ("hoverStyle",      "hoverStyle={{$0}}",                   "StyleSheet on hover",       10),
            ("activeStyle",     "activeStyle={{$0}}",                  "StyleSheet on active",      10),
            ("focusStyle",      "focusStyle={{$0}}",                   "StyleSheet on focus",       10),
            ("className",       "className=\"$0\"",                    "CSS class names",           10),
            ("id",              "id=\"$0\"",                           "Element ID",                10),
            ("key",             "key=\"$0\"",                          "Reconciler key",            10),
            ("onClick",         "onClick={() => {$0}}",                "Click handler",              2),
            ("onDoubleClick",   "onDoubleClick={() => {$0}}",          "Double-click handler",       2),
            ("onMouseEnter",    "onMouseEnter={() => {$0}}",           "Mouse enter handler",        2),
            ("onMouseLeave",    "onMouseLeave={() => {$0}}",           "Mouse leave handler",        2),
            ("onMouseDown",     "onMouseDown={() => {$0}}",            "Mouse down handler",         2),
            ("onMouseUp",       "onMouseUp={() => {$0}}",              "Mouse up handler",           2),
            ("onFocus",         "onFocus={() => {$0}}",                "Focus handler",              2),
            ("onBlur",          "onBlur={() => {$0}}",                 "Blur handler",               2),
            ("onPointerDown",   "onPointerDown={(e) => {$0}}",         "Pointer down handler",       2),
            ("onPointerUp",     "onPointerUp={(e) => {$0}}",           "Pointer up handler",         2),
            ("onPointerMove",   "onPointerMove={(e) => {$0}}",         "Pointer move handler",       2),
            ("onPointerEnter",  "onPointerEnter={(e) => {$0}}",        "Pointer enter handler",      2),
            ("onPointerLeave",  "onPointerLeave={(e) => {$0}}",        "Pointer leave handler",      2),
            ("onWheel",         "onWheel={(e) => {$0}}",               "Wheel handler",              2),
            ("onKeyDown",       "onKeyDown={(k) => {$0}}",             "Key down (string key name)", 2),
            ("onKeyUp",         "onKeyUp={(k) => {$0}}",               "Key up (string key name)",   2),
            ("onKeyDownEvent",  "onKeyDownEvent={(e) => {$0}}",        "Key down (KeyEvent)",        2),
            ("onKeyUpEvent",    "onKeyUpEvent={(e) => {$0}}",          "Key up (KeyEvent)",          2),
            ("onKeyChar",       "onKeyChar={(e) => {$0}}",             "Key char event",             2),
        ];

        // ── Element-specific props ─────────────────────────────────────────────────

        private static readonly Dictionary<string, (string name, string insert, string detail, int kind)[]> ElementProps = new()
        {
            ["Input"] = [("value", "value={$0}", "Bound text value", 10), ("onChange", "onChange={(v) => {$0}}", "Change handler", 2)],
            ["Textarea"] = [("value", "value={$0}", "Bound text value", 10), ("onChange", "onChange={(v) => {$0}}", "Change handler", 2), ("rows", "rows={$0}", "Number of rows", 10)],
            ["Image"] = [("src", "src=\"$0\"", "Image path or URL", 10)],
            ["Checkbox"] = [("checked", "checked={$0}", "Checked state", 10), ("onCheckedChange", "onCheckedChange={(v) => {$0}}", "Check state change", 2)],
            ["RadioGroup"] = [("selectedValue", "selectedValue={$0}", "Selected option value", 10), ("onSelect", "onSelect={(v) => {$0}}", "Selection handler", 2)],
            ["RadioOption"] = [("value", "value=\"$0\"", "Option value", 10)],
        };

        // ── CSS properties ────────────────────────────────────────────────────────

        private static readonly (string css, string detail, string[] values)[] CssProps =
        [
            // Layout
            ("display",                "How element is rendered",        ["flex", "block", "inline", "inline-flex", "inline-block", "grid", "inline-grid", "none"]),
            ("flexDirection",          "Main axis direction",            ["row", "column", "row-reverse", "column-reverse"]),
            ("flexWrap",               "Allow wrapping to next line",    ["nowrap", "wrap", "wrap-reverse"]),
            ("flex",                   "flex shorthand (grow shrink basis)", []),
            ("flexGrow",               "How much to grow",               []),
            ("flexShrink",             "How much to shrink",             []),
            ("justifyContent",         "Alignment along main axis",      ["flex-start", "flex-end", "center", "space-between", "space-around", "space-evenly"]),
            ("alignItems",             "Alignment along cross axis",     ["flex-start", "flex-end", "center", "stretch", "baseline"]),
            ("alignSelf",              "Self cross-axis alignment",      ["auto", "flex-start", "flex-end", "center", "stretch", "baseline"]),
            ("alignContent",           "Multi-line cross alignment",     ["flex-start", "flex-end", "center", "stretch", "space-between", "space-around"]),
            ("gap",                    "Row and column gap (px)",        []),
            ("rowGap",                 "Row gap (px)",                   []),
            ("columnGap",              "Column gap (px)",                []),
            // Grid
            ("gridTemplateColumns",    "Grid column track sizes",        []),
            ("gridTemplateRows",       "Grid row track sizes",           []),
            ("gridColumn",             "Column span e.g. '1 / 3'",      []),
            ("gridRow",                "Row span e.g. '1 / 3'",         []),
            ("gridColumnStart",        "Start column line",              []),
            ("gridColumnEnd",          "End column line",                []),
            ("gridRowStart",           "Start row line",                 []),
            ("gridRowEnd",             "End row line",                   []),
            ("justifyItems",           "Justify all items in grid",      ["start", "end", "center", "stretch"]),
            ("justifySelf",            "Justify single item in grid",    ["start", "end", "center", "stretch"]),
            // Sizing
            ("width",                  "Width (px or %)",                ["auto"]),
            ("height",                 "Height (px or %)",               ["auto"]),
            ("minWidth",               "Minimum width",                  []),
            ("minHeight",              "Minimum height",                 []),
            ("maxWidth",               "Maximum width",                  []),
            ("maxHeight",              "Maximum height",                 []),
            // Spacing
            ("padding",                "All-sides padding (px)",         []),
            ("paddingTop",             "Top padding",                    []),
            ("paddingRight",           "Right padding",                  []),
            ("paddingBottom",          "Bottom padding",                 []),
            ("paddingLeft",            "Left padding",                   []),
            ("margin",                 "All-sides margin (px)",          []),
            ("marginTop",              "Top margin",                     []),
            ("marginRight",            "Right margin",                   []),
            ("marginBottom",           "Bottom margin",                  []),
            ("marginLeft",             "Left margin",                    []),
            // Visual
            ("background",             "Background color (#hex or rgba)", []),
            ("backgroundColor",        "Background color alias",         []),
            ("color",                  "Text color (#hex or rgba)",      []),
            ("opacity",                "Opacity 0..1",                   []),
            ("visibility",             "Element visibility",             ["visible", "hidden"]),
            ("boxShadow",              "Box shadow",                     []),
            ("backgroundImage",        "Background image",               []),
            ("backgroundSize",         "Background image size",          ["cover", "contain", "auto"]),
            ("backgroundPosition",     "Background position",            ["center", "top", "bottom", "left", "right"]),
            // Typography
            ("fontSize",               "Font size (px or em)",           []),
            ("fontWeight",             "Font weight",                    ["normal", "bold", "100", "200", "300", "400", "500", "600", "700", "800", "900"]),
            ("fontFamily",             "Font family",                    []),
            ("lineHeight",             "Line height multiplier",         []),
            ("letterSpacing",          "Letter spacing (px)",            []),
            ("textAlign",              "Text alignment",                 ["left", "center", "right"]),
            ("textOverflow",           "Text overflow behaviour",        ["ellipsis", "clip"]),
            ("textDecoration",         "Text decoration",                ["none", "underline"]),
            ("whiteSpace",             "White-space handling",           ["normal", "nowrap", "pre", "pre-wrap"]),
            ("wordWrap",               "Word wrap",                      ["normal", "break-word"]),
            // Border
            ("border",                 "Border shorthand",               []),
            ("borderTop",              "Top border",                     []),
            ("borderRight",            "Right border",                   []),
            ("borderBottom",           "Bottom border",                  []),
            ("borderLeft",             "Left border",                    []),
            ("borderRadius",           "Corner radius (px)",             []),
            ("borderWidth",            "Border width (px)",              []),
            ("borderColor",            "Border color",                   []),
            ("borderStyle",            "Border style",                   ["solid", "dashed", "dotted", "none"]),
            // Overflow
            ("overflow",               "Content overflow",               ["hidden", "visible", "scroll", "auto", "clip"]),
            ("overflowX",              "Horizontal overflow",            ["hidden", "visible", "scroll", "auto", "clip"]),
            ("overflowY",              "Vertical overflow",              ["hidden", "visible", "scroll", "auto", "clip"]),
            // Position
            ("position",               "Positioning scheme",             ["relative", "absolute", "fixed", "sticky"]),
            ("top",                    "Top offset (px)",                []),
            ("left",                   "Left offset (px)",               []),
            ("right",                  "Right offset (px)",              []),
            ("bottom",                 "Bottom offset (px)",             []),
            ("zIndex",                 "Z stack order",                  []),
            // Interaction
            ("cursor",                 "Mouse cursor shape",             ["default", "pointer", "text", "crosshair", "grab", "grabbing", "not-allowed"]),
            ("pointerEvents",          "Receive pointer events",         ["auto", "none"]),
            // Transform
            ("transform",              "CSS transforms",                 []),
            ("transition",             "CSS transition e.g. 'all 0.2s'", []),
            ("rotate",                 "Rotation (deg)",                 []),
            // Misc
            ("boxSizing",              "Box sizing model",               ["border-box", "content-box"]),
            ("objectFit",              "Image fit mode",                 ["contain", "cover", "fill"]),
        ];

        private static readonly Dictionary<string, string[]> CssPropValues =
            CssProps.ToDictionary(p => p.css, p => p.values, StringComparer.Ordinal);

        // ── Hooks / C# snippets ───────────────────────────────────────────────────

        private static readonly (string label, string insert, string detail, int kind)[] CSharpSnippets =
        [
            ("useState",    "var ($1, set$2, update$2) = Hooks.UseState($0);",           "State hook",           15),
            ("useEffect",   "Hooks.UseEffect(() => {\n    $0\n}, [$1]);",               "Effect hook",          15),
            ("useRef",      "var $1 = Hooks.UseRef($0);",                               "Ref hook",             15),
            ("useReducer",  "var ($1, dispatch) = Hooks.UseReducer($2, $0);",           "Reducer hook",         15),
            ("useContext",  "var $1 = Hooks.UseContext($0);",                            "Context hook",         15),
            ("useCallback", "var $1 = Hooks.UseCallback(() => {\n    $0\n}, [$2]);",    "Callback hook",        15),
            ("UI.Map",      "UI.Map($1, x => x.$2, x => (\n    $0\n))",                "Map list to nodes",    15),
            ("UI.When",     "UI.When($1, $0)",                                           "Conditional node",     15),
        ];

        // ─────────────────────────────────────────────────────────────────────────

        public static object[] Compute(string src, int line, int ch, string docUri)
        {
            if (string.IsNullOrEmpty(src))
                return BuildCSharpCompletions();

            var offset = LineCharToOffset(src, line, ch);
            var (ctx, tagName, propName) = DetectContext(src, line, ch);

            return ctx switch
            {
                CompletionContext.JsxTagName  => BuildTagCompletions(),
                CompletionContext.JsxPropName => BuildPropCompletions(tagName, src),
                CompletionContext.JsxStyleProp  => BuildStylePropCompletions(),
                CompletionContext.JsxStyleValue => BuildStyleValueCompletions(propName),
                CompletionContext.JsxClassName  => BuildClassNameCompletions(docUri, src),
                CompletionContext.JsxEventValue => BuildCSharpCompletions(),
                CompletionContext.JsxPropValue  => BuildCSharpCompletions(),
                CompletionContext.ImportPath    => BuildImportPathCompletions(tagName, docUri),
                CompletionContext.CSharp        => BuildCSharpCompletionsWithRoslyn(src, line, ch, offset),
                _ => BuildCSharpCompletions(),
            };
        }

        // ─── Context detection ────────────────────────────────────────────────────

        private static (CompletionContext ctx, string tag, string prop) DetectContext(string src, int line, int ch)
        {
            // Convert (line, ch) to absolute offset
            var offset = LineCharToOffset(src, line, ch);
            var before = src[..offset];

            // Check if inside @import "..." or @import '...' — file path completion
            // Look at the current line only (imports are always single-line)
            var currentLineStart = before.LastIndexOf('\n') + 1;
            var currentLineBefore = before[currentLineStart..];
            var importMatch = Regex.Match(currentLineBefore, @"@import\s+[""']([^""']*)$");
            if (importMatch.Success)
                return (CompletionContext.ImportPath, importMatch.Groups[1].Value, "");

            // Check if inside className="..." (look for unclosed className=" before cursor)
            var classMatch = Regex.Match(before, @"\bclassName\s*=\s*""([^""]*)$");
            if (classMatch.Success)
                return (CompletionContext.JsxClassName, "", "");

            // Check if inside style={{ ... }} — scan back for style={{
            var styleOpen = FindUnclosedStyleBlock(before);
            if (styleOpen >= 0)
            {
                // Are we after a colon (typing a value)?
                var styleContent = before[styleOpen..];
                var propMatch = Regex.Match(styleContent, @"\b([a-zA-Z]+)\s*:\s*[^,}]*$");
                if (propMatch.Success)
                    return (CompletionContext.JsxStyleValue, "", propMatch.Groups[1].Value);
                return (CompletionContext.JsxStyleProp, "", "");
            }

            // Check if inside a JSX open tag — find the last unclosed <TagName
            var tagMatch = FindUnclosedTag(before);
            if (tagMatch != null)
            {
                var (tag, afterTag) = tagMatch.Value;

                // Immediately after < or <Tag with only whitespace — tag name completion
                if (Regex.IsMatch(afterTag.TrimEnd(), @"^$"))
                    return (CompletionContext.JsxTagName, "", "");

                // Inside onXxx={...}
                if (Regex.IsMatch(afterTag, @"\bon[A-Z][a-zA-Z]*\s*=\s*\{[^}]*$"))
                    return (CompletionContext.JsxEventValue, tag, "");

                // Inside ={...}
                if (Regex.IsMatch(afterTag, @"=\s*\{[^}]*$"))
                    return (CompletionContext.JsxPropValue, tag, "");

                // Otherwise we're between props
                return (CompletionContext.JsxPropName, tag, "");
            }

            // Typing a tag name after <
            if (Regex.IsMatch(before, @"<[A-Z][a-zA-Z0-9]*$"))
                return (CompletionContext.JsxTagName, "", "");

            return (CompletionContext.CSharp, "", "");
        }

        /// <summary>Returns the offset past "style={{" if we're inside an unclosed style block, or -1.</summary>
        private static int FindUnclosedStyleBlock(string before)
        {
            var m = Regex.Match(before, @"\bstyle\s*=\s*\{\{");
            if (!m.Success) return -1;
            // Walk forward from the end of the match counting {{ vs }} depth
            var pos = m.Index + m.Length;
            int depth = 2; // opened {{
            while (pos < before.Length && depth > 0)
            {
                if (before[pos] == '{') depth++;
                else if (before[pos] == '}') depth--;
                if (depth == 0) return -1; // we're past the closing }}
                pos++;
            }
            return depth > 0 ? m.Index + m.Length : -1;
        }

        /// <summary>Returns (tagName, textAfterTagName) if cursor is inside an open tag, or null.</summary>
        private static (string tag, string afterTag)? FindUnclosedTag(string before)
        {
            // Walk backwards through the text looking for an unmatched <
            int depth = 0;
            for (int i = before.Length - 1; i >= 0; i--)
            {
                if (before[i] == '>') { depth++; continue; }
                if (before[i] == '<')
                {
                    if (depth > 0) { depth--; continue; }
                    // Found an unmatched <
                    var rest = before[(i + 1)..];
                    // Make sure it's a component tag (starts with uppercase or is a known lowercase element)
                    var tagMatch = Regex.Match(rest, @"^([A-Z][a-zA-Z0-9]*)(.*)$", RegexOptions.Singleline);
                    if (tagMatch.Success)
                        return (tagMatch.Groups[1].Value, tagMatch.Groups[2].Value);
                    return null;
                }
            }
            return null;
        }

        private static int LineCharToOffset(string src, int line, int ch)
        {
            int l = 0, offset = 0;
            while (offset < src.Length && l < line)
            {
                if (src[offset] == '\n') l++;
                offset++;
            }
            return Math.Min(offset + ch, src.Length);
        }

        // ─── Completion builders ──────────────────────────────────────────────────

        private static object[] BuildTagCompletions() =>
            Elements.Select(e => Item(e, e, $"<{e}> element", 7)).ToArray();

        private static readonly HashSet<string> _intrinsics = new(StringComparer.Ordinal)
        {
            "Box","Text","Button","Input","Image","Scroll","Viewport",
            "Checkbox","Textarea","Table","TableRow","TableCell",
            "RadioGroup","RadioOption","Select","Fragment"
        };

        private static object[] BuildPropCompletions(string tagName, string src)
        {
            var items = new List<object>();
            // Universal props
            foreach (var (name, insert, detail, kind) in UniversalProps)
                items.Add(Item(name, insert, detail, kind));
            // Element-specific props for intrinsics
            if (ElementProps.TryGetValue(tagName, out var extras))
                foreach (var (name, insert, detail, kind) in extras)
                    items.Add(Item(name, insert, detail, kind));
            // Custom component: extract typed props from its function signature
            if (!_intrinsics.Contains(tagName))
            {
                var customItems = BuildCustomComponentPropCompletions(src, tagName);
                items.AddRange(customItems);
            }
            return [.. items];
        }

        /// <summary>
        /// Finds the props type for a custom component and returns completion items for its properties.
        /// Handles both generic-typed (<c>function Badge&lt;BadgeProps&gt;</c>) and
        /// inline-destructured (<c>function Badge({ string label })</c>) signatures.
        /// </summary>
        private static object[] BuildCustomComponentPropCompletions(string src, string tagName)
        {
            try
            {
                // ── 1. Inline destructured: function Badge({ string label, PaperColour colour }) ──
                var destructMatch = Regex.Match(src,
                    $@"function\s+{Regex.Escape(tagName)}\s*\({{\s*([^}}]+)}}\s*\)",
                    RegexOptions.Singleline);
                if (destructMatch.Success)
                {
                    var items = new List<object>();
                    foreach (var part in destructMatch.Groups[1].Value.Split(','))
                    {
                        var p = part.Trim();
                        if (string.IsNullOrEmpty(p)) continue;
                        // "type name" or "type name = default"
                        var eq = p.IndexOf('='); if (eq >= 0) p = p[..eq].Trim();
                        var sp = p.LastIndexOf(' '); if (sp < 0) continue;
                        var type = p[..sp].Trim();
                        var name = p[(sp + 1)..].Trim();
                        var camel = char.ToLower(name[0]) + name[1..];
                        if (Regex.IsMatch(name, @"^\w+$"))
                            items.Add(Item(camel, camel + "={${1}}", $"({type}) {name}", 5));
                    }
                    if (items.Count > 0) return [.. items];
                }

                // ── 2. Generic typed: function Badge<BadgeProps>({ Name1, Name2 }) or (props) ──
                var genericMatch = Regex.Match(src,
                    $@"function\s+{Regex.Escape(tagName)}\s*<(\w+)>\s*\(",
                    RegexOptions.Singleline);
                if (!genericMatch.Success) return [];

                var propsTypeName = genericMatch.Groups[1].Value;

                // Use the cached Roslyn compilation to find the type and its properties
                var (compilation, _, _) = RoslynHover.GetOrBuildCompilation(src);
                var propsType = compilation.GetSymbolsWithName(propsTypeName, SymbolFilter.Type)
                                        .OfType<INamedTypeSymbol>()
                                        .FirstOrDefault();
                if (propsType == null) return [];

                // Collect constructor parameters (record primary ctor) — they carry default values
                var ctorParams = propsType.Constructors
                    .OrderByDescending(c => c.Parameters.Length)
                    .FirstOrDefault()?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty;
                var defaultsByName = ctorParams.ToDictionary(
                    p => p.Name, p => p.HasExplicitDefaultValue, StringComparer.OrdinalIgnoreCase);

                var propItems = new List<object>();
                foreach (var member in propsType.GetMembers().OfType<IPropertySymbol>()
                            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic))
                {
                    var camelName = char.ToLower(member.Name[0]) + member.Name[1..];
                    var typeName = member.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    var isOptional = defaultsByName.TryGetValue(member.Name, out var hasDefault) && hasDefault;
                    var detail = isOptional ? $"({typeName}) — optional" : $"({typeName})";
                    propItems.Add(Item(camelName, camelName + "={${1}}", detail, 5));
                }
                return [.. propItems];
            }
            catch { return []; }
        }

        private static object[] BuildStylePropCompletions() =>
            CssProps.Select(p => Item(p.css, p.css + ": ", p.detail, 10)).ToArray();

        private static object[] BuildStyleValueCompletions(string propName)
        {
            if (CssPropValues.TryGetValue(propName, out var vals) && vals.Length > 0)
                return vals.Select(v => Item(v, $"'{v}'", v, 12)).ToArray();

            // Generic fallback: suggest number literals and common units
            return
            [
                Item("0",    "0",       "Zero",           12),
                Item("px",   "${1:16}", "Pixel value",    12),
                Item("em",   "${1:1.5}em", "Em value",   12),
                Item("%",    "${1:100}%",  "Percent",     12),
                Item("auto", "'auto'",     "Auto",        12),
            ];
        }

        private static object[] BuildClassNameCompletions(string docUri, string src)
        {
            // Try to parse class names from the co-located .csss file referenced in @import, or <same>.csss
            var classes = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                // Look for @import "*.csss" in source
                var importMatches = Regex.Matches(src, @"@import\s+['""]([^'""]+\.csss)['""]");
                var docPath = docUri.Replace("file:///", "/").Replace("file://", "/");
                if (OperatingSystem.IsWindows()) docPath = Uri.UnescapeDataString(docUri[8..]); // strip file:///
                var docDir = Path.GetDirectoryName(docPath) ?? ".";

                var csssFiles = new List<string>();
                foreach (Match m in importMatches)
                    csssFiles.Add(Path.GetFullPath(Path.Combine(docDir, m.Groups[1].Value)));

                // Also try co-located <same>.csss
                var colocated = Path.ChangeExtension(docPath, ".csss");
                if (File.Exists(colocated)) csssFiles.Add(colocated);

                foreach (var f in csssFiles)
                {
                    if (!File.Exists(f)) continue;
                    var csss = File.ReadAllText(f);
                    foreach (Match m in Regex.Matches(csss, @"\.([\w-]+)\s*(?:\{|:)"))
                        classes.Add(m.Groups[1].Value);
                }
            }
            catch { /* best effort */ }

            if (classes.Count == 0)
                return [];

            return classes.Select(c => Item(c, c, "CSS class", 14)).ToArray();
        }

        private static object[] BuildImportPathCompletions(string partial, string docUri)
        {
            try
            {
                // Resolve the document's directory from the URI
                var docPath = Uri.UnescapeDataString(docUri.StartsWith("file:///") ? docUri[8..] : docUri[7..]);
                if (!OperatingSystem.IsWindows() && !docPath.StartsWith('/'))
                    docPath = "/" + docPath;
                var docDir = Path.GetDirectoryName(docPath) ?? ".";

                // Split partial path into directory prefix + filename prefix typed so far.
                // '/' is a VSCode word boundary, so insertText only needs to be the current segment
                // (the part after the last '/') — VSCode replaces the current word with it, leaving
                // any preceding path segments (e.g. "./components/") intact.
                var dirPart = Path.GetDirectoryName(partial)?.Replace('\\', '/') ?? "";
                var filePart = Path.GetFileName(partial);
                var searchDir = dirPart.Length > 0
                    ? Path.GetFullPath(Path.Combine(docDir, dirPart))
                    : docDir;

                if (!Directory.Exists(searchDir)) return [];

                var items = new List<object>();

                // Subdirectories — insertText is just the dir name + "/" so it appends cleanly
                foreach (var dir in Directory.GetDirectories(searchDir).Order())
                {
                    var name = Path.GetFileName(dir);
                    if (!name.StartsWith(filePart, StringComparison.OrdinalIgnoreCase)) continue;
                    items.Add(new { label = name + "/", insertText = name + "/", kind = 19, detail = "directory" });
                }

                // .csx and .csss files — insertText is just the filename
                var csxFiles = Directory.GetFiles(searchDir, "*.csx");
                var csssFiles = Directory.GetFiles(searchDir, "*.csss");
                foreach (var file in csxFiles.Concat(csssFiles).Order())
                {
                    var name = Path.GetFileName(file);
                    if (!name.StartsWith(filePart, StringComparison.OrdinalIgnoreCase)) continue;
                    if (Path.GetFullPath(file).Equals(Path.GetFullPath(docPath), StringComparison.OrdinalIgnoreCase)) continue;
                    var ext = name.EndsWith(".csx") ? ".csx component" : ".csss stylesheet";
                    items.Add(new { label = name, insertText = name, kind = 17, detail = ext });
                }

                return [.. items];
            }
            catch { return []; }
        }

        private static object[] BuildCSharpCompletions()
        {
            var items = new List<object>();
            // Hooks / snippets
            foreach (var (label, insert, detail, kind) in CSharpSnippets)
                items.Add(SnippetItem(label, insert, detail, kind));
            // Element factory completions for C# code
            foreach (var e in Elements)
                items.Add(Item($"UI.{e}", $"UI.{e}(", $"Create {e} element", 7));
            return [.. items];
        }

        private static object[] BuildCSharpCompletionsWithRoslyn(string csxSrc, int line, int ch, int cursorOffset)
        {
            var before = csxSrc[..cursorOffset];

            // Member access: cursor after "identifier." → return instance members of that type
            var memberMatch = Regex.Match(before, @"\b([A-Za-z_][A-Za-z0-9_]*)\.$");
            if (memberMatch.Success)
            {
                var identifier = memberMatch.Groups[1].Value;
                var memberItems = RoslynMembers.GetMembers(csxSrc, identifier, cursorOffset);
                if (memberItems.Length > 0)
                    return memberItems;
            }

            // Position-aware Roslyn completions using LookupSymbols at the mapped generated-C# offset.
            // These replace the static snippet list with live symbols from the semantic model.
            var roslynItems = RoslynCompletions.GetCompletions(csxSrc, line, ch);

            // Merge with Paper-specific snippets (hooks, UI.*) which aren't in LookupSymbols
            var snippetItems = BuildCSharpCompletions();

            // Roslyn items first, then snippets (VSCode will blend them with its own ranking)
            return [.. roslynItems, .. snippetItems];
        }

        // ─── Item helpers ─────────────────────────────────────────────────────────

        private static object Item(string label, string insert, string detail, int kind) => new
        {
            label,
            kind,
            detail,
            insertText = insert,
            insertTextFormat = 1, // PlainText
        };

        private static object SnippetItem(string label, string insert, string detail, int kind) => new
        {
            label,
            kind,
            detail,
            insertText = insert,
            insertTextFormat = 2, // Snippet
        };
    }
}