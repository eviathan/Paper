using Paper.Core.Styles;

namespace Paper.CSSS
{
    /// <summary>
    /// Compiles CSSS into component-scoped class styles (CSS Modules-like).
    /// Supported selectors: simple class selectors (e.g. .button) with optional
    /// :hover/:active/:focus pseudo-classes.
    /// </summary>
    public static class ScopedCSSSCompiler
    {
        public static Dictionary<string, StyleSheet> CompileScoped(string csss, string scopeId)
        {
            var raw = CSSSCompiler.Compile(csss);
            var scoped = new Dictionary<string, StyleSheet>(StringComparer.Ordinal);

            foreach (var (selector, style) in raw)
            {
                var s = selector.Trim();
                if (!s.StartsWith('.')) continue;

                // No combinators for now (component-scoped class styling).
                if (s.Contains(' ') || s.Contains('>') || s.Contains('+') || s.Contains('~'))
                    continue;

                string pseudo = "";
                int pseudoIdx = s.IndexOf(':');
                if (pseudoIdx >= 0)
                {
                    pseudo = s[pseudoIdx..];
                    s = s[..pseudoIdx];
                }

                var className = s.TrimStart('.');
                if (className.Length == 0) continue;

                // Keep the pseudo suffix in the key so runtime can match via InteractionState.
                string key = $"{className}__{scopeId}{pseudo}";
                scoped[key] = style;
            }

            return scoped;
        }
    }
}

