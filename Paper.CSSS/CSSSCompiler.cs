using Paper.Core.Styles;
using Paper.CSSS.Lexer;
using Paper.CSSS.Parser;
using Paper.CSSS.Preprocessor;

namespace Paper.CSSS
{
    /// <summary>
    /// The public entry point for Paper's CSSS compiler.
    /// Parses CSSS source text and returns a dictionary mapping CSS selectors
    /// to <see cref="StyleSheet"/> instances.
    ///
    /// Usage:
    /// <code>
    ///   var styles = CSSSCompiler.Compile(@"
    ///     $primary: #4CAF50;
    ///     .button {
    ///       background: $primary;
    ///       padding: 8px 16px;
    ///       border-radius: 4px;
    ///       &:hover { opacity: 0.9; }
    ///     }
    ///   ");
    ///   var buttonStyle = styles[".button"];
    /// </code>
    /// </summary>
    public static class CSSSCompiler
    {
        /// <summary>
        /// Compile CSSS source text into a map of selector → <see cref="StyleSheet"/>.
        /// Multiple selectors for the same rule are stored individually.
        /// </summary>
        public static Dictionary<string, StyleSheet> Compile(string csss)
        {
            var tokens = new CSSSLexer(csss).Tokenise();
            var ast    = new CSSSParser(tokens).Parse();
            var rules  = new CSSSPreprocessor().Process(ast);
            return StyleSheetMapper.MapRules(rules);
        }

        /// <summary>
        /// Compile CSSS and return the style for a specific selector.
        /// Returns <see cref="StyleSheet.Empty"/> if the selector is not found.
        /// </summary>
        public static StyleSheet CompileOne(string csss, string selector)
        {
            var map = Compile(csss);
            return map.TryGetValue(selector, out var s) ? s : StyleSheet.Empty;
        }

        /// <summary>
        /// Parse a single inline CSS value string and apply it to the given property name,
        /// returning a new <see cref="StyleSheet"/> with just that property set.
        /// Useful for dynamic style application.
        /// </summary>
        public static StyleSheet FromDeclaration(string property, string value) =>
            StyleSheetMapper.ApplyDeclaration(StyleSheet.Empty, property, value);
    }
}
