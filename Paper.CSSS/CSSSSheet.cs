using Paper.Core.Reconciler;
using Paper.Core.Styles;

namespace Paper.CSSS
{
    internal sealed record CSSSRule(CSSSSelector Selector, StyleSheet Style);

    /// <summary>
    /// A parsed CSSS style sheet — a list of typed selectors with associated StyleSheets.
    /// Supports runtime matching against fibers (including ancestor-chain selectors).
    /// </summary>
    public sealed class CSSSSheet : ICSSSSheet, StyleResolver.ICSSSSheetMatcher
    {
        private readonly List<CSSSRule> _rules;

        public string SourcePath { get; }

        internal CSSSSheet(string sourcePath, List<CSSSRule> rules)
        {
            SourcePath = sourcePath;
            _rules = rules;
        }

        /// <summary>Return the merged StyleSheet from all rules matching this fiber + interaction state.</summary>
        public StyleSheet Match(Fiber fiber, InteractionState state)
        {
            var result = StyleSheet.Empty;
            foreach (var rule in _rules)
                if (rule.Selector.Matches(fiber, state))
                    result = result.Merge(rule.Style);
                    
            return result;
        }

        /// <summary>Build a CSSSSheet from a flat selector→StyleSheet map (output of CSSSCompiler).</summary>
        internal static CSSSSheet FromDictionary(string sourcePath, Dictionary<string, StyleSheet> map)
        {
            var rules = new List<CSSSRule>(map.Count);
            foreach (var (sel, style) in map)
                rules.Add(new CSSSRule(CSSSSelector.Parse(sel), style));
            return new CSSSSheet(sourcePath, rules);
        }
    }
}
