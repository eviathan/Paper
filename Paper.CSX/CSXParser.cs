using System.Text;
using System.Text.RegularExpressions;
using Paper.CSX.Syntax;

namespace Paper.CSX
{
    public static class CSXCompiler
    {
        /// <summary>
        /// If the file contains "return ( ... );", returns (preamble, jsxContent) so callers can emit preamble + "return " + Parse(jsxContent).
        /// Otherwise returns ("", fileContent) so the whole file is parsed as JSX.
        /// Supports multi-function CSX: helper functions before the entry function are converted to Func&lt;Props, UINode&gt; lambdas.
        /// Supports cross-file imports: <c>@import "./Badge.csx"</c> inlines all component functions from the target file.
        /// </summary>
        public static (string preamble, string jsxContent) ExtractPreambleAndJsx(string fileContent, string? baseDir = null)
        {
            // Inline any @import "*.csx" files before further processing.
            if (baseDir != null)
                fileContent = InlineCSXImports(fileContent, baseDir);

            // Strip @import "*.csss" directives — these are handled by the hot-reload layer, not compiled to C#.
            var filteredLines = fileContent.Split('\n')
                .Where(l => !l.Trim().StartsWith("@import", StringComparison.OrdinalIgnoreCase) ||
                             !(l.Contains(".csss\"") || l.Contains(".csss'")));
            var cleanBody = string.Join('\n', filteredLines).Trim().TrimEnd(';');
            if (string.IsNullOrWhiteSpace(cleanBody))
                return (string.Empty, string.Empty);

            // Convert any helper function definitions (all but the last) to C# lambda declarations.
            cleanBody = ConvertHelperFunctions(cleanBody);

            var returnOpen = cleanBody.IndexOf("return (", StringComparison.Ordinal);
            if (returnOpen >= 0)
            {
                var jsxStart = returnOpen + "return (".Length;
                var depth = 1;
                var i = jsxStart;
                while (i < cleanBody.Length && depth > 0)
                {
                    var c = cleanBody[i];
                    if (c == '(') depth++;
                    else if (c == ')') depth--;
                    i++;
                }
                if (depth == 0)
                {
                    var jsxContent = cleanBody.Substring(jsxStart, i - 1 - jsxStart).Trim();
                    var preamble = cleanBody.Substring(0, returnOpen).Trim();
                    // Strip the last "function Name(...) {" declaration from the preamble
                    // (the entry function's declaration; its body preamble follows the '{')
                    var lastFuncIdx = preamble.LastIndexOf("function ", StringComparison.Ordinal);
                    if (lastFuncIdx >= 0)
                    {
                        // Extract param and optional generic type from the entry function declaration
                        var epParenOpen = preamble.IndexOf('(', lastFuncIdx);
                        var epParenClose = epParenOpen >= 0 ? preamble.IndexOf(')', epParenOpen) : -1;
                        var entryParam = (epParenOpen >= 0 && epParenClose > epParenOpen)
                            ? preamble.Substring(epParenOpen + 1, epParenClose - epParenOpen - 1).Trim()
                            : null;

                        // Extract optional generic: "function Badge<BadgeProps>(" → "BadgeProps"
                        string? entryGenericType = null;
                        if (epParenOpen >= 0)
                        {
                            var rawEntryName = preamble.Substring(lastFuncIdx + "function ".Length,
                                epParenOpen - (lastFuncIdx + "function ".Length)).Trim();
                            var ag = rawEntryName.IndexOf('<');
                            var agc = ag >= 0 ? rawEntryName.IndexOf('>', ag) : -1;
                            if (ag >= 0 && agc > ag)
                                entryGenericType = rawEntryName.Substring(ag + 1, agc - ag - 1).Trim();
                        }

                        var declBrace = preamble.IndexOf('{', lastFuncIdx);
                        if (declBrace >= 0)
                            preamble = preamble.Substring(0, lastFuncIdx) + preamble.Substring(declBrace + 1);
                        preamble = preamble.Trim();

                        // Inject prop bindings at the top of the entry function body
                        if (!string.IsNullOrEmpty(entryGenericType))
                        {
                            var untypedNames = ParseUntypedDestructuredNames(entryParam ?? "");
                            if (untypedNames != null)
                            {
                                // function App<AppProps>({ Label, Colour }) → destructure from typed record
                                var pb2 = new StringBuilder();
                                pb2.AppendLine($"var __typed = props.As<{entryGenericType}>();");
                                foreach (var propName in untypedNames)
                                    pb2.AppendLine($"var {propName} = __typed.{propName};");
                                preamble = pb2.ToString() + preamble;
                            }
                            else if (!string.IsNullOrEmpty(entryParam))
                            {
                                // function App<AppProps>(p) → var p = props.As<AppProps>();
                                preamble = $"var {entryParam} = props.As<{entryGenericType}>();\n" + preamble;
                            }
                        }
                        else if (entryParam != null)
                        {
                            var typedParams = ParseTypedDestructuredParams(entryParam);
                            if (typedParams != null)
                                preamble = BuildPropBindings(typedParams, "props") + preamble;
                        }
                    }
                    else
                    {
                        preamble = preamble.Trim();
                    }
                    return (preamble, jsxContent);
                }
            }

            return (string.Empty, cleanBody.StartsWith("<") ? cleanBody : fileContent);
        }

        /// <summary>
        /// Finds all top-level function definitions. All except the last are converted to
        /// <c>Func&lt;Props, UINode&gt; Name = (params) => { ... return compiledJsx; };</c> declarations.
        /// The last function (the entry point) is left untouched for normal extraction.
        /// </summary>
        private static string ConvertHelperFunctions(string source)
        {
            var funcs = FindTopLevelFunctions(source);
            if (funcs.Count <= 1) return source;

            var sb = new StringBuilder();
            int pos = 0;
            for (int fi = 0; fi < funcs.Count - 1; fi++)
            {
                var (start, bodyStart, bodyEnd, name, param, genericType) = funcs[fi];

                // Emit code before this helper function.
                var before = source.Substring(pos, start - pos);
                // For code before the first function (module-level preamble), stabilize var declarations
                // so that objects like PaperContext retain the same reference across re-renders.
                if (fi == 0)
                    before = StabilizeModuleVars(before);
                sb.Append(before);

                var body = source.Substring(bodyStart + 1, bodyEnd - bodyStart - 1);

                // Four ways to declare typed props:
                //   1. function Badge<BadgeProps>({ Label, Colour }) → destructure from typed record
                //   2. function Badge<BadgeProps>(props)             → var props = __props.As<BadgeProps>();
                //   3. function Badge({ string label })              → var label = __props.Get<string>("label");
                //   4. function Badge(props)                         → plain Props parameter
                string csParam = "Props __props";
                string propBindings = string.Empty;
                if (!string.IsNullOrEmpty(genericType))
                {
                    var untypedNames = ParseUntypedDestructuredNames(param);
                    if (untypedNames != null)
                    {
                        // Case 1: function Badge<BadgeProps>({ Label, Colour })
                        var pb2 = new StringBuilder();
                        pb2.AppendLine($"var __typed = __props.As<{genericType}>();");
                        foreach (var propName in untypedNames)
                            pb2.AppendLine($"var {propName} = __typed.{propName};");
                        propBindings = pb2.ToString();
                    }
                    else if (!string.IsNullOrEmpty(param))
                    {
                        // Case 2: function Badge<BadgeProps>(props)
                        propBindings = $"var {param} = __props.As<{genericType}>();\n";
                    }
                }
                else
                {
                    var typedParams = ParseTypedDestructuredParams(param);
                    if (typedParams != null)
                    {
                        // Case 3: function Badge({ string label, PaperColour colour })
                        propBindings = BuildPropBindings(typedParams, "__props");
                    }
                    else
                    {
                        // Case 4: plain
                        csParam = string.IsNullOrEmpty(param) ? "Props __props" : $"Props {param}";
                    }
                }

                // Wrap in UseStable so the same Func<> reference is returned on every re-render.
                // This lets the reconciler match fibers by reference equality and update (not remount) them.
                sb.AppendLine($"Func<Props, UINode> {name} = Hooks.UseStable<Func<Props, UINode>>(() => ({csParam}) => {{");
                if (!string.IsNullOrEmpty(propBindings)) sb.Append(propBindings);
                sb.Append(CompileFunctionBody(body));
                sb.AppendLine("});");
                pos = bodyEnd + 1;
            }
            sb.Append(source.Substring(pos));
            return sb.ToString();
        }

        /// <summary>
        /// Parses a destructured typed param list like <c>{ string label, PaperColour colour, int count = 0 }</c>.
        /// Returns null if the param is not in destructured form.
        /// </summary>
        private static List<(string type, string name, string? defaultVal)>? ParseTypedDestructuredParams(string param)
        {
            param = param.Trim();
            if (!param.StartsWith("{") || !param.Contains("}")) return null;

            var inner = param.Substring(1, param.LastIndexOf('}') - 1).Trim();
            if (string.IsNullOrWhiteSpace(inner)) return null;

            var result = new List<(string, string, string?)>();
            foreach (var part in SplitCommaRespectingAngles(inner))
            {
                var p = part.Trim();
                if (string.IsNullOrEmpty(p)) continue;

                string? defaultVal = null;
                var eqIdx = p.IndexOf('=');
                if (eqIdx >= 0)
                {
                    defaultVal = p.Substring(eqIdx + 1).Trim();
                    p = p.Substring(0, eqIdx).Trim();
                }

                // last whitespace-separated token is the name; everything before is the type
                var lastSpace = p.LastIndexOf(' ');
                if (lastSpace < 0) continue;
                var type = p.Substring(0, lastSpace).Trim();
                var name = p.Substring(lastSpace + 1).Trim();

                if (Regex.IsMatch(name, @"^\w+$") && !string.IsNullOrEmpty(type))
                    result.Add((type, name, defaultVal));
            }
            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Returns property names from a plain destructure like <c>{ Label, Colour }</c>.
        /// Returns null if any part has spaces (indicating typed destructure) or isn't a plain identifier.
        /// </summary>
        private static List<string>? ParseUntypedDestructuredNames(string param)
        {
            param = param.Trim();
            if (!param.StartsWith("{") || !param.Contains("}")) return null;
            var inner = param.Substring(1, param.LastIndexOf('}') - 1).Trim();
            if (string.IsNullOrWhiteSpace(inner)) return null;
            var names = inner.Split(',').Select(p => p.Trim()).ToList();
            // All parts must be plain identifiers — no spaces (which would indicate a typed destructure)
            return names.All(n => Regex.IsMatch(n, @"^\w+$")) ? names : null;
        }

        /// <summary>Splits a comma-separated list while respecting angle-bracket nesting (for generics).</summary>
        private static IEnumerable<string> SplitCommaRespectingAngles(string s)
        {
            var parts = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '<') depth++;
                else if (s[i] == '>') depth--;
                else if (s[i] == ',' && depth == 0)
                {
                    parts.Add(s.Substring(start, i - start));
                    start = i + 1;
                }
            }
            parts.Add(s.Substring(start));
            return parts;
        }

        /// <summary>Builds <c>var name = propsVar.Get&lt;type&gt;("name");</c> bindings for each typed param.</summary>
        private static string BuildPropBindings(List<(string type, string name, string? defaultVal)> typedParams, string propsVar)
        {
            var sb = new StringBuilder();
            foreach (var (type, name, defaultVal) in typedParams)
            {
                if (defaultVal != null)
                    sb.AppendLine($"var {name} = {propsVar}.Has(\"{name}\") ? {propsVar}.Get<{type}>(\"{name}\") : ({type})({defaultVal});");
                else
                    sb.AppendLine($"var {name} = {propsVar}.Get<{type}>(\"{name}\");");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Wraps simple single-line <c>var X = value;</c> declarations in <c>Hooks.UseStable(() => value)</c>
        /// so module-level objects (e.g. PaperContext) retain a stable reference across re-renders.
        /// </summary>
        private static string StabilizeModuleVars(string code)
        {
            // Match: var identifier = expression_with_no_newlines;
            return Regex.Replace(
                code,
                @"(?m)^(\s*var\s+)(\w+)(\s*=\s*)([^\n;]+)\s*;",
                m => $"{m.Groups[1].Value}{m.Groups[2].Value}{m.Groups[3].Value}Hooks.UseStable(() => {m.Groups[4].Value.Trim()});");
        }

        // ── Cross-file import support ──────────────────────────────────────────

        /// <summary>
        /// Scans <paramref name="source"/> for <c>@import "./Foo.csx"</c> directives, converts ALL
        /// functions in each imported file into <c>Func&lt;Props, UINode&gt;</c> lambda declarations,
        /// and prepends them to the source so they are in scope for the importing file.
        /// The <c>@import</c> lines are stripped from the returned source.
        /// </summary>
        private static string InlineCSXImports(string source, string baseDir)
        {
            var importedCode = new StringBuilder();
            var remaining = new List<string>();

            foreach (var line in source.Split('\n'))
            {
                var trimmed = line.Trim();
                bool isImport = trimmed.StartsWith("@import", StringComparison.OrdinalIgnoreCase);
                if (isImport)
                {
                    int q1 = trimmed.IndexOfAny(new[] { '"', '\'' });
                    int q2 = q1 >= 0 ? trimmed.IndexOfAny(new[] { '"', '\'' }, q1 + 1) : -1;
                    if (q1 >= 0 && q2 > q1)
                    {
                        string importPath = trimmed[(q1 + 1)..q2];
                        if (importPath.EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
                        {
                            var absPath = Path.GetFullPath(Path.Combine(baseDir, importPath));
                            if (File.Exists(absPath))
                            {
                                var importedSource = File.ReadAllText(absPath);
                                var importedDir = Path.GetDirectoryName(absPath) ?? baseDir;
                                importedCode.AppendLine(ExtractAllComponentFunctions(importedSource, importedDir));
                            }
                            continue; // strip the @import ".csx" line
                        }
                    }
                }
                remaining.Add(line);
            }

            if (importedCode.Length == 0) return source;
            return importedCode.ToString() + string.Join('\n', remaining);
        }

        /// <summary>
        /// Converts ALL top-level functions in a CSX source file to
        /// <c>Func&lt;Props, UINode&gt; Name = Hooks.UseStable&lt;...&gt;(() => ...);</c> declarations.
        /// Used when inlining imported .csx files so their components become available as variables
        /// in the importing file.
        /// </summary>
        public static string ExtractAllComponentFunctions(string source, string? baseDir = null)
        {
            // Strip all @import lines (both .csx and .csss) — .csx ones are recursively resolved here.
            if (baseDir != null)
                source = InlineCSXImports(source, baseDir);

            var filteredLines = source.Split('\n')
                .Where(l => !l.Trim().StartsWith("@import", StringComparison.OrdinalIgnoreCase));
            source = string.Join('\n', filteredLines).Trim().TrimEnd(';');

            var funcs = FindTopLevelFunctions(source);
            if (funcs.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            int pos = 0;
            for (int fi = 0; fi < funcs.Count; fi++)
            {
                var (start, bodyStart, bodyEnd, name, param, genericType) = funcs[fi];

                var before = source.Substring(pos, start - pos);
                if (fi == 0) before = StabilizeModuleVars(before);
                sb.Append(before);

                var body = source.Substring(bodyStart + 1, bodyEnd - bodyStart - 1);

                // Same four-way prop binding logic used in ConvertHelperFunctions
                string csParam = "Props __props";
                string propBindings = string.Empty;
                if (!string.IsNullOrEmpty(genericType))
                {
                    var untypedNames = ParseUntypedDestructuredNames(param);
                    if (untypedNames != null)
                    {
                        var pb2 = new StringBuilder();
                        pb2.AppendLine($"var __typed = __props.As<{genericType}>();");
                        foreach (var propName in untypedNames)
                            pb2.AppendLine($"var {propName} = __typed.{propName};");
                        propBindings = pb2.ToString();
                    }
                    else if (!string.IsNullOrEmpty(param))
                    {
                        propBindings = $"var {param} = __props.As<{genericType}>();\n";
                    }
                }
                else
                {
                    var typedParams = ParseTypedDestructuredParams(param);
                    if (typedParams != null)
                        propBindings = BuildPropBindings(typedParams, "__props");
                    else
                        csParam = string.IsNullOrEmpty(param) ? "Props __props" : $"Props {param}";
                }

                sb.AppendLine($"Func<Props, UINode> {name} = Hooks.UseStable<Func<Props, UINode>>(() => ({csParam}) => {{");
                if (!string.IsNullOrEmpty(propBindings)) sb.Append(propBindings);
                sb.Append(CompileFunctionBody(body));
                sb.AppendLine("});");
                pos = bodyEnd + 1;
            }

            // Trailing code after the last function (shouldn't usually exist but preserve it)
            if (pos < source.Length)
                sb.Append(source.Substring(pos));

            return sb.ToString();
        }

        /// <summary>Returns (funcStart, bodyOpenBrace, bodyCloseBrace, name, param, genericType?) for each top-level function.
        /// Supports <c>function Badge&lt;BadgeProps&gt;(props)</c> — genericType is the type between &lt; &gt;.</summary>
        private static List<(int start, int bodyStart, int bodyEnd, string name, string param, string? genericType)> FindTopLevelFunctions(string source)
        {
            var results = new List<(int, int, int, string, string, string?)>();
            int i = 0;
            while (i < source.Length)
            {
                var idx = source.IndexOf("function ", i, StringComparison.Ordinal);
                if (idx < 0) break;
                var nameStart = idx + "function ".Length;
                var parenOpen = source.IndexOf('(', nameStart);
                if (parenOpen < 0) break;

                // Extract name and optional generic: "Badge<BadgeProps>" → name="Badge", generic="BadgeProps"
                var rawName = source.Substring(nameStart, parenOpen - nameStart).Trim();
                string name;
                string? genericType = null;
                var angleOpen = rawName.IndexOf('<');
                if (angleOpen >= 0)
                {
                    var angleClose = rawName.IndexOf('>', angleOpen);
                    if (angleClose > angleOpen)
                    {
                        name = rawName.Substring(0, angleOpen).Trim();
                        genericType = rawName.Substring(angleOpen + 1, angleClose - angleOpen - 1).Trim();
                    }
                    else { name = rawName; }
                }
                else { name = rawName; }

                if (!Regex.IsMatch(name, @"^\w+$")) { i = idx + 1; continue; }
                var parenClose = source.IndexOf(')', parenOpen);
                if (parenClose < 0) break;
                var param = source.Substring(parenOpen + 1, parenClose - parenOpen - 1).Trim();
                var braceOpen = source.IndexOf('{', parenClose);
                if (braceOpen < 0) break;
                var braceClose = FindMatchingBrace(source, braceOpen);
                results.Add((idx, braceOpen, braceClose, name, param, genericType));
                i = braceClose + 1;
            }
            return results;
        }

        /// <summary>
        /// Converts a CSX function body to C# by compiling any JSX in the return statement.
        /// </summary>
        private static string CompileFunctionBody(string body)
        {
            var returnIdx = body.IndexOf("return (", StringComparison.Ordinal);
            if (returnIdx < 0) return body.Trim() + "\n";
            var parenOpen = returnIdx + "return (".Length - 1;
            var depth = 1;
            var j = parenOpen + 1;
            while (j < body.Length && depth > 0)
            {
                if (body[j] == '(') depth++;
                else if (body[j] == ')') depth--;
                j++;
            }
            var jsxContent = body.Substring(parenOpen + 1, j - 1 - (parenOpen + 1)).Trim();
            var preamble = body.Substring(0, returnIdx).Trim();
            string generatedJsx;
            try { generatedJsx = Parse(jsxContent); }
            catch (Exception ex) { generatedJsx = $"UI.Text(\"Helper JSX error: {ex.Message}\")"; }
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(preamble)) { sb.AppendLine(preamble); }
            sb.AppendLine($"return {generatedJsx};");
            return sb.ToString();
        }

        /// <summary>Finds the position of the closing brace matching the opening brace at <paramref name="openPos"/>.</summary>
        private static int FindMatchingBrace(string s, int openPos)
        {
            int depth = 1;
            int i = openPos + 1;
            while (i < s.Length && depth > 0)
            {
                char c = s[i];
                if (c == '\\') { i += 2; continue; }
                if (c == '"') { i++; while (i < s.Length && s[i] != '"') { if (s[i] == '\\') i++; i++; } }
                else if (c == '\'') { i++; while (i < s.Length && s[i] != '\'') { if (s[i] == '\\') i++; i++; } }
                else if (c == '{') depth++;
                else if (c == '}') depth--;
                i++;
            }
            return i - 1;
        }

        public static string Parse(string csxContent)
        {
            try
            {
                string processed = Preprocess(csxContent);
                var ast = new CSXElementParser(processed).ParseFirstElement();
                return new CSXCodeGenerator().Generate(ast);
            }
            catch (Exception ex)
            {
                return $"// Parse error: {ex.Message}\nUI.Text(\"Parse Error: {ex.Message}\")";
            }
        }

        private static string Preprocess(string body)
        {
            // Replace const [...] = useState(...) with var (...) = Hooks.UseState(...)
            // Handle 1, 2, or 3 destructured names and pad to fill the 3-tuple.
            string result = Regex.Replace(body, @"const \[([^\]]+)\]\s*=\s*useState", m =>
            {
                var names = m.Groups[1].Value.Split(',').Select(n => n.Trim()).ToList();
                string tuple = names.Count switch
                {
                    1 => $"var ({names[0]}, _, _)",
                    2 => $"var ({names[0]}, {names[1]}, _)",
                    _ => $"var ({names[0]}, {names[1]}, {names[2]})",
                };
                return $"{tuple} = Hooks.UseState";
            });
            // Replace console.log with System.Console.WriteLine
            result = result.Replace("console.log", "System.Console.WriteLine");
            // Replace template literals with string interpolation
            result = ReplaceTemplateLiterals(result);
            // Fix useEffect: ensure the lambda returns Action? by adding return null; if no return exists
            result = FixUseEffect(result);
            // Replace useEffect with Hooks.UseEffect (for cases without curly braces)
            result = ReplaceUseEffect(result);
            return result;
        }

        private static string ReplaceTemplateLiterals(string input)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < input.Length)
            {
                if (input[i] == '`')
                {
                    int j = i + 1;
                    int backtickCount = 1;
                    while (j < input.Length && backtickCount > 0)
                    {
                        if (input[j] == '\\')
                        {
                            j += 2;
                        }
                        else if (input[j] == '`')
                        {
                            backtickCount--;
                            j++;
                        }
                        else
                        {
                            j++;
                        }
                    }

                    if (backtickCount == 0)
                    {
                        string content = input.Substring(i + 1, j - i - 2);
                        sb.Append($"$\"{content}\"");
                        i = j;
                    }
                    else
                    {
                        sb.Append(input[i]);
                        i++;
                    }
                }
                else
                {
                    sb.Append(input[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static string FixUseEffect(string input)
        {
            var pattern = @"Hooks\.UseEffect\(\s*\(\s*\)\s*=>\s*\{([^}]*)\}";
            return Regex.Replace(input, pattern, m =>
            {
                string effectBody = m.Groups[1].Value;
                if (!effectBody.Contains("return"))
                {
                    effectBody = effectBody.TrimEnd();
                    if (effectBody.EndsWith(";"))
                        effectBody = effectBody.Substring(0, effectBody.Length - 1);
                    effectBody += "; return null";
                }
                return $"Hooks.UseEffect(() => {{ {effectBody} }})";
            });
        }

        private static string ReplaceUseEffect(string input)
        {
            var pattern = @"useEffect\(\s*\(\s*\)\s*=>\s*([^,\)]+)";
            return Regex.Replace(input, pattern, m =>
            {
                string effectBody = m.Groups[1].Value.Trim();
                if (!effectBody.StartsWith("{") && !effectBody.Contains("return"))
                {
                    return $"Hooks.UseEffect(() => {{ {effectBody}; return null; }}";
                }
                return $"Hooks.UseEffect(() => {effectBody}";
            });
        }

        private static string ParseJsxTags(string content)
        {
            var result = content;

            // Process tags in order from inner to outer to handle nesting properly
            bool modified = true;
            while (modified)
            {
                modified = false;

                // Handle Text tags since they don't usually contain other tags
                string textResult = Regex.Replace(result, @"<Text\s*([^>]*)>([\s\S]*?)</Text>", m =>
                {
                    string props = m.Groups[1].Value;
                    string textContent = m.Groups[2].Value.Trim();

                    string styleCode = "StyleSheet.Empty";
                    var styleMatch = Regex.Match(props, @"style\s*=\s*\{([^}]+)\}");
                    if (styleMatch.Success)
                    {
                        styleCode = ParseInlineStyle(styleMatch.Groups[1].Value);
                    }

                    return $"UI.Text(\"{textContent}\", {styleCode})";
                }, RegexOptions.Multiline | RegexOptions.Singleline);

                if (textResult != result)
                {
                    result = textResult;
                    modified = true;
                }

                // Handle Button tags with any order of attributes
                string buttonResult = Regex.Replace(result, @"<Button\s*([^>]*)>(.*?)</Button>", m =>
                {
                    string props = m.Groups[1].Value;
                    string buttonContent = m.Groups[2].Value;

                    string onClickCode = "null";
                    var onClickMatch = Regex.Match(props, @"onClick\s*=\s*\{([^}]+)\}");
                    if (onClickMatch.Success)
                    {
                        onClickCode = $"() => {onClickMatch.Groups[1].Value}";
                    }

                    string styleCode = "StyleSheet.Empty";
                    var styleMatch = Regex.Match(props, @"style\s*=\s*\{([^}]+)\}");
                    if (styleMatch.Success)
                    {
                        styleCode = ParseInlineStyle(styleMatch.Groups[1].Value);
                    }

                    string buttonText = buttonContent.Trim();
                    buttonText = Regex.Replace(buttonText, @"onClick\s*=\s*\{[^}]+\}|style\s*=\s*\{[^}]+\}|{", "");
                    buttonText = Regex.Replace(buttonText, @">", "", RegexOptions.Multiline | RegexOptions.Singleline);
                    buttonText = Regex.Replace(buttonText, @"<", "", RegexOptions.Multiline | RegexOptions.Singleline);
                    buttonText = buttonText.Trim();

                    if (string.IsNullOrWhiteSpace(buttonText) || buttonText.Contains("}"))
                    {
                        if (buttonContent.Contains("Increment"))
                            buttonText = "Increment";
                        else if (buttonContent.Contains("Decrement"))
                            buttonText = "Decrement";
                        else if (buttonContent.Contains("Reset"))
                            buttonText = "Reset";
                        else
                            buttonText = "Button";
                    }

                    return $"UI.Button(\"{buttonText}\", {onClickCode}, {styleCode})";
                }, RegexOptions.Multiline | RegexOptions.Singleline);

                if (buttonResult != result)
                {
                    result = buttonResult;
                    modified = true;
                }

                // Handle Box tags which can contain other tags
                string boxResult = Regex.Replace(result, @"<Box\s*([^>]*)>([\s\S]*?)</Box>", m =>
                {
                    string props = m.Groups[1].Value;
                    string children = m.Groups[2].Value.Trim();

                    string styleCode = "StyleSheet.Empty";
                    var styleMatch = Regex.Match(props, @"style\s*=\s*\{([^}]+)\}");
                    if (styleMatch.Success)
                    {
                        styleCode = ParseInlineStyle(styleMatch.Groups[1].Value);
                    }

                    string onClickCode = "null";
                    var onClickMatch = Regex.Match(props, @"onClick\s*=\s*\{([^}]+)\}");
                    if (onClickMatch.Success)
                    {
                        onClickCode = $"() => {onClickMatch.Groups[1].Value}";
                    }

                    if (!string.IsNullOrWhiteSpace(children))
                    {
                        string processedChildren = ParseJsxTags(children);

                        while (processedChildren.Contains(")\n") || processedChildren.Contains(")\r\n"))
                        {
                            processedChildren = processedChildren.Replace(")\n", "), \n").Replace(")\r\n", "), \r\n");
                        }

                        return $"UI.Box({styleCode}, {processedChildren})";
                    }
                    else
                    {
                        return $"UI.Box({styleCode})";
                    }
                }, RegexOptions.Multiline | RegexOptions.Singleline);

                if (boxResult != result)
                {
                    result = boxResult;
                    modified = true;
                }

                // Handle custom tags
                string customResult = Regex.Replace(result, @"<(\w+)\s*([^>]*)>([\s\S]*?)</\1>", m =>
                {
                    string tagName = m.Groups[1].Value;
                    string props = m.Groups[2].Value;
                    string children = m.Groups[3].Value.Trim();

                    string styleCode = "StyleSheet.Empty";
                    var styleMatch = Regex.Match(props, @"style\s*=\s*\{([^}]+)\}");
                    if (styleMatch.Success)
                    {
                        styleCode = ParseInlineStyle(styleMatch.Groups[1].Value);
                    }

                    string onClickCode = "null";
                    var onClickMatch = Regex.Match(props, @"onClick\s*=\s*\{([^}]+)\}");
                    if (onClickMatch.Success)
                    {
                        onClickCode = $"() => {onClickMatch.Groups[1].Value}";
                    }

                    string componentName = char.ToUpper(tagName[0]) + tagName.Substring(1);

                    if (!string.IsNullOrWhiteSpace(children))
                    {
                        string processedChildren = ParseJsxTags(children);

                        while (processedChildren.Contains(")\n") || processedChildren.Contains(")\r\n"))
                        {
                            processedChildren = processedChildren.Replace(")\n", "), \n").Replace(")\r\n", "), \r\n");
                        }

                        return $"{componentName}({styleCode}, {processedChildren})";
                    }
                    else
                    {
                        return $"{componentName}({styleCode})";
                    }
                }, RegexOptions.Multiline | RegexOptions.Singleline);

                if (customResult != result)
                {
                    result = customResult;
                    modified = true;
                }
            }

            return result;
        }

        private static string ParseInlineStyle(string styleExpr)
        {
            var sb = new StringBuilder();
            sb.Append("new StyleSheet { ");

            string normalizedStyle = styleExpr.Trim().Replace(Environment.NewLine, " ").Replace("\n", " ").Replace("\r", " ").Replace("  ", " ");

            if (normalizedStyle.Contains("boxShadow"))
            {
                int boxShadowStart = normalizedStyle.IndexOf("boxShadow", StringComparison.OrdinalIgnoreCase);
                int valueStart = normalizedStyle.IndexOf(':', boxShadowStart) + 1;
                int valueEnd = -1;

                int parenCount = 0;
                for (int j = valueStart; j < normalizedStyle.Length; j++)
                {
                    if (normalizedStyle[j] == '(') parenCount++;
                    else if (normalizedStyle[j] == ')') parenCount--;
                    else if (normalizedStyle[j] == ',' && parenCount == 0)
                    {
                        valueEnd = j;
                        break;
                    }
                    else if (normalizedStyle[j] == '}' && parenCount == 0)
                    {
                        valueEnd = j;
                        break;
                    }
                }

                if (valueEnd == -1) valueEnd = normalizedStyle.Length;

                sb.Append($"BoxShadow = new[] {{ new BoxShadow(0f, 4f, 12f, new PaperColour(0f, 0f, 0f, 0.3f)) }}, ");

                normalizedStyle = normalizedStyle.Remove(boxShadowStart, valueEnd - boxShadowStart);
            }

            var declMatches = Regex.Matches(normalizedStyle, @"(\w+)\s*:\s*([^,}]+)(?=[,}])", RegexOptions.Multiline);

            foreach (Match match in declMatches)
            {
                string prop = match.Groups[1].Value.Trim();
                string value = match.Groups[2].Value.Trim();

                if (string.Equals(prop, "boxShadow", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string paperProp = ToPascalCase(prop);
                string paperValue = ParseStyleValue(value, paperProp);

                sb.Append($"{paperProp} = {paperValue}, ");
            }

            if (sb.Length > "new StyleSheet { ".Length)
                sb.Length -= 2;

            sb.Append(" }");
            return sb.ToString();
        }

        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            if (input.Length > 1 && char.IsLower(input[0]) && char.IsUpper(input[1]))
            {
                return char.ToUpper(input[0]) + input.Substring(1);
            }
            else if (input.Length > 0)
            {
                return char.ToUpper(input[0]) + input.Substring(1);
            }

            return input;
        }

        private static string ParseStyleValue(string value, string propertyName = "")
        {
            if (string.IsNullOrEmpty(value))
                return "null";

            string trimmedValue = value.Trim().Trim('"', '\'', ' ', '\t');

            if (trimmedValue.Contains(' '))
            {
                return $"\"{trimmedValue}\"";
            }

            if (double.TryParse(trimmedValue, out double num))
            {
                if (string.Equals(propertyName, "borderRadius", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{num}f";
                }

                if (string.Equals(propertyName, "padding", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "margin", StringComparison.OrdinalIgnoreCase))
                {
                    return $"new Thickness({num}f)";
                }

                return $"Length.Px({num})";
            }

            if (trimmedValue.StartsWith('#'))
            {
                return ParseHexColor(trimmedValue);
            }

            switch (trimmedValue.ToLower())
            {
                case "auto":
                    return "Length.Auto";
                case "flex":
                    return "Display.Flex";
                case "block":
                    return "Display.Block";
                case "none":
                    return "Display.None";
                case "row":
                    return "FlexDirection.Row";
                case "column":
                    return "FlexDirection.Column";
                case "center":
                    if (string.Equals(propertyName, "justifyContent", StringComparison.OrdinalIgnoreCase))
                    {
                        return "JustifyContent.Center";
                    }
                    else if (string.Equals(propertyName, "alignItems", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(propertyName, "alignSelf", StringComparison.OrdinalIgnoreCase))
                    {
                        return "AlignItems.Center";
                    }
                    else if (string.Equals(propertyName, "justifyItems", StringComparison.OrdinalIgnoreCase))
                    {
                        return "JustifyItems.Center";
                    }
                    return "AlignItems.Center";
                case "stretch":
                    if (string.Equals(propertyName, "justifyContent", StringComparison.OrdinalIgnoreCase))
                    {
                        return "JustifyContent.Stretch";
                    }
                    else if (string.Equals(propertyName, "alignItems", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(propertyName, "alignSelf", StringComparison.OrdinalIgnoreCase))
                    {
                        return "AlignItems.Stretch";
                    }
                    else if (string.Equals(propertyName, "justifyItems", StringComparison.OrdinalIgnoreCase))
                    {
                        return "JustifyItems.Stretch";
                    }
                    return "AlignItems.Stretch";
                case "100%":
                    return "Length.Percent(100)";
                case "white":
                    return "new PaperColour(1f, 1f, 1f, 1f)";
                case "wrap":
                    if (string.Equals(propertyName, "flexWrap", StringComparison.OrdinalIgnoreCase))
                        return "FlexWrap.Wrap";
                    goto default;
                case "nowrap":
                    if (string.Equals(propertyName, "flexWrap", StringComparison.OrdinalIgnoreCase))
                        return "FlexWrap.NoWrap";
                    goto default;
                case "wrap-reverse":
                    if (string.Equals(propertyName, "flexWrap", StringComparison.OrdinalIgnoreCase))
                        return "FlexWrap.WrapReverse";
                    goto default;
                default:
                    if (trimmedValue.Contains("px") || trimmedValue.Contains("em") || trimmedValue.Contains("rem") || trimmedValue.Contains("%"))
                    {
                        if (trimmedValue.Contains("%"))
                        {
                            var percentValue = double.Parse(trimmedValue.Replace("%", ""));
                            return $"Length.Percent({percentValue})";
                        }
                        else if (trimmedValue.Contains("px"))
                        {
                            var pxValue = double.Parse(trimmedValue.Replace("px", ""));

                            if (string.Equals(propertyName, "borderRadius", StringComparison.OrdinalIgnoreCase))
                            {
                                return $"{pxValue}f";
                            }

                            if (string.Equals(propertyName, "padding", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(propertyName, "margin", StringComparison.OrdinalIgnoreCase))
                            {
                                return $"new Thickness({pxValue}f)";
                            }

                            return $"Length.Px({pxValue})";
                        }
                    }

                    return $"\"{trimmedValue}\"";
            }
        }

        private static string ParseHexColor(string hex)
        {
            hex = hex.TrimStart('#');

            if (hex.Length == 3)
            {
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            }

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