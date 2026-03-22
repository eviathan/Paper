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
        /// Counts the number of leading whitespace characters on <paramref name="line"/>,
        /// expanding tabs to the nearest tab stop so that LSP character offsets are correct.
        /// </summary>
        internal static int CountLeadingWhitespace(string line, int tabSize = 4)
        {
            int col = 0;
            foreach (char c in line)
            {
                if (c == ' ')  { col++; continue; }
                if (c == '\t') { col += tabSize - (col % tabSize); continue; }
                break;
            }
            return col;
        }

        /// <summary>
        /// Maps a (line, character) cursor in the CSX source to a character offset in
        /// <paramref name="generatedSrc"/>.  Returns -1 if the position is not inside the preamble.
        /// </summary>
        public static int ToGeneratedOffset(string csxSrc, string generatedSrc, int csxLine, int csxCol)
        {
            int preambleStart    = FindPreambleStartLine(csxSrc);
            int genPreambleStart = RoslynSemanticTokens.FindGenPreambleStart(generatedSrc);

            if (csxLine < preambleStart)
            {
                // For the function-declaration line (UINode App(Props props) {), the token at the
                // cursor may also appear in the generated method signature line.  Find the word under
                // the cursor and locate it in the generated signature so Roslyn can resolve it.
                var csxLines2 = csxSrc.Split('\n');
                string declLine = csxLine < csxLines2.Length ? csxLines2[csxLine] : "";
                string word = ExtractWordAt(declLine, csxCol);
                if (string.IsNullOrEmpty(word)) return -1;

                // The generated signature is 2 lines above the preamble body ({genPreambleStart - 2}).
                int sigLine = genPreambleStart - 2;
                if (sigLine < 0) return -1;
                string genSigLine = GetLine(generatedSrc, sigLine);
                int wordIdx = genSigLine.IndexOf(word, StringComparison.Ordinal);
                if (wordIdx < 0) return -1;

                return LineColToOffset(generatedSrc, sigLine, wordIdx);
            }

            int depth   = csxLine - preambleStart;
            int genLine = genPreambleStart + depth;

            // Preamble lines are .Trim()-ed then re-indented with 8 spaces.
            // Strip the original leading whitespace from the column so the offset is correct.
            // Tab characters are expanded to tab stops so that LSP character offsets align correctly.
            var csxLines = csxSrc.Split('\n');
            string origLine = csxLine < csxLines.Length ? csxLines[csxLine] : "";
            int leadingWs = CountLeadingWhitespace(origLine);
            int genCol = Math.Max(0, csxCol - leadingWs) + PreambleColOffset;

            return LineColToOffset(generatedSrc, genLine, genCol);
        }

        /// <summary>
        /// Returns the 0-indexed line in <paramref name="csxSrc"/> where the preamble content begins.
        ///
        /// Two cases:
        /// <list type="bullet">
        /// <item><b>Simple</b> — all code is inside a single entry function (e.g. App.csx):
        ///   preamble starts on the line after the function declaration.</item>
        /// <item><b>Complex</b> — module-level declarations appear before the first
        ///   <c>function</c> keyword (e.g. DemoApp.csx): preamble starts at the first
        ///   non-@import, non-blank line because the entire CSX body is flattened into
        ///   the generated method.</item>
        /// </list>
        /// </summary>
        public static int FindPreambleStartLine(string csxSrc)
        {
            var lines = csxSrc.Split('\n');
            int i = 0;

            // Skip @import lines at the top
            while (i < lines.Length && lines[i].TrimStart().StartsWith("@import", StringComparison.OrdinalIgnoreCase))
                i++;

            // Check for module-level code (non-blank, non-function) before the first function
            for (int j = i; j < lines.Length; j++)
            {
                var trimmed = lines[j].Trim();
                if (trimmed.Length == 0) continue;
                if (Regex.IsMatch(trimmed, @"^UINode(?:<[^>]*>)?\s+\w")) break; // no module-level code found
                return j;                                             // complex: preamble starts here
            }

            // Simple: preamble starts on the line after the entry function declaration
            for (int j = i; j < lines.Length; j++)
            {
                if (Regex.IsMatch(lines[j].Trim(), @"^UINode\s+\w"))
                    return j + 1;
            }

            return i;
        }

        /// <summary>Extracts the identifier/word under column <paramref name="col"/> in <paramref name="line"/>.</summary>
        private static string ExtractWordAt(string line, int col)
        {
            if (col >= line.Length) return "";
            int start = col;
            while (start > 0 && (char.IsLetterOrDigit(line[start - 1]) || line[start - 1] == '_')) start--;
            int end = col;
            while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_')) end++;
            return start < end ? line[start..end] : "";
        }

        /// <summary>Returns the text of 0-indexed <paramref name="lineIndex"/> from <paramref name="src"/>.</summary>
        private static string GetLine(string src, int lineIndex)
        {
            int line = 0, pos = 0;
            while (pos < src.Length && line < lineIndex)
            {
                if (src[pos++] == '\n') line++;
            }
            int end = pos;
            while (end < src.Length && src[end] != '\n') end++;
            return src[pos..end];
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
