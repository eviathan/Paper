namespace Paper.CSSS.Preprocessor
{
    /// <summary>
    /// A flat, resolved CSS rule produced by the preprocessor:
    /// one or more selectors + a list of property/value declarations.
    /// This is the output of the CSSS preprocessor pipeline.
    /// </summary>
    internal sealed record CSSSRule(
        List<string>                   Selectors,
        List<(string prop, string val)> Declarations)
    {
        /// <summary>Optional wrapping at-rule, e.g. "@media (max-width: 768px)".</summary>
        public string? AtRule { get; init; }

        public override string ToString()
        {
            string sel   = string.Join(", ", Selectors);
            string decls = string.Join("; ", Declarations.Select(d => $"{d.prop}: {d.val}"));
            return AtRule != null
                ? $"{AtRule} {{ {sel} {{ {decls} }} }}"
                : $"{sel} {{ {decls} }}";
        }
    }
}
