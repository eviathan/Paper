using System.Linq;
using System.Text;

namespace Paper.CSX.Syntax
{
    /// <summary>
    /// Lowers CSX AST into a C# expression that constructs Paper UINodes.
    /// This is intentionally conservative: it supports Paper intrinsics and treats { ... } as opaque code.
    /// </summary>
    public sealed class CSXCodegen
    {
        public string Generate(CSXElement root)
        {
            return GenElement(root);
        }

        private static string GenElement(CSXElement el)
        {
            // Intrinsics mapping: PascalCase tag to UI.* factory
            return el.Name switch
            {
                "Box" => GenBox(el),
                "Text" => GenText(el),
                "Button" => GenButton(el),
                "Input" => GenInput(el),
                "Image" => GenImage(el),
                "Scroll" => GenScroll(el),
                "Viewport" => GenViewport(el),
                "Checkbox" => GenCheckbox(el),
                "Textarea" => GenTextarea(el),
                "Table" => GenTable(el),
                "TableRow" => GenTableRow(el),
                "TableCell" => GenTableCell(el),
                "RadioGroup" => GenRadioGroup(el),
                "Select" => GenSelect(el),
                _ => GenCustom(el),
            };
        }

        private static string GenBox(CSXElement el)
        {
            var pb = new StringBuilder();
            pb.Append("new PropsBuilder()");

            ApplyCommonProps(pb, el);
            ApplyChildren(pb, el);

            pb.Append(".Build()");
            string? key = GetKey(el);
            return key != null ? $"UI.Box({pb}, {key})" : $"UI.Box({pb})";
        }

        private static string GenScroll(CSXElement el)
        {
            // Scroll currently has only UI.Scroll(style, children). Prefer that.
            string style = GetStyle(el) ?? "StyleSheet.Empty";
            var children = GenChildrenArray(el);
            return children.Count > 0
                ? $"UI.Scroll({style}, {string.Join(", ", children)})"
                : $"UI.Scroll({style})";
        }

        private static string GenViewport(CSXElement el)
        {
            // Viewport(textureHandle, style?)
            string tex = "0";
            foreach (var a in el.Attributes)
            {
                if (a.Name is "textureHandle" or "texture")
                {
                    tex = a.Value is CSXExpressionValue ev ? ev.Code : a.Value is CSXStringValue sv ? sv.Value : "0";
                }
            }
            string? style = GetStyle(el);
            return style != null
                ? $"UI.Viewport({tex}, {style})"
                : $"UI.Viewport({tex})";
        }

        private static string GenText(CSXElement el)
        {
            string content = GenInterpolatedText(el.Children);
            var pb = new StringBuilder();
            pb.Append("new PropsBuilder()");
            pb.Append($".Text({content})");
            ApplyCommonProps(pb, el);
            pb.Append(".Build()");
            string? key = GetKey(el);
            return key != null ? $"new UINode(\"text\", {pb}, {key})" : $"new UINode(\"text\", {pb})";
        }

        private static string GenButton(CSXElement el)
        {
            bool hasInteractionStyle = el.Attributes.Any(a => a.Name is "hoverStyle" or "activeStyle" or "focusStyle");
            string? key = GetKey(el);

            if (hasInteractionStyle || key != null)
            {
                // Use PropsBuilder path so all props (including interaction styles and key) are supported.
                var pb = new StringBuilder("new PropsBuilder()");
                pb.Append($".Text({GenInterpolatedText(el.Children)})");
                ApplyCommonProps(pb, el);  // handles style, className, onClick, hoverStyle, etc.
                pb.Append(".Build()");
                return key != null ? $"new UINode(\"button\", {pb}, {key})" : $"new UINode(\"button\", {pb})";
            }

            string label = GenInterpolatedText(el.Children);
            string onClick = "null";
            string style = GetStyle(el) ?? "StyleSheet.Empty";

            foreach (var a in el.Attributes)
            {
                if (a.Name is "onClick" or "onclick")
                    onClick = ToActionLambda(a.Value);
            }

            return $"UI.Button({label}, {onClick}, {style})";
        }

        private static string GenInput(CSXElement el)
        {
            string value = "\"\"";
            string onChange = "null";
            string style = GetStyle(el) ?? "StyleSheet.Empty";

            foreach (var a in el.Attributes)
            {
                if (a.Name is "value")
                    value = a.Value is CSXExpressionValue ev ? ev.Code : Quote(a.Value is CSXStringValue sv ? sv.Value : a.Value is CSXBareValue bv ? bv.Value : "");
                else if (a.Name is "onChange" or "onchange")
                {
                    // Expected: (v) => ...
                    onChange = ToStringLambda(a.Value);
                }
            }

            return $"UI.Input({value}, {onChange}, {style})";
        }

        private static string GenCheckbox(CSXElement el)
        {
            string @checked = "false";
            string onCheckedChange = "null";
            string? label = null;
            string style = GetStyle(el) ?? "StyleSheet.Empty";

            foreach (var a in el.Attributes)
            {
                if (a.Name is "checked")
                    @checked = a.Value is CSXExpressionValue ev ? ev.Code : (a.Value is CSXBareValue bv && bv.Value == "true" ? "true" : "false");
                else if (a.Name is "onCheckedChange" or "oncheckedchange")
                    onCheckedChange = ToActionBoolLambda(a.Value);
                else if (a.Name is "label")
                    label = a.Value is CSXExpressionValue ev2 ? ev2.Code : (a.Value is CSXStringValue sv ? Quote(sv.Value) : "null");
            }
            if (label == null && el.Children.Count > 0)
                label = GenInterpolatedText(el.Children);

            string labelArg = label != null ? label : "null";
            return $"UI.Checkbox({@checked}, {onCheckedChange}, {labelArg}, {style})";
        }

        private static string GenTextarea(CSXElement el)
        {
            string value = "\"\"";
            string onChange = "null";
            string rows = "null";
            string style = GetStyle(el) ?? "StyleSheet.Empty";

            foreach (var a in el.Attributes)
            {
                if (a.Name is "value")
                    value = a.Value is CSXExpressionValue ev ? ev.Code : Quote(a.Value is CSXStringValue sv ? sv.Value : a.Value is CSXBareValue bv ? bv.Value : "");
                else if (a.Name is "onChange" or "onchange")
                    onChange = ToStringLambda(a.Value);
                else if (a.Name is "rows")
                    rows = a.Value is CSXExpressionValue evr ? evr.Code : (a.Value is CSXBareValue bvr ? bvr.Value : "null");
            }
            return $"UI.Textarea({value}, {onChange}, {rows}, {style})";
        }

        private static string GenTable(CSXElement el)
        {
            string style = GetStyle(el) ?? "StyleSheet.Empty";
            var children = GenChildrenArray(el);
            if (children.Count > 0)
                return $"UI.Table({style}, null, {string.Join(", ", children)})";
            return $"UI.Table({style})";
        }

        private static string GenTableRow(CSXElement el)
        {
            string style = GetStyle(el) ?? "StyleSheet.Empty";
            var children = GenChildrenArray(el);
            if (children.Count > 0)
                return $"UI.TableRow({style}, null, {string.Join(", ", children)})";
            return $"UI.TableRow({style})";
        }

        private static string GenTableCell(CSXElement el)
        {
            string style = GetStyle(el) ?? "StyleSheet.Empty";
            var children = GenChildrenArray(el);
            if (children.Count > 0)
                return $"UI.TableCell({style}, null, {string.Join(", ", children)})";
            return $"UI.TableCell({style})";
        }

        private static string GenRadioGroup(CSXElement el)
        {
            string options = "Array.Empty<(string Value, string Label)>()";
            string selectedValue = "\"\"";
            string onSelect = "null";
            string style = GetStyle(el) ?? "StyleSheet.Empty";

            foreach (var a in el.Attributes)
            {
                if (a.Name is "options")
                    options = a.Value is CSXExpressionValue ev ? ev.Code : options;
                else if (a.Name is "selectedValue")
                    selectedValue = a.Value is CSXExpressionValue ev2 ? ev2.Code : (a.Value is CSXStringValue sv ? Quote(sv.Value) : selectedValue);
                else if (a.Name is "onSelect" or "onselect")
                    onSelect = ToStringLambda(a.Value);
            }
            return $"UI.RadioGroup({options}, {selectedValue}, {onSelect}, {style})";
        }

        private static string GenSelect(CSXElement el)
        {
            string options = "Array.Empty<(string Value, string Label)>()";
            string selectedValue = "\"\"";
            string onSelect = "null";
            string style = GetStyle(el) ?? "StyleSheet.Empty";

            foreach (var a in el.Attributes)
            {
                if (a.Name is "options")
                    options = a.Value is CSXExpressionValue ev ? ev.Code : options;
                else if (a.Name is "value" or "selectedValue")
                    selectedValue = a.Value is CSXExpressionValue ev2 ? ev2.Code : (a.Value is CSXStringValue sv ? Quote(sv.Value) : selectedValue);
                else if (a.Name is "onChange" or "onSelect" or "onchange" or "onselect")
                    onSelect = ToStringLambda(a.Value);
            }
            return $"UI.Component(Paper.Core.Components.Primitives.SelectComponent, new PropsBuilder().Set(\"options\", {options}).Set(\"selectedValue\", {selectedValue}).Set(\"onSelect\", {onSelect}).Style({style}).Build())";
        }

        private static string GenImage(CSXElement el)
        {
            string src = "\"\"";
            foreach (var a in el.Attributes)
            {
                if (a.Name == "src")
                {
                    src = a.Value is CSXExpressionValue ev ? ev.Code : Quote(a.Value is CSXStringValue sv ? sv.Value : a.Value is CSXBareValue bv ? bv.Value : "");
                    break;
                }
            }
            string? style = GetStyle(el);
            return style != null ? $"UI.Image({src}, {style})" : $"UI.Image({src})";
        }

        private static string GenCustom(CSXElement el)
        {
            // Treat custom components as function components with Props built from attributes.
            // Example: <MyComp foo={bar} /> -> UI.Component(MyComp, new PropsBuilder().Set(\"foo\", bar).Build())
            var pb = new StringBuilder();
            pb.Append("new PropsBuilder()");

            foreach (var a in el.Attributes)
            {
                if (a.Name == "style")
                {
                    var s = GetStyle(el);
                    if (s != null) pb.Append($".Style({s})");
                    continue;
                }

                if (a.Name is "hoverStyle" or "activeStyle" or "focusStyle")
                {
                    var s = GetStyleAttr(el, a.Name);
                    if (s != null) pb.Append($".Set(\"{a.Name}\", {s})");
                    continue;
                }

                if (a.Name == "className")
                {
                    string cls = a.Value is CSXStringValue sv ? Quote(sv.Value) :
                                 a.Value is CSXExpressionValue ev ? ev.Code :
                                 Quote(((CSXBareValue)a.Value).Value);
                    pb.Append($".ClassName({cls})");
                    continue;
                }

                if (a.Name == "id")
                {
                    string idVal = a.Value is CSXStringValue idSv ? Quote(idSv.Value) :
                                   a.Value is CSXExpressionValue idEv ? idEv.Code :
                                   Quote(((CSXBareValue)a.Value).Value);
                    pb.Append($".Id({idVal})");
                    continue;
                }

                if (a.Name == "key") continue; // key is a UINode constructor arg, not a prop

                if (a.Value is CSXExpressionValue ev2)
                    pb.Append($".Set(\"{a.Name}\", {ev2.Code})");
                else if (a.Value is CSXStringValue sv2)
                    pb.Append($".Set(\"{a.Name}\", {Quote(sv2.Value)})");
                else if (a.Value is CSXBareValue bv2)
                    pb.Append($".Set(\"{a.Name}\", {Quote(bv2.Value)})");
            }

            ApplyChildren(pb, el);
            pb.Append(".Build()");

            string? key = GetKey(el);
            return key != null ? $"UI.Component({el.Name}, {pb}, {key})" : $"UI.Component({el.Name}, {pb})";
        }

        private static void ApplyCommonProps(StringBuilder pb, CSXElement el)
        {
            foreach (var a in el.Attributes)
            {
                switch (a.Name)
                {
                    case "style":
                    {
                        var s = GetStyle(el);
                        if (s != null) pb.Append($".Style({s})");
                        break;
                    }
                    case "className":
                    {
                        string cls = a.Value is CSXStringValue sv ? Quote(sv.Value) :
                                     a.Value is CSXExpressionValue ev ? ev.Code :
                                     a.Value is CSXBareValue bv ? Quote(bv.Value) : "\"\"";
                        pb.Append($".ClassName({cls})");
                        break;
                    }
                    case "id":
                    {
                        string idVal = a.Value is CSXStringValue idSv ? Quote(idSv.Value) :
                                       a.Value is CSXExpressionValue idEv ? idEv.Code :
                                       a.Value is CSXBareValue idBv ? Quote(idBv.Value) : "\"\"";
                        pb.Append($".Id({idVal})");
                        break;
                    }
                    case "onClick":
                    {
                        pb.Append($".OnClick({ToActionLambda(a.Value)})");
                        break;
                    }
                    case "hoverStyle":
                    case "activeStyle":
                    case "focusStyle":
                    {
                        var s = GetStyleAttr(el, a.Name);
                        if (s != null) pb.Append($".Set(\"{a.Name}\", {s})");
                        break;
                    }
                }
            }
        }

        private static void ApplyChildren(StringBuilder pb, CSXElement el)
        {
            var kids = GenChildrenArray(el);
            if (kids.Count == 0) return;
            // When any child is a code expression it may return UINode[] (e.g. .Select(...).ToArray()).
            // UI.Nodes() accepts mixed UINode / IEnumerable<UINode> and flattens them safely.
            bool hasExpr = el.Children.Any(c => c is CSXExpression ex && (ex.Code?.Trim().Length ?? 0) > 0 && !ex.Code!.TrimStart().StartsWith("/*"));
            if (hasExpr)
                pb.Append($".Children(UI.Nodes({string.Join(", ", kids)}))");
            else
                pb.Append($".Children({string.Join(", ", kids)})");
        }

        private static List<string> GenChildrenArray(CSXElement el)
        {
            var kids = new List<string>();
            foreach (var c in el.Children)
            {
                switch (c)
                {
                    case CSXChildElement ce:
                        kids.Add(GenElement(ce.Element));
                        break;
                    case CSXText:
                        // Ignore loose text nodes for non-Text elements (common whitespace/newlines).
                        break;
                    case CSXExpression ex:
                        // Expression child expected to be a UINode or collection; pass through. Skip empty or JSX comments {/* */} (emitted as C# comments and become missing args).
                        var code = ex.Code?.Trim() ?? "";
                        if (code.Length == 0 || code.StartsWith("/*", StringComparison.Ordinal))
                            break;
                        // Recursively compile any JSX elements embedded inside the expression (e.g. lambda bodies).
                        kids.Add(TransformJsxInExpression(ex.Code!));
                        break;
                }
            }
            return kids;
        }

        /// <summary>
        /// Scans a C# expression string for embedded JSX elements (e.g. inside lambda bodies or ternaries)
        /// and compiles each one to a UINode expression. Only triggers when &lt; is followed by an uppercase
        /// letter at a position that is not preceded by a word character (to avoid matching generics like
        /// List&lt;T&gt; or comparisons like a &lt; b).
        /// </summary>
        private static string TransformJsxInExpression(string code)
        {
            if (!code.Contains('<')) return code;

            var sb = new StringBuilder();
            int i = 0;
            while (i < code.Length)
            {
                // Skip string literals to avoid false JSX detection.
                if (code[i] == '"' || code[i] == '\'')
                {
                    char q = code[i];
                    sb.Append(code[i++]);
                    while (i < code.Length && code[i] != q)
                    {
                        if (code[i] == '\\' && i + 1 < code.Length) sb.Append(code[i++]);
                        sb.Append(code[i++]);
                    }
                    if (i < code.Length) sb.Append(code[i++]);
                    continue;
                }

                // Detect JSX: '<' followed by an uppercase letter at an expression position
                // (not preceded by a word character, which would indicate generics or comparison).
                if (code[i] == '<' &&
                    i + 1 < code.Length &&
                    char.IsUpper(code[i + 1]) &&
                    IsExpressionPosition(code, i))
                {
                    var sub = code.Substring(i);
                    var parser = new CSXParser2(sub);
                    try
                    {
                        var el = parser.ParseFirstElement();
                        int consumed = parser.Position;
                        sb.Append(new CSXCodegen().Generate(el));
                        i += consumed;
                        continue;
                    }
                    catch { /* Not valid JSX — treat '<' as a regular character */ }
                }

                sb.Append(code[i++]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns true when the '&lt;' at <paramref name="pos"/> appears in an expression context
        /// (not as a generic type parameter or comparison operator). Heuristic: the previous
        /// non-whitespace character must NOT be a word character (letter, digit, underscore).
        /// </summary>
        private static bool IsExpressionPosition(string code, int pos)
        {
            int j = pos - 1;
            while (j >= 0 && char.IsWhiteSpace(code[j])) j--;
            if (j < 0) return true; // start of expression
            char prev = code[j];
            return !char.IsLetterOrDigit(prev) && prev != '_' && prev != ')' && prev != ']';
        }

        private static string? GetKey(CSXElement el)
        {
            foreach (var a in el.Attributes)
            {
                if (a.Name != "key") continue;
                return a.Value is CSXExpressionValue ev ? ev.Code :
                       a.Value is CSXStringValue sv ? Quote(sv.Value) :
                       a.Value is CSXBareValue bv ? Quote(bv.Value) : null;
            }
            return null;
        }

        private static string GenInterpolatedText(IReadOnlyList<CSXChild> children)
        {
            var parts = new List<(bool isExpr, string text)>();
            foreach (var c in children)
            {
                switch (c)
                {
                    case CSXText t:
                        parts.Add((false, t.Text));
                        break;
                    case CSXExpression e:
                        parts.Add((true, e.Code));
                        break;
                }
            }

            // Collapse whitespace like JSX: replace any whitespace run (including newlines) with a single space.
            string Normalize(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");

            if (parts.Count == 0) return Quote("");
            if (parts.Count == 1 && !parts[0].isExpr) return Quote(Normalize(parts[0].text).Trim());

            // For multi-part strings: trim leading whitespace on first text segment and trailing on last.
            var normalized = parts.Select((p, i) =>
            {
                if (p.isExpr) return p;
                string t = Normalize(p.text);
                if (i == 0) t = t.TrimStart();
                if (i == parts.Count - 1) t = t.TrimEnd();
                return (p.isExpr, t);
            }).ToList();

            var sb = new StringBuilder();
            sb.Append("$\"");
            foreach (var (isExpr, text) in normalized)
            {
                if (!isExpr)
                {
                    sb.Append(EscapeForInterpolatedString(text));
                }
                else
                {
                    sb.Append('{');
                    sb.Append(text.Trim());
                    sb.Append('}');
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string EscapeForInterpolatedString(string s)
        {
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("{", "{{")
                .Replace("}", "}}");
        }

        private static string Quote(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string? GetStyle(CSXElement el) => GetStyleAttr(el, "style");

        private static string? GetStyleAttr(CSXElement el, string attrName)
        {
            foreach (var a in el.Attributes)
            {
                if (a.Name != attrName) continue;
                if (a.Value is CSXExpressionValue ev)
                    return InlineStyleTranslator.Translate(ev.Code);
                if (a.Value is CSXStringValue sv)
                    return InlineStyleTranslator.Translate(sv.Value);
            }
            return null;
        }

        private static string ToActionLambda(CSXAttributeValue v)
        {
            string code = v switch
            {
                CSXExpressionValue ev => ev.Code,
                CSXStringValue sv => sv.Value,
                CSXBareValue bv => bv.Value,
                _ => ""
            };
            code = code.Trim();

            // Common CSX shape: () => expr
            if (code.StartsWith("() =>", StringComparison.Ordinal))
            {
                var body = code.Substring("() =>".Length).Trim();
                if (!body.StartsWith("{"))
                {
                    if (!body.EndsWith(";")) body += ";";
                    body = "{ " + body + " }";
                }
                return $"() => {body}";
            }

            return code.Length == 0 ? "null" : code;
        }

        private static string ToStringLambda(CSXAttributeValue v)
        {
            string code = v switch
            {
                CSXExpressionValue ev => ev.Code,
                CSXStringValue sv => sv.Value,
                CSXBareValue bv => bv.Value,
                _ => ""
            };
            return code.Length == 0 ? "null" : code;
        }

        /// <summary>Wraps an expression so it has type Action&lt;bool&gt; (e.g. UseState setter is Action&lt;object?&gt;).</summary>
        private static string ToActionBoolLambda(CSXAttributeValue v)
        {
            string code = v switch
            {
                CSXExpressionValue ev => ev.Code,
                CSXStringValue sv => sv.Value,
                CSXBareValue bv => bv.Value,
                _ => ""
            };
            code = code.Trim();
            if (code.Length == 0) return "null";
            // If it's already a lambda, use as-is (caller may need Action<bool>; cast at call site if needed).
            if (code.Contains("=>", StringComparison.Ordinal)) return code;
            return $"(bool b) => {code}(b)";
        }
    }
}

