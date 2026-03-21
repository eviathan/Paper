using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Paper.CSX;

namespace Paper.CSX.LanguageServer
{
    /// <summary>
    /// Provides single-file symbol rename via Roslyn.
    /// Finds all references to the symbol under the cursor and returns a WorkspaceEdit.
    /// </summary>
    internal static class RoslynRename
    {
        /// <summary>
        /// Returns the range of the identifier under the cursor that can be renamed,
        /// or null if there is no renameable symbol at that position.
        /// The inline rename UI shows the current name pre-populated inside that range.
        /// </summary>
        public static object? PrepareRename(string csxSrc, int csxLine, int csxCol)
        {
            try
            {
                var (compilation, tree, generatedSrc) = RoslynHover.GetOrBuildCompilation(csxSrc);
                var model = compilation.GetSemanticModel(tree);

                int offset = CsxPositionMapper.ToGeneratedOffset(csxSrc, generatedSrc, csxLine, csxCol);
                if (offset < 0) return null;

                var token = tree.GetRoot().FindToken(offset);
                if (!token.IsKind(SyntaxKind.IdentifierToken)) return null;

                var symbol = model.GetSymbolInfo(token.Parent!).Symbol
                          ?? model.GetDeclaredSymbol(token.Parent!);
                if (symbol == null) return null;

                // Return the range of the token in CSX coordinates so the editor highlights it
                var lineSpan = tree.GetLineSpan(token.Span);
                int genLine  = lineSpan.StartLinePosition.Line;
                int genCol   = lineSpan.StartLinePosition.Character;
                int genEndCol = lineSpan.EndLinePosition.Character;

                int genPreambleStart = RoslynSemanticTokens.FindGenPreambleStart(generatedSrc);
                int csxPreambleStart = CsxPositionMapper.FindPreambleStartLine(csxSrc);
                var csxLines         = csxSrc.Split('\n');

                if (genLine < genPreambleStart) return null;

                int mappedCsxLine = (genLine - genPreambleStart) + csxPreambleStart;
                if (mappedCsxLine < 0 || mappedCsxLine >= csxLines.Length) return null;

                string origLine = csxLines[mappedCsxLine];
                int leadingWs   = CsxPositionMapper.CountLeadingWhitespace(origLine);
                int csxStart    = Math.Max(0, genCol    - CsxPositionMapper.PreambleColOffset) + leadingWs;
                int csxEnd      = Math.Max(0, genEndCol - CsxPositionMapper.PreambleColOffset) + leadingWs;

                return new
                {
                    range = new
                    {
                        start = new { line = mappedCsxLine, character = csxStart },
                        end   = new { line = mappedCsxLine, character = csxEnd   },
                    },
                    placeholder = symbol.Name,
                };
            }
            catch
            {
                return null;
            }
        }

        public static object? GetRename(string csxSrc, string csxUri, int csxLine, int csxCol, string newName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newName)) return null;

                var (compilation, tree, generatedSrc) = RoslynHover.GetOrBuildCompilation(csxSrc);
                var model = compilation.GetSemanticModel(tree);

                int offset = CsxPositionMapper.ToGeneratedOffset(csxSrc, generatedSrc, csxLine, csxCol);
                if (offset < 0) return null;

                var token = tree.GetRoot().FindToken(offset);
                if (token.IsKind(SyntaxKind.None)) return null;

                var symbol = model.GetSymbolInfo(token.Parent!).Symbol
                          ?? model.GetDeclaredSymbol(token.Parent!);
                if (symbol == null) return null;

                int genPreambleStart = RoslynSemanticTokens.FindGenPreambleStart(generatedSrc);
                int csxPreambleStart = CsxPositionMapper.FindPreambleStartLine(csxSrc);
                var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(csxSrc);
                int genPreambleEnd = genPreambleStart + (preamble?.Split('\n').Length ?? 0);
                var csxLines = csxSrc.Split('\n');

                var edits = new HashSet<RenameEdit>();

                // Walk all identifier nodes and tokens that resolve to the same symbol
                foreach (var node in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    var nodeSymbol = model.GetSymbolInfo(node).Symbol
                                  ?? model.GetSymbolInfo(node).CandidateSymbols.FirstOrDefault();
                    if (nodeSymbol == null || !SymbolsMatch(symbol, nodeSymbol)) continue;

                    var edit = MakeEdit(tree, node.Span, genPreambleStart, genPreambleEnd,
                                        csxPreambleStart, csxLines, newName);
                    if (edit != null) edits.Add(edit);
                }

                // Also handle declaration sites (variable declarator, parameter, local function)
                foreach (var token2 in tree.GetRoot().DescendantTokens())
                {
                    if (!token2.IsKind(SyntaxKind.IdentifierToken)) continue;
                    if (token2.Text != symbol.Name) continue;

                    var declared = model.GetDeclaredSymbol(token2.Parent!);
                    if (declared == null || !SymbolsMatch(symbol, declared)) continue;

                    var edit = MakeEdit(tree, token2.Span, genPreambleStart, genPreambleEnd,
                                        csxPreambleStart, csxLines, newName);
                    if (edit != null) edits.Add(edit); // HashSet deduplicates by value
                }

                if (edits.Count == 0) return null;

                return new
                {
                    changes = new Dictionary<string, object[]>
                    {
                        [csxUri] = edits.Select(e => e.ToLspEdit()).ToArray()
                    }
                };
            }
            catch
            {
                return null;
            }
        }

        private static RenameEdit? MakeEdit(
            SyntaxTree tree, Microsoft.CodeAnalysis.Text.TextSpan span,
            int genPreambleStart, int genPreambleEnd,
            int csxPreambleStart, string[] csxLines, string newName)
        {
            var lineSpan = tree.GetLineSpan(span);
            int genLine   = lineSpan.StartLinePosition.Line;
            int genCol    = lineSpan.StartLinePosition.Character;
            int genEndCol = lineSpan.EndLinePosition.Character;

            if (genLine < genPreambleStart || genLine >= genPreambleEnd) return null;

            int csxLine = (genLine - genPreambleStart) + csxPreambleStart;
            if (csxLine < 0 || csxLine >= csxLines.Length) return null;

            string origLine = csxLines[csxLine];
            int leadingWs   = CsxPositionMapper.CountLeadingWhitespace(origLine);
            int csxStart    = Math.Max(0, genCol    - CsxPositionMapper.PreambleColOffset) + leadingWs;
            int csxEnd      = Math.Max(0, genEndCol - CsxPositionMapper.PreambleColOffset) + leadingWs;

            return new RenameEdit(csxLine, csxStart, csxEnd, newName);
        }

        private static bool SymbolsMatch(ISymbol a, ISymbol b)
            => SymbolEqualityComparer.Default.Equals(a, b)
            || SymbolEqualityComparer.Default.Equals(a.OriginalDefinition, b.OriginalDefinition);
    }

    /// <summary>Concrete edit record so deduplication works by value, not object identity.</summary>
    internal sealed record RenameEdit(int Line, int StartChar, int EndChar, string NewText)
    {
        public object ToLspEdit() => new
        {
            range = new
            {
                start = new { line = Line, character = StartChar },
                end   = new { line = Line, character = EndChar   },
            },
            newText = NewText,
        };
    }
}
