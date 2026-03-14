using System.Text.RegularExpressions;

namespace Paper.CSX.LanguageServer
{
    /// <summary>
    /// Maps positions between CSX source and the generated C# source produced by
    /// <see cref="RoslynHover.GetOrBuildCompilation"/>.
    ///
    /// Generated C# template structure (0-indexed lines):
    ///   0-6  : using directives
    ///   7    : blank
    ///   8    : public static class _Ls…_
    ///   9    : {
    ///   10   : public static UINode Render(Props props)
    ///   11   : {
    ///   12…  : preamble lines (8-space indent, original leading whitespace stripped)
    /// </summary>
    internal static class CsxPositionMapper
    {
        // Column offset: preamble lines are re-indented with 8 spaces in the generated source.
        internal const int PreambleColOffset = 8;

        /// <summary>
        /// Maps a (line, character) cursor in the CSX source to a character offset in
        /// <paramref name="generatedSrc"/>.  Returns -1 if the position is not inside the preamble.
        /// </summary>
        public static int ToGeneratedOffset(string csxSrc, string generatedSrc, int csxLine, int csxCol)
        {
            int preambleStart    = FindPreambleStartLine(csxSrc);
            int genPreambleStart = RoslynSemanticTokens.FindGenPreambleStart(generatedSrc);

            // Position is before the preamble (inside an import or function-decl line) — clamp to start
            int depth   = csxLine < preambleStart ? 0 : csxLine - preambleStart;
            int genLine = genPreambleStart + depth;

            // Preamble lines are .Trim()-ed then re-indented with 8 spaces.
            // Strip the original leading whitespace from the column so the offset is correct.
            var csxLines = csxSrc.Split('\n');
            string origLine = csxLine < csxLines.Length ? csxLines[csxLine] : "";
            int leadingWs = origLine.Length - origLine.TrimStart().Length;
            int genCol = Math.Max(0, csxCol - leadingWs) + PreambleColOffset;

            return LineColToOffset(generatedSrc, genLine, genCol);
        }

        /// <summary>
        /// Returns the 0-indexed line in <paramref name="csxSrc"/> where the preamble content begins
        /// (i.e. first line inside the entry function body, after @import lines and the function-decl line).
        /// </summary>
        public static int FindPreambleStartLine(string csxSrc)
        {
            var lines = csxSrc.Split('\n');
            int i = 0;

            // Skip @import lines at the top (both .csx and .csss imports)
            while (i < lines.Length && lines[i].TrimStart().StartsWith("@import", StringComparison.OrdinalIgnoreCase))
                i++;

            // Scan forward past blank lines to find the entry function declaration
            int functionDeclLine = -1;
            for (int j = i; j < lines.Length; j++)
            {
                if (Regex.IsMatch(lines[j].Trim(), @"^function\s+\w"))
                {
                    functionDeclLine = j;
                    break;
                }
            }

            // Preamble starts on the line AFTER the function declaration
            return functionDeclLine >= 0 ? functionDeclLine + 1 : i;
        }

        private static int LineColToOffset(string src, int targetLine, int targetCol)
        {
            int line = 0, pos = 0;
            while (pos < src.Length && line < targetLine)
            {
                if (src[pos] == '\n') line++;
                pos++;
            }
            return Math.Min(pos + targetCol, src.Length);
        }
    }
}
