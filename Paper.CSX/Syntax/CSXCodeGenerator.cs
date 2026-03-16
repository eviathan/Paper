using System.Linq;
using System.Text;

namespace Paper.CSX.Syntax
{
    /// <summary>
    /// Simple formatter for CSX to C#
    /// </summary>
    public sealed class CSXCodeGenerator
    {
        [System.ThreadStatic]
        private static IReadOnlySet<string>? _classNames;

        public string Generate(CSXElement root, IReadOnlySet<string>? classComponentNames = null)
        {
            _classNames = classComponentNames;
            try { return GenerateElement(root, 1); }
            finally { _classNames = null; }
        }

        private static string GenerateElement(CSXElement element, int indent = 0)
        {
            var indentStr = new string(' ', indent * 4);
            var nextIndent = new string(' ', (indent + 1) * 4);
            
            return element.Name switch
            {
                "Box" => GenerateBox(element, indent),
                "Text" => GenerateText(element, indent),
                "Button" => GenerateButton(element, indent),
                "Input" => GenerateInput(element, indent),
                "Image" => GenerateImage(element, indent),
                "Scroll" => GenerateScroll(element, indent),
                "Viewport" => GenerateViewport(element, indent),
                "Checkbox" => GenerateCheckbox(element, indent),
                "Textarea" => GenenerateTextArea(element, indent),
                "Table" => GenerateTable(element, indent),
                "TableRow" => GenerateTableRow(element, indent),
                "TableCell" => GenerateTableCell(element, indent),
                "RadioGroup" => GenerateRadioGroup(element, indent),
                "Select" => GenerateSelect(element, indent),
                "Portal" => GeneratePortal(element, indent),
                _ => GenerateCustom(element, indent),
            };
        }

        private static string GenerateBox(CSXElement element, int indent = 0)
        {
            var props = BuildProps(element);
            string? key = GetKey(element);

            if (key != null)
                return $"UI.Box(\n{props},\n{key})";
            else
                return $"UI.Box(\n{props})";
        }

        private static string GenerateText(CSXElement element, int indent = 0)
        {
            string content = GenerateInterpolatedText(element.Children);
            var props = BuildProps(element, extraProp: $".Text({content})");
            string? key = GetKey(element);

            if (key != null)
                return $"new UINode(\"text\",\n{props},\n{key})";
            else
                return $"new UINode(\"text\",\n{props})";
        }

        private static string GenerateButton(CSXElement element, int indent = 0)
        {
            bool hasInteractionStyle = element.Attributes
                .Any(attribute => attribute.Name is "hoverStyle" or "activeStyle" or "focusStyle");

            string? key = GetKey(element);

            if (hasInteractionStyle || key != null)
            {
                var props = BuildProps(element, extraProp: $".Text({GenerateInterpolatedText(element.Children)})");
                if (key != null)
                    return $"new UINode(\"button\",\n{props},\n{key})";
                else
                    return $"new UINode(\"button\",\n{props})";
            }

            string label = GenerateInterpolatedText(element.Children);
            string onClick = "null";
            string style = GetStyle(element) ?? "StyleSheet.Empty";

            foreach (var attribute in element.Attributes)
            {
                if (attribute.Name is "onClick" or "onclick")
                    onClick = ToActionLambda(attribute.Value);
            }

            return $"UI.Button({label}, {onClick}, {style})";
        }

        private static string BuildProps(CSXElement el, int indent = 0, string? extraProp = null)
        {
            var sb = new StringBuilder();
            sb.Append("new PropsBuilder()");

            foreach (var a in el.Attributes)
            {
                switch (a.Name)
                {
                    case "style":
                    {
                        var s = GetStyle(el);
                        if (s != null) sb.Append($"\n.Style({s})");
                        break;
                    }
                    case "className":
                    {
                        string cls = a.Value is CSXStringValue sv ? Quote(sv.Value) :
                                     a.Value is CSXExpressionValue ev ? ev.Code :
                                     a.Value is CSXBareValue bv ? Quote(bv.Value) : "\"\"";
                        sb.Append($"\n.ClassName({cls})");
                        break;
                    }
                    case "id":
                    {
                        string idVal = a.Value is CSXStringValue idSv ? Quote(idSv.Value) :
                                       a.Value is CSXExpressionValue idEv ? idEv.Code :
                                       a.Value is CSXBareValue idBv ? Quote(idBv.Value) : "\"\"";
                        sb.Append($"\n.Id({idVal})");
                        break;
                    }
                    case "onClick":
                    {
                        sb.Append($"\n.OnClick({ToActionLambda(a.Value)})");
                        break;
                    }
                    case "hoverStyle":
                    case "activeStyle":
                    case "focusStyle":
                    {
                        var s = GetStyleAttr(el, a.Name);
                        if (s != null) sb.Append($"\n.Set(\"{a.Name}\", {s})");
                        break;
                    }
                }
            }

            if (extraProp != null)
                sb.Append($"\n{extraProp}");

            var kids = el.Children.Select(c =>
            {
                if (c is CSXChildElement ce)
                    return GenerateElement(ce.Element);
                if (c is CSXText ct)
                {
                    var text = ct.Text.Trim();
                    if (string.IsNullOrEmpty(text)) return null;
                    return $"new UINode(\"text\", new PropsBuilder().Text({Quote(text)}).Build())";
                }
                if (c is CSXExpression ex)
                {
                    var code = ex.Code?.Trim() ?? "";
                    if (code.Length == 0 || code.StartsWith("/*", StringComparison.Ordinal))
                        return null;
                    return TransformJsxInExpression(ex.Code!);
                }
                return null;
            }).Where(x => x != null).ToList();

            bool isTextElement = el.Name == "Text";
            if (kids.Count > 0 && !isTextElement)
            {
                bool hasExpr = el.Children.Any(c => c is CSXExpression ex && (ex.Code?.Trim().Length ?? 0) > 0 && !ex.Code!.TrimStart().StartsWith("/*"));
                if (hasExpr)
                {
                    sb.Append($"\n.Children(UI.Nodes({string.Join(", ", kids)}))");
                }
                else
                {
                    sb.Append($"\n.Children({string.Join(", ", kids)})");
                }
            }

            sb.Append("\n.Build()");
            return sb.ToString();
        }

        // Helper methods
        private static string GenerateInterpolatedText(IReadOnlyList<CSXChild> children)
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

            string Normalize(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");

            if (parts.Count == 0) return Quote("");
            if (parts.Count == 1 && !parts[0].isExpr) return Quote(Normalize(parts[0].text).Trim());

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
            if (code.Contains("=>", StringComparison.Ordinal)) return code;
            return $"(bool b) => {code}(b)";
        }

        private static string TransformJsxInExpression(string code, int indent = 0)
        {
            if (!code.Contains('<')) return code;

            var sb = new StringBuilder();
            int i = 0;
            while (i < code.Length)
            {
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

                if (code[i] == '<' &&
                    i + 1 < code.Length &&
                    char.IsUpper(code[i + 1]) &&
                    IsExpressionPosition(code, i))
                {
                    var sub = code.Substring(i);
                    var parser = new CSXElementParser(sub);
                    try
                    {
                        var el = parser.ParseFirstElement();
                        int consumed = parser.Position;
                        sb.Append(CSXCodeGenerator.GenerateElement(el, indent));
                        i += consumed;
                        continue;
                    }
                    catch { }
                }

                sb.Append(code[i++]);
            }
            return sb.ToString();
        }

        private static bool IsExpressionPosition(string code, int pos)
        {
            int j = pos - 1;
            while (j >= 0 && char.IsWhiteSpace(code[j])) j--;
            if (j < 0) return true;
            char prev = code[j];
            return !char.IsLetterOrDigit(prev) && prev != '_' && prev != ')' && prev != ']';
        }

        private static string GenerateInput(CSXElement el, int indent = 0)
        {
            string value = "\"\"";
            string onChange = "null";
            string style = GetStyle(el) ?? "StyleSheet.Empty";

            foreach (var a in el.Attributes)
            {
                if (a.Name is "value")
                    value = a.Value is CSXExpressionValue ev ? ev.Code : Quote(a.Value is CSXStringValue sv ? sv.Value : a.Value is CSXBareValue bv ? bv.Value : "");
                else if (a.Name is "onChange" or "onchange")
                    onChange = ToStringLambda(a.Value);
            }

            return $"UI.Input({value}, {onChange}, {style})";
        }

        private static string GenerateImage(CSXElement el, int indent = 0)
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

        private static string GenerateScroll(CSXElement element, int indent = 0)
        {
            string style = GetStyle(element) ?? "StyleSheet.Empty";
            var children = GenerateChildrenArray(element);
            if (children.Count > 0)
                return $"UI.Scroll({style}, {string.Join(", ", children)})";
            return $"UI.Scroll({style})";
        }

        private static string GenerateViewport(CSXElement element, int indent = 0)
        {
            string texture = "0";
            foreach (var attribute in element.Attributes)
            {
                if (attribute.Name is "textureHandle" or "texture")
                {
                    texture = attribute.Value is CSXExpressionValue expressionValue
                        ? expressionValue.Code
                        : attribute.Value is CSXStringValue stringValue
                            ? stringValue.Value
                            : "0";
                }
            }

            string? style = GetStyle(element);
            return style != null
                ? $"UI.Viewport({texture}, {style})"
                : $"UI.Viewport({texture})";
        }

        private static string GenerateCheckbox(CSXElement el, int indent = 0)
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
                label = GenerateInterpolatedText(el.Children);

            string labelArg = label != null ? label : "null";
            return $"UI.Checkbox({@checked}, {onCheckedChange}, {labelArg}, {style})";
        }

        private static string GenenerateTextArea(CSXElement el, int indent = 0)
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

        private static string GenerateTable(CSXElement el, int indent = 0)
        {
            string style = GetStyle(el) ?? "StyleSheet.Empty";
            var children = GenerateChildrenArray(el);
            if (children.Count > 0)
                return $"UI.Table({style}, null, {string.Join(", ", children)})";
            return $"UI.Table({style})";
        }

        private static string GenerateTableRow(CSXElement el, int indent = 0)
        {
            string style = GetStyle(el) ?? "StyleSheet.Empty";
            var children = GenerateChildrenArray(el);
            if (children.Count > 0)
                return $"UI.TableRow({style}, null, {string.Join(", ", children)})";
            return $"UI.TableRow({style})";
        }

        private static string GenerateTableCell(CSXElement el, int indent = 0)
        {
            string style = GetStyle(el) ?? "StyleSheet.Empty";
            var children = GenerateChildrenArray(el);
            if (children.Count > 0)
                return $"UI.TableCell({style}, null, {string.Join(", ", children)})";
            return $"UI.TableCell({style})";
        }

        private static string GenerateRadioGroup(CSXElement el, int indent = 0)
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

        private static string GenerateSelect(CSXElement el, int indent = 0)
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

        private static string GeneratePortal(CSXElement el, int indent = 0)
        {
            var children = GenerateChildrenArray(el);
            if (children.Count > 0)
                return $"UI.Portal(new UINode[]{{ {string.Join(", ", children)} }})";
            return "UI.Portal()";
        }

        private static string GenerateCustom(CSXElement el, int indent = 0)
        {
            var props = BuildProps(el);
            string? key = GetKey(el);

            bool isClass = _classNames?.Contains(el.Name) == true;
            if (isClass)
            {
                return key != null
                    ? $"UI.Component<{el.Name}>({props}, {key})"
                    : $"UI.Component<{el.Name}>({props})";
            }
            return key != null
                ? $"UI.Component({el.Name}, {props}, {key})"
                : $"UI.Component({el.Name}, {props})";
        }

        private static string GenerateCheckbox(CSXElement el)
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
                label = GenerateInterpolatedText(el.Children);

            string labelArg = label != null ? label : "null";
            return $"UI.Checkbox({@checked}, {onCheckedChange}, {labelArg}, {style})";
        }

        private static string GenenerateTextArea(CSXElement el)
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

        private static string GenerateTable(CSXElement el)
        {
            string style = GetStyle(el) ?? "StyleSheet.Empty";
            var children = GenerateChildrenArray(el);
            if (children.Count > 0)
                return $"UI.Table({style}, null, {string.Join(", ", children)})";
            return $"UI.Table({style})";
        }

        private static string GenerateTableRow(CSXElement el)
        {
            string style = GetStyle(el) ?? "StyleSheet.Empty";
            var children = GenerateChildrenArray(el);
            if (children.Count > 0)
                return $"UI.TableRow({style}, null, {string.Join(", ", children)})";
            return $"UI.TableRow({style})";
        }

        private static string GenerateTableCell(CSXElement el)
        {
            string style = GetStyle(el) ?? "StyleSheet.Empty";
            var children = GenerateChildrenArray(el);
            if (children.Count > 0)
                return $"UI.TableCell({style}, null, {string.Join(", ", children)})";
            return $"UI.TableCell({style})";
        }

        private static string GenerateRadioGroup(CSXElement el)
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

        private static List<string> GenerateChildrenArray(CSXElement el)
        {
            var kids = new List<string>();
            foreach (var c in el.Children)
            {
                switch (c)
                {
                    case CSXChildElement ce:
                        kids.Add(GenerateElement(ce.Element));
                        break;
                    case CSXText:
                        break;
                    case CSXExpression ex:
                        var code = ex.Code?.Trim() ?? "";
                        if (code.Length == 0 || code.StartsWith("/*", StringComparison.Ordinal))
                            break;
                        kids.Add(TransformJsxInExpression(ex.Code!));
                        break;
                }
            }
            return kids;
        }
    }
}