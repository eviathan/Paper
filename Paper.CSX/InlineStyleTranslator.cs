using System.Text;
using System.Text.RegularExpressions;

namespace Paper.CSX
{
    internal static class InlineStyleTranslator
    {
        /// <summary>
        /// Translates a JS/TS object-literal-ish style expression into a C# <c>new StyleSheet { ... }</c>.
        /// Input typically looks like: <c>{ display: 'flex', padding: 12, background: '#fff' }</c>
        /// or without outer braces when coming from some sources.
        /// </summary>
        public static string Translate(string styleExpr)
        {
            if (string.IsNullOrWhiteSpace(styleExpr))
                return "StyleSheet.Empty";

            string normalized = styleExpr.Trim();
            if (normalized.StartsWith("{") && normalized.EndsWith("}"))
                normalized = normalized[1..^1];

            var sb = new StringBuilder();
            sb.Append("new StyleSheet { ");

            string flat = normalized.Replace("\r", " ").Replace("\n", " ");
            while (flat.Contains("  ", StringComparison.Ordinal))
                flat = flat.Replace("  ", " ");

            // Allow value to be followed by comma/}, or end. Values may contain parenthesised groups (e.g. rgb(r,g,b)).
            var declMatches = Regex.Matches(flat, @"(\w+)\s*:\s*((?:[^,}(]|\([^)]*\))+)(?=\s*[,}]|\s*$)", RegexOptions.Multiline);
            foreach (Match match in declMatches)
            {
                string prop = match.Groups[1].Value.Trim();
                string value = match.Groups[2].Value.Trim();

                string paperProp = ToPascalCase(prop);

                // CSS property aliases — map non-StyleSheet names to their equivalents
                if (paperProp == "BackgroundColor") paperProp = "Background";

                // CSS shorthands that expand to multiple StyleSheet properties
                if (paperProp == "Flex")  { EmitFlexShorthand(sb, value); continue; }
                if (paperProp == "Gap")   { EmitGapShorthand(sb, value); continue; }
                if (paperProp == "Grid")  continue; // ignore `grid` shorthand (too complex)
                if (paperProp == "GridColumn") { EmitGridSpan(sb, value, column: true);  continue; }
                if (paperProp == "GridRow")    { EmitGridSpan(sb, value, column: false); continue; }

                // String pass-through props (no C# type conversion needed)
                if (paperProp is "Transition" or "GridTemplateColumns" or "GridTemplateRows")
                {
                    string raw = value.Trim().Trim('"', '\'');
                    sb.Append($"{paperProp} = \"{raw}\", ");
                    continue;
                }

                string paperValue = CSXParserStyleValue.Parse(value, paperProp);
                sb.Append($"{paperProp} = {paperValue}, ");
            }

            if (sb.Length > "new StyleSheet { ".Length)
                sb.Length -= 2;

            sb.Append(" }");
            return sb.ToString();
        }

        /// <summary>Emit ColumnGap+RowGap for CSS <c>gap</c> shorthand.</summary>
        private static void EmitGapShorthand(StringBuilder sb, string rawValue)
        {
            string v = rawValue.Trim().Trim('"', '\'');
            var parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string rowGap = CSXParserStyleValue.Parse(parts[0], "RowGap");
            string colGap = parts.Length > 1 ? CSXParserStyleValue.Parse(parts[1], "ColumnGap") : rowGap;
            if (rowGap != "null") sb.Append($"RowGap = {rowGap}, ");
            if (colGap != "null") sb.Append($"ColumnGap = {colGap}, ");
        }

        /// <summary>Emit GridColumnStart/End or GridRowStart/End for <c>grid-column</c> / <c>grid-row</c> shorthands.</summary>
        private static void EmitGridSpan(StringBuilder sb, string rawValue, bool column)
        {
            string v = rawValue.Trim().Trim('"', '\'');
            var parts = v.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (int.TryParse(parts[0].Trim(), out int start))
                sb.Append(column ? $"GridColumnStart = {start}, " : $"GridRowStart = {start}, ");
            if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int end))
                sb.Append(column ? $"GridColumnEnd = {end}, " : $"GridRowEnd = {end}, ");
        }

        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            return char.ToUpperInvariant(input[0]) + input[1..];
        }

        /// <summary>
        /// Emits StyleSheet properties for the CSS <c>flex</c> shorthand.
        /// Handles: flex: N  →  FlexGrow=N, FlexShrink=1
        ///          flex: N M  →  FlexGrow=N, FlexShrink=M
        ///          flex: N M basis  →  FlexGrow=N, FlexShrink=M, FlexBasis=basis
        ///          flex: none  →  FlexGrow=0, FlexShrink=0
        ///          flex: auto  →  FlexGrow=1, FlexShrink=1
        /// </summary>
        private static void EmitFlexShorthand(StringBuilder sb, string rawValue)
        {
            string v = rawValue.Trim().Trim('"', '\'');
            if (string.Equals(v, "none", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("FlexGrow = 0f, FlexShrink = 0f, ");
                return;
            }
            if (string.Equals(v, "auto", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("FlexGrow = 1f, FlexShrink = 1f, ");
                return;
            }
            var parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double grow)) return;
            sb.Append($"FlexGrow = {grow}f, ");
            if (parts.Length >= 2 && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double shrink))
                sb.Append($"FlexShrink = {shrink}f, ");
            else
                sb.Append("FlexShrink = 1f, ");
            if (parts.Length >= 3)
            {
                string basis = CSXParserStyleValue.Parse(parts[2], "FlexBasis");
                if (basis != "null")
                    sb.Append($"FlexBasis = {basis}, ");
            }
        }
    }

    internal static class CSXParserStyleValue
    {
        public static string Parse(string value, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "null";

            string trimmedValue = value.Trim().Trim('"', '\'', ' ', '\t');

            // rgb()/rgba() may contain spaces — handle before the space-split branch.
            if (trimmedValue.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                var rgb = ParseRgbColor(trimmedValue);
                if (rgb != null) return rgb;
            }

            // FontWeight numeric values ("100"–"900") must be checked before the generic
            // numeric path, which would otherwise emit Length.Px(value).
            if (string.Equals(propertyName, "FontWeight", StringComparison.OrdinalIgnoreCase))
            {
                var fw = ParseFontWeight(trimmedValue);
                if (fw != null) return fw;
            }

            if (trimmedValue.Contains(' '))
            {
                if (string.Equals(propertyName, "Padding", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "Margin", StringComparison.OrdinalIgnoreCase))
                    return ParseThicknessExpression(trimmedValue);
                if (string.Equals(propertyName, "Border", StringComparison.OrdinalIgnoreCase))
                {
                    var b = ParseBorderShorthand(trimmedValue);
                    if (b != null) return b;
                }
                if (string.Equals(propertyName, "BorderTop", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "BorderRight", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "BorderBottom", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "BorderLeft", StringComparison.OrdinalIgnoreCase))
                {
                    var bs = ParseBorderSideShorthand(trimmedValue);
                    if (bs != null) return bs;
                }
                return $"\"{trimmedValue}\"";
            }

            // border-side: 0 → Border.None
            if ((string.Equals(propertyName, "BorderTop", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(propertyName, "BorderRight", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(propertyName, "BorderBottom", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(propertyName, "BorderLeft", StringComparison.OrdinalIgnoreCase)) &&
                (trimmedValue == "0" || string.Equals(trimmedValue, "none", StringComparison.OrdinalIgnoreCase)))
                return "Border.None";

            // Rotate accepts deg/rad/turn units: rotate: '45deg', rotate: '1.5rad', rotate: '0.25turn'
            if (string.Equals(propertyName, "Rotate", StringComparison.OrdinalIgnoreCase))
            {
                var lower2 = trimmedValue.ToLowerInvariant();
                if (lower2.EndsWith("deg") && double.TryParse(lower2[..^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double deg))
                    return $"{(float)(deg * Math.PI / 180.0)}f";
                if (lower2.EndsWith("turn") && double.TryParse(lower2[..^4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double turn))
                    return $"{(float)(turn * 2.0 * Math.PI)}f";
                if (lower2.EndsWith("rad") && double.TryParse(lower2[..^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double rad))
                    return $"{rad}f";
            }

            if (double.TryParse(trimmedValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double num))
            {
                if (string.Equals(propertyName, "BorderRadius", StringComparison.OrdinalIgnoreCase))
                    return $"{num}f";
                if (string.Equals(propertyName, "FlexGrow", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "FlexShrink", StringComparison.OrdinalIgnoreCase))
                    return $"{num}f";
                if (string.Equals(propertyName, "Opacity", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "LineHeight", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "LetterSpacing", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "Rotate", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "TranslateX", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "TranslateY", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "ScaleX", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "ScaleY", StringComparison.OrdinalIgnoreCase))
                    return $"{num}f";
                if (string.Equals(propertyName, "ZIndex", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "GridColumnStart", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "GridColumnEnd",   StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "GridRowStart",    StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "GridRowEnd",      StringComparison.OrdinalIgnoreCase))
                    return $"{(int)num}";
                if (string.Equals(propertyName, "Padding", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "Margin", StringComparison.OrdinalIgnoreCase))
                    return $"new Thickness({num}f)";

                return $"Length.Px({num})";
            }

            if (trimmedValue.StartsWith('#'))
                return ParseHexColor(trimmedValue);

            if (trimmedValue.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                var rgb = ParseRgbColor(trimmedValue);
                if (rgb != null) return rgb;
            }

            var lower = trimmedValue.ToLowerInvariant();
            if (string.Equals(propertyName, "JustifyContent", StringComparison.OrdinalIgnoreCase))
            {
                var jc = ParseJustifyContent(lower);
                if (jc != null) return jc;
            }
            if (string.Equals(propertyName, "AlignItems", StringComparison.OrdinalIgnoreCase))
            {
                var ai = ParseAlignItems(lower);
                if (ai != null) return ai;
            }
            if (string.Equals(propertyName, "AlignSelf", StringComparison.OrdinalIgnoreCase))
            {
                var alignSelfExpr = ParseAlignSelf(lower);
                if (alignSelfExpr != null) return alignSelfExpr;
            }
            if (string.Equals(propertyName, "AlignContent", StringComparison.OrdinalIgnoreCase))
            {
                var ac = ParseAlignContent(lower);
                if (ac != null) return ac;
            }
            if (string.Equals(propertyName, "Overflow", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "OverflowX", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "OverflowY", StringComparison.OrdinalIgnoreCase))
            {
                var ov = ParseOverflow(lower);
                if (ov != null) return ov;
            }
            if (string.Equals(propertyName, "FontWeight", StringComparison.OrdinalIgnoreCase))
            {
                var fw = ParseFontWeight(lower);
                if (fw != null) return fw;
            }
            if (string.Equals(propertyName, "FlexWrap", StringComparison.OrdinalIgnoreCase))
            {
                var fw = ParseFlexWrap(lower);
                if (fw != null) return fw;
            }
            if (string.Equals(propertyName, "FlexDirection", StringComparison.OrdinalIgnoreCase))
            {
                var fd = ParseFlexDirection(lower);
                if (fd != null) return fd;
            }
            if (string.Equals(propertyName, "TextAlign", StringComparison.OrdinalIgnoreCase))
            {
                var ta = ParseTextAlign(lower);
                if (ta != null) return ta;
            }
            if (string.Equals(propertyName, "ObjectFit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "BackgroundSize", StringComparison.OrdinalIgnoreCase))
            {
                var of = ParseObjectFit(lower);
                if (of != null) return of;
            }
            if (string.Equals(propertyName, "Display", StringComparison.OrdinalIgnoreCase))
            {
                var d = ParseDisplay(lower);
                if (d != null) return d;
            }
            if (string.Equals(propertyName, "BoxSizing", StringComparison.OrdinalIgnoreCase))
                return lower == "content-box" ? "BoxSizing.ContentBox" : "BoxSizing.BorderBox";
            if (string.Equals(propertyName, "TextOverflow", StringComparison.OrdinalIgnoreCase))
            {
                var to = ParseTextOverflow(lower);
                if (to != null) return to;
            }
            if (string.Equals(propertyName, "Cursor", StringComparison.OrdinalIgnoreCase))
            {
                var c = ParseCursor(lower);
                if (c != null) return c;
            }
            if (string.Equals(propertyName, "Position", StringComparison.OrdinalIgnoreCase))
            {
                var p = ParsePosition(lower);
                if (p != null) return p;
            }
            if (string.Equals(propertyName, "Visibility", StringComparison.OrdinalIgnoreCase))
            {
                var vis = ParseVisibility(lower);
                if (vis != null) return vis;
            }
            if (string.Equals(propertyName, "WhiteSpace", StringComparison.OrdinalIgnoreCase))
            {
                var ws = ParseWhiteSpace(lower);
                if (ws != null) return ws;
            }
            if (string.Equals(propertyName, "PointerEvents", StringComparison.OrdinalIgnoreCase))
            {
                var pe = ParsePointerEvents(lower);
                if (pe != null) return pe;
            }
            if (string.Equals(propertyName, "JustifyItems", StringComparison.OrdinalIgnoreCase))
            {
                var ji = ParseJustifyItems(lower);
                if (ji != null) return ji;
            }
            if (string.Equals(propertyName, "BackgroundImage", StringComparison.OrdinalIgnoreCase))
            {
                // "url(path)" or plain path
                string path = trimmedValue.Trim();
                if (path.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && path.EndsWith(")"))
                    path = path.Substring(4, path.Length - 5).Trim().Trim('"', '\'');
                else
                    path = path.Trim('"', '\'');
                return $"\"{path.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
            }

            return lower switch
            {
                "auto" => "Length.Auto",
                "100%" => "Length.Percent(100)",
                "none" => "null",
                // Named CSS colours
                "white"         => "new PaperColour(1f, 1f, 1f, 1f)",
                "black"         => "new PaperColour(0f, 0f, 0f, 1f)",
                "transparent"   => "new PaperColour(0f, 0f, 0f, 0f)",
                "red"           => "new PaperColour(1f, 0f, 0f, 1f)",
                "green"         => "new PaperColour(0f, 0.502f, 0f, 1f)",
                "blue"          => "new PaperColour(0f, 0f, 1f, 1f)",
                "gray" or "grey"=> "new PaperColour(0.502f, 0.502f, 0.502f, 1f)",
                "silver"        => "new PaperColour(0.753f, 0.753f, 0.753f, 1f)",
                "yellow"        => "new PaperColour(1f, 1f, 0f, 1f)",
                "orange"        => "new PaperColour(1f, 0.647f, 0f, 1f)",
                "purple"        => "new PaperColour(0.502f, 0f, 0.502f, 1f)",
                "pink"          => "new PaperColour(1f, 0.753f, 0.796f, 1f)",
                _ => ParseCssLengthOrString(trimmedValue, propertyName),
            };
        }

        private static string ParseThicknessExpression(string value)
        {
            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return ParseLengthLiteral(parts[0]) is string s ? $"new Thickness({s})" : "new Thickness(0f)";
            if (parts.Length == 2)
            {
                string v = ParseLengthLiteral(parts[0]) ?? "Length.Px(0)";
                string h = ParseLengthLiteral(parts[1]) ?? "Length.Px(0)";
                return $"new Thickness({v}, {h})";
            }
            if (parts.Length >= 4)
            {
                string t = ParseLengthLiteral(parts[0]) ?? "Length.Px(0)";
                string r = ParseLengthLiteral(parts[1]) ?? "Length.Px(0)";
                string b = ParseLengthLiteral(parts[2]) ?? "Length.Px(0)";
                string l = ParseLengthLiteral(parts[3]) ?? "Length.Px(0)";
                return $"new Thickness({t}, {r}, {b}, {l})";
            }
            return "new Thickness(0f)";
        }

        private static string? ParseLengthLiteral(string v)
        {
            v = v.Trim().Trim('"', '\'');
            if (v.EndsWith("px") && double.TryParse(v[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double px))
                return $"Length.Px({px})";
            if (v.EndsWith('%') && double.TryParse(v[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double pct))
                return $"Length.Percent({pct})";
            if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double n))
                return $"Length.Px({n})";
            return null;
        }

        private static string? ParseJustifyContent(string value) => value switch
        {
            "flex-start" => "JustifyContent.FlexStart",
            "flex-end" => "JustifyContent.FlexEnd",
            "center" => "JustifyContent.Center",
            "space-between" => "JustifyContent.SpaceBetween",
            "space-around" => "JustifyContent.SpaceAround",
            "space-evenly" => "JustifyContent.SpaceEvenly",
            _ => null,
        };

        private static string? ParseAlignItems(string value) => value switch
        {
            "flex-start" => "AlignItems.FlexStart",
            "flex-end" => "AlignItems.FlexEnd",
            "center" => "AlignItems.Center",
            "stretch" => "AlignItems.Stretch",
            "baseline" => "AlignItems.Baseline",
            _ => null,
        };

        private static string? ParseAlignContent(string value) => value switch
        {
            "flex-start" => "AlignContent.FlexStart",
            "flex-end" => "AlignContent.FlexEnd",
            "center" => "AlignContent.Center",
            "space-between" => "AlignContent.SpaceBetween",
            "space-around" => "AlignContent.SpaceAround",
            "stretch" => "AlignContent.Stretch",
            _ => null,
        };

        private static string? ParseObjectFit(string value) => value switch
        {
            "fill" => "ObjectFit.Fill",
            "contain" => "ObjectFit.Contain",
            "cover" => "ObjectFit.Cover",
            _ => null,
        };

        private static string? ParseAlignSelf(string value) => value switch
        {
            "auto" => "AlignSelf.Auto",
            "flex-start" => "AlignSelf.FlexStart",
            "flex-end" => "AlignSelf.FlexEnd",
            "center" => "AlignSelf.Center",
            "stretch" => "AlignSelf.Stretch",
            "baseline" => "AlignSelf.Baseline",
            _ => null,
        };

        private static string? ParseOverflow(string value) => value switch
        {
            "visible" => "Overflow.Visible",
            "hidden" => "Overflow.Hidden",
            "scroll" => "Overflow.Scroll",
            "auto" => "Overflow.Auto",
            _ => null,
        };

        private static string? ParseTextAlign(string value) => value switch
        {
            "left" => "TextAlign.Left",
            "right" => "TextAlign.Right",
            "center" => "TextAlign.Center",
            "justify" => "TextAlign.Justify",
            _ => null,
        };

        private static string? ParseFlexWrap(string value) => value switch
        {
            "nowrap" => "FlexWrap.NoWrap",
            "wrap" => "FlexWrap.Wrap",
            "wrap-reverse" => "FlexWrap.WrapReverse",
            _ => null,
        };

        private static string? ParseFlexDirection(string value) => value switch
        {
            "row" => "FlexDirection.Row",
            "row-reverse" => "FlexDirection.RowReverse",
            "column" => "FlexDirection.Column",
            "column-reverse" => "FlexDirection.ColumnReverse",
            _ => null,
        };

        private static string? ParseFontWeight(string value) => value switch
        {
            "normal" => "FontWeight.Normal",
            "bold" => "FontWeight.Bold",
            "100" => "FontWeight.Thin",
            "200" => "FontWeight.ExtraLight",
            "300" => "FontWeight.Light",
            "400" => "FontWeight.Normal",
            "500" => "FontWeight.Medium",
            "600" => "FontWeight.SemiBold",
            "700" => "FontWeight.Bold",
            "800" => "FontWeight.ExtraBold",
            "900" => "FontWeight.Black",
            _ => null,
        };

        private static string? ParseJustifyItems(string value) => value switch
        {
            "start" => "JustifyItems.Start",
            "end" => "JustifyItems.End",
            "center" => "JustifyItems.Center",
            "stretch" => "JustifyItems.Stretch",
            _ => null,
        };

        private static string? ParseDisplay(string value) => value switch
        {
            "block" => "Display.Block",
            "flex" => "Display.Flex",
            "grid" => "Display.Grid",
            "none" => "Display.None",
            "inline" => "Display.Inline",
            "inline-flex" => "Display.InlineFlex",
            _ => null,
        };

        private static string? ParseTextOverflow(string value) => value switch
        {
            "ellipsis" => "TextOverflow.Ellipsis",
            "clip" => "TextOverflow.Clip",
            _ => null,
        };

        private static string? ParseCursor(string value) => value switch
        {
            "default" => "Cursor.Default",
            "pointer" => "Cursor.Pointer",
            "text" => "Cursor.Text",
            "move" => "Cursor.Move",
            "not-allowed" => "Cursor.NotAllowed",
            "crosshair" => "Cursor.Crosshair",
            "grab" => "Cursor.Grab",
            "grabbing" => "Cursor.Grabbing",
            "wait" => "Cursor.Wait",
            "help" => "Cursor.Help",
            "none" => "Cursor.None",
            _ => null,
        };

        private static string? ParsePosition(string value) => value switch
        {
            "static" => "Position.Static",
            "relative" => "Position.Relative",
            "absolute" => "Position.Absolute",
            "fixed" => "Position.Fixed",
            "sticky" => "Position.Sticky",
            _ => null,
        };

        private static string? ParseVisibility(string value) => value switch
        {
            "visible" => "Visibility.Visible",
            "hidden" => "Visibility.Hidden",
            _ => null,
        };

        private static string? ParseWhiteSpace(string value) => value switch
        {
            "normal" => "WhiteSpace.Normal",
            "nowrap" => "WhiteSpace.NoWrap",
            "pre" => "WhiteSpace.Pre",
            "pre-wrap" => "WhiteSpace.PreWrap",
            _ => null,
        };

        private static string? ParsePointerEvents(string value) => value switch
        {
            "auto" => "PointerEvents.Auto",
            "none" => "PointerEvents.None",
            _ => null,
        };

        /// <summary>
        /// Parses CSS border shorthand: "1px solid #rgba" or "2px #color" or "1px solid".
        /// Returns a C# BorderEdges initializer, or null if unrecognised.
        /// </summary>
        private static string? ParseBorderShorthand(string value)
        {
            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            float width = 0;
            string colourExpr = "PaperColour.White";
            bool hasColour = false;

            foreach (var part in parts)
            {
                var p = part.Trim().Trim('"', '\'');
                if (p.EndsWith("px", StringComparison.OrdinalIgnoreCase) &&
                    double.TryParse(p[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double w))
                {
                    width = (float)w;
                }
                else if (double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double wn))
                {
                    width = (float)wn;
                }
                else if (p.StartsWith('#'))
                {
                    colourExpr = ParseHexColor(p);
                    hasColour = true;
                }
                else if (p is "none" or "solid" or "dashed" or "dotted" or "hidden")
                {
                    if (p == "none") return "new BorderEdges(Border.None)";
                    // border-style is implicit in Border (always solid for now)
                }
            }

            if (width <= 0 && !hasColour) return null;
            return $"new BorderEdges(new Border({width}f, {colourExpr}))";
        }

        /// <summary>
        /// Like <see cref="ParseBorderShorthand"/> but returns a <c>Border</c> struct expression
        /// (used for individual border-side properties like BorderTop).
        /// </summary>
        private static string? ParseBorderSideShorthand(string value)
        {
            if (string.Equals(value.Trim(), "none", StringComparison.OrdinalIgnoreCase))
                return "Border.None";

            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            float width = 0;
            string colourExpr = "PaperColour.White";
            bool hasColour = false;

            foreach (var part in parts)
            {
                var p = part.Trim().Trim('"', '\'');
                if (p.EndsWith("px", StringComparison.OrdinalIgnoreCase) &&
                    double.TryParse(p[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double w))
                {
                    width = (float)w;
                }
                else if (double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double wn))
                {
                    width = (float)wn;
                }
                else if (p.StartsWith('#'))
                {
                    colourExpr = ParseHexColor(p);
                    hasColour = true;
                }
                else if (p is "none")
                {
                    return "Border.None";
                }
                // "solid", "dashed" etc. are ignored (only solid supported)
            }

            if (width <= 0 && !hasColour) return null;
            return $"new Border({width}f, {colourExpr})";
        }

        private static string ParseCssLengthOrString(string trimmedValue, string propertyName)
        {
            if (trimmedValue.EndsWith('%') && double.TryParse(trimmedValue[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double pct))
                return $"Length.Percent({pct})";

            if (trimmedValue.EndsWith("em", StringComparison.OrdinalIgnoreCase) && double.TryParse(trimmedValue[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double em))
                return $"Length.Em({em})";

            if (trimmedValue.EndsWith("px", StringComparison.OrdinalIgnoreCase) && double.TryParse(trimmedValue[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double px))
            {
                if (string.Equals(propertyName, "BorderRadius", StringComparison.OrdinalIgnoreCase))
                    return $"{px}f";
                if (string.Equals(propertyName, "Padding", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "Margin", StringComparison.OrdinalIgnoreCase))
                    return $"new Thickness({px}f)";
                return $"Length.Px({px})";
            }

            // If the value looks like a C# identifier (variable reference) and the property is a colour type,
            // emit new PaperColour(varName) so that string-typed context values work correctly.
            bool isColourProp = propertyName is "Background" or "Color" or "BorderColor" or "OutlineColor";
            if (isColourProp && Regex.IsMatch(trimmedValue, @"^[a-zA-Z_]\w*$"))
                return $"new PaperColour({trimmedValue})";

            return $"\"{trimmedValue}\"";
        }

        /// <summary>Parses <c>rgb(r,g,b)</c> or <c>rgba(r,g,b,a)</c> into a PaperColour initializer.</summary>
        private static string? ParseRgbColor(string value)
        {
            var m = Regex.Match(value.Trim(), @"rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([\d.]+))?\s*\)", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            int r = int.Parse(m.Groups[1].Value);
            int g = int.Parse(m.Groups[2].Value);
            int b = int.Parse(m.Groups[3].Value);
            float a = m.Groups[4].Success
                ? float.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture)
                : 1f;
            return $"new PaperColour({r / 255.0f}f, {g / 255.0f}f, {b / 255.0f}f, {a}f)";
        }

        private static string ParseHexColor(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 3)
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";

            if (hex.Length == 6)
            {
                int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return $"new PaperColour({r / 255.0f}f, {g / 255.0f}f, {b / 255.0f}f, 1f)";
            }

            if (hex.Length == 8)
            {
                int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                int a = int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                return $"new PaperColour({r / 255.0f}f, {g / 255.0f}f, {b / 255.0f}f, {a / 255.0f}f)";
            }

            return "new PaperColour(0f, 0f, 0f, 1f)";
        }
    }
}

