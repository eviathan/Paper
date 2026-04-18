using Paper.CSSS.Parser;
using Paper.CSSS.Lexer;

namespace Paper.CSSS.Preprocessor
{
    /// <summary>
    /// Expands CSSS-specific features into flat CSS rules:
    /// <list type="bullet">
    ///   <item>Variable substitution (<c>$name</c>)</item>
    ///   <item>Nested rule flattening (including parent selector <c>&amp;</c>)</item>
    ///   <item>Mixin definition and <c>@include</c> expansion</item>
    ///   <item><c>@import</c> - inline imported styles</item>
    ///   <item><c>@extend</c> - selector inheritance</item>
    /// </list>
    /// Output is a list of <see cref="CSSSRule"/> — plain selector + declaration pairs.
    /// </summary>
    internal sealed class CSSSPreprocessor
    {
        private readonly Dictionary<string, string>            _variables = new();
        private readonly Dictionary<string, CSSSMixin>         _mixins    = new();
        private readonly Dictionary<string, List<string>>      _extendMap = new();
        private Func<string, string?>? _importResolver;

        /// <summary>Set a function to resolve @import paths to CSSS source code.</summary>
        public void SetImportResolver(Func<string, string?> resolver) => _importResolver = resolver;

        public List<CSSSRule> Process(CSSSStylesheet sheet)
        {
            // First pass: collect top-level variables and mixins
            PreCollect(sheet.Statements, _variables, _mixins);

            var output = new List<CSSSRule>();
            ProcessBlock(sheet.Statements, new List<string>(), output);
            ApplyExtends(output);
            return output;
        }

        private void ApplyExtends(List<CSSSRule> output)
        {
            foreach (var rule in output)
            {
                foreach (var selector in rule.Selectors.ToList())
                {
                    if (_extendMap.TryGetValue(selector, out var parents))
                    {
                        foreach (var parent in parents)
                        {
                            if (!rule.Selectors.Contains(parent))
                                rule.Selectors.Add(parent);
                        }
                    }
                }
            }
        }

        // ── Pre-collection ────────────────────────────────────────────────────

        private static void PreCollect(
            List<CSSSStatement> stmts,
            Dictionary<string, string>    vars,
            Dictionary<string, CSSSMixin> mixins)
        {
            foreach (var stmt in stmts)
            {
                if (stmt is CSSSVariableDecl v) vars[v.Name]   = v.Value;
                if (stmt is CSSSMixin m)         mixins[m.Name] = m;
            }
        }

        // ── Block processing ──────────────────────────────────────────────────

        private void ProcessBlock(
            List<CSSSStatement> stmts,
            List<string>        parentSelectors,
            List<CSSSRule>       output,
            Dictionary<string, string>? localVars = null)
        {
            // Merge local vars onto a copy of the current variable scope
            var vars = localVars != null
                ? new Dictionary<string, string>(_variables).Merge(localVars)
                : _variables;

            var decls = new List<(string prop, string value)>();

            foreach (var stmt in stmts)
            {
                switch (stmt)
                {
                    case CSSSVariableDecl vd:
                        vars[vd.Name] = SubstituteVars(vd.Value, vars);
                        break;

                    case CSSSDeclaration d:
                        decls.Add((d.Property, SubstituteVars(d.Value, vars)));
                        break;

                    case Parser.CSSSRule rule:
                        // Emit accumulated declarations for parent selector before processing nested rule
                        if (decls.Count > 0 && parentSelectors.Count > 0)
                        {
                            output.Add(new CSSSRule(parentSelectors.ToList(), decls.ToList()));
                            decls.Clear();
                        }

                        // Compute new selectors
                        var newSelectors = ExpandSelectors(rule.Selectors, parentSelectors);
                        ProcessBlock(rule.Body, newSelectors, output, vars != _variables ? vars : null);
                        break;

                    case CSSSInclude include:
                        if (_mixins.TryGetValue(include.Name, out var mixin))
                        {
                            var mixinVars = BuildMixinVars(mixin, include.Arguments, vars);
                            ProcessBlock(mixin.Body, parentSelectors, output, mixinVars);
                        }
                        break;

                    case CSSSMixin mixinDef:
                        _mixins[mixinDef.Name] = mixinDef;
                        break;

                    case CSSSAtRule at:
                        // Pass @media and similar through as a special rule
                        if (at.Block != null)
                        {
                            var subRules = new List<CSSSRule>();
                            ProcessBlock(at.Block, parentSelectors, subRules, vars != _variables ? vars : null);
                            foreach (var sub in subRules)
                                output.Add(sub with { AtRule = $"@{at.Name} {at.Prelude}" });
                        }
                        break;

                    case CSSSImport importStmt:
                        // Resolve and inline imported styles
                        if (_importResolver != null)
                        {
                            var importPath = SubstituteVars(importStmt.Path.Trim('"', '\''), vars);
                            var importedSource = _importResolver(importPath);
                            if (!string.IsNullOrEmpty(importedSource))
                            {
                                var importTokens = new CSSSLexer(importedSource).Tokenise();
                                var importAst = new CSSSParser(importTokens).Parse();
                                var importPreprocessor = new CSSSPreprocessor
                                {
                                    _importResolver = _importResolver
                                };
                                var importedRules = importPreprocessor.Process(importAst);
                                output.AddRange(importedRules);
                            }
                        }
                        break;

                    case CSSSExtend extendStmt:
                        // Record @extend for later processing
                        foreach (var targetSelector in extendStmt.Selectors)
                        {
                            foreach (var parent in parentSelectors)
                            {
                                if (!_extendMap.ContainsKey(targetSelector))
                                    _extendMap[targetSelector] = new List<string>();
                                _extendMap[targetSelector].Add(parent);
                            }
                        }
                        break;
                }
            }

            // Emit any remaining declarations
            if (decls.Count > 0 && parentSelectors.Count > 0)
                output.Add(new CSSSRule(parentSelectors.ToList(), decls.ToList()));
        }

        // ── Selector expansion ────────────────────────────────────────────────

        private static List<string> ExpandSelectors(
            List<string> childSelectors,
            List<string> parentSelectors)
        {
            if (parentSelectors.Count == 0)
                return childSelectors.ToList();

            var result = new List<string>();
            foreach (var child in childSelectors)
            {
                if (child.Contains('&'))
                {
                    // Parent selector substitution: & → parent
                    foreach (var parent in parentSelectors)
                        result.Add(child.Replace("&", parent));
                }
                else
                {
                    // Descendant combinator: parent child
                    foreach (var parent in parentSelectors)
                        result.Add($"{parent} {child}");
                }
            }
            return result;
        }

        // ── Variable substitution ─────────────────────────────────────────────

        private static string SubstituteVars(string value, Dictionary<string, string> vars)
        {
            // Replace #{$var} interpolation first
            int i = 0;
            var sb = new System.Text.StringBuilder();
            while (i < value.Length)
            {
                if (i + 1 < value.Length && value[i] == '#' && value[i + 1] == '{')
                {
                    int end = value.IndexOf('}', i + 2);
                    if (end >= 0)
                    {
                        string varName = value[(i + 2)..end].Trim().TrimStart('$');
                        string resolved = EvaluateExpression(varName, vars);
                        sb.Append(resolved);
                        i = end + 1;
                        continue;
                    }
                }
                // $var references
                if (value[i] == '$')
                {
                    if (i > 0 && (char.IsLetterOrDigit(value[i-1]) || value[i-1] == '_'))
                    {
                        sb.Append(value[i++]);
                        continue;
                    }
                    int start = i + 1;
                    int end   = start;
                    while (end < value.Length && (char.IsLetterOrDigit(value[end]) || value[end] == '-' || value[end] == '_'))
                        end++;
                    string varName = value[start..end];
                    
                    // Check if this is part of an expression (e.g., $a + $b)
                    int exprEnd = end;
                    while (exprEnd < value.Length && " \t+-*/".Contains(value[exprEnd])) exprEnd++;
                    bool isExpression = exprEnd < value.Length && "+-*/".Contains(value[exprEnd]);
                    
                    if (isExpression)
                    {
                        // Extract the full expression
                        string expr = value[start..];
                        string result = EvaluateExpression(expr, vars);
                        sb.Append(result);
                        i = value.Length;
                        continue;
                    }
                    
                    sb.Append(vars.TryGetValue(varName, out var v) ? v : $"${varName}");
                    i = end;
                    continue;
                }
                sb.Append(value[i++]);
            }
            return sb.ToString();
        }

        /// <summary>Evaluates simple arithmetic expressions with variables.</summary>
        private static string EvaluateExpression(string expr, Dictionary<string, string> vars)
        {
            // First substitute all variables in the expression
            string substituted = expr;
            foreach (var kv in vars)
            {
                substituted = substituted.Replace($"${kv.Key}", kv.Value);
            }
            
            // Try to evaluate as arithmetic
            try
            {
                // Simple evaluation: handle + - * / with numbers
                var tokens = substituted.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 3)
                {
                    double result = 0;
                    double? left = ParseNumber(tokens[0]);
                    if (left == null) return expr.Trim();
                    
                    for (int i = 1; i + 1 < tokens.Length; i += 2)
                    {
                        string op = tokens[i];
                        double? right = ParseNumber(tokens[i + 1]);
                        if (right == null) return expr.Trim();
                        
                        result = op switch
                        {
                            "+" => left.Value + right.Value,
                            "-" => left.Value - right.Value,
                            "*" => left.Value * right.Value,
                            "/" => right.Value != 0 ? left.Value / right.Value : 0,
                            _ => left.Value
                        };
                        left = result;
                    }
                    
                    // Return integer if result is whole, otherwise decimal
                    return result == Math.Floor(result) ? ((int)result).ToString() : result.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch { }
            
            return substituted.Trim();
        }

        private static double? ParseNumber(string s)
        {
            s = s.Trim();
            if (s.EndsWith("px")) s = s[..^2];
            if (s.EndsWith("em")) s = s[..^2];
            if (s.EndsWith("rem")) s = s[..^3];
            if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v))
                return v;
            return null;
        }

        // ── Mixin argument binding ────────────────────────────────────────────

        private static Dictionary<string, string> BuildMixinVars(
            CSSSMixin mixin, List<string> args, Dictionary<string, string> scope)
        {
            var local = new Dictionary<string, string>();
            for (int i = 0; i < mixin.Parameters.Count; i++)
            {
                var param = mixin.Parameters[i];
                string argValue;
                if (i < args.Count && !string.IsNullOrWhiteSpace(args[i]))
                {
                    argValue = args[i];
                }
                else if (!string.IsNullOrEmpty(param.DefaultValue))
                {
                    // Use default value
                    argValue = param.DefaultValue;
                }
                else
                {
                    argValue = "";
                }
                local[param.Name] = SubstituteVars(argValue, scope);
            }
            return local;
        }
    }

    // ── Extension helpers ─────────────────────────────────────────────────────

    internal static class DictionaryExtensions
    {
        public static Dictionary<TK, TV> Merge<TK, TV>(
            this Dictionary<TK, TV> self, Dictionary<TK, TV> other)
            where TK : notnull
        {
            var result = new Dictionary<TK, TV>(self);
            foreach (var kv in other) result[kv.Key] = kv.Value;
            return result;
        }
    }
}
