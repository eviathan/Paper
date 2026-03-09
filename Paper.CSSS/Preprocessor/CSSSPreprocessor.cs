using Paper.CSSS.Parser;

namespace Paper.CSSS.Preprocessor
{
    /// <summary>
    /// Expands CSSS-specific features into flat CSS rules:
    /// <list type="bullet">
    ///   <item>Variable substitution (<c>$name</c>)</item>
    ///   <item>Nested rule flattening (including parent selector <c>&amp;</c>)</item>
    ///   <item>Mixin definition and <c>@include</c> expansion</item>
    /// </list>
    /// Output is a list of <see cref="CSSSRule"/> — plain selector + declaration pairs.
    /// </summary>
    internal sealed class CSSSPreprocessor
    {
        private readonly Dictionary<string, string>            _variables = new();
        private readonly Dictionary<string, CSSSMixin>         _mixins    = new();

        public List<CSSSRule> Process(CSSSStylesheet sheet)
        {
            // First pass: collect top-level variables and mixins
            PreCollect(sheet.Statements, _variables, _mixins);

            var output = new List<CSSSRule>();
            ProcessBlock(sheet.Statements, new List<string>(), output);
            return output;
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
                        sb.Append(vars.TryGetValue(varName, out var iv) ? iv : "");
                        i = end + 1;
                        continue;
                    }
                }
                // $var references
                if (value[i] == '$')
                {
                    int start = i + 1;
                    int end   = start;
                    while (end < value.Length && (char.IsLetterOrDigit(value[end]) || value[end] == '-' || value[end] == '_'))
                        end++;
                    string varName = value[start..end];
                    sb.Append(vars.TryGetValue(varName, out var v) ? v : $"${varName}");
                    i = end;
                    continue;
                }
                sb.Append(value[i++]);
            }
            return sb.ToString();
        }

        // ── Mixin argument binding ────────────────────────────────────────────

        private static Dictionary<string, string> BuildMixinVars(
            CSSSMixin mixin, List<string> args, Dictionary<string, string> scope)
        {
            var local = new Dictionary<string, string>();
            for (int i = 0; i < mixin.Parameters.Count; i++)
            {
                string paramName = mixin.Parameters[i];
                string argValue  = i < args.Count ? args[i] : "";
                local[paramName] = SubstituteVars(argValue, scope);
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
