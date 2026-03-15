using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paper.CSX.LanguageServer
{
    internal static class RoslynDefinition
    {
        /// <summary>
        /// Returns a single LSP Location object for the declaration of the symbol under the cursor,
        /// or null if no definition can be found.
        /// </summary>
        public static object? GetDefinition(string csxSrc, string csxUri, int csxLine, int csxCol)
        {
            try
            {
                var (compilation, tree, generatedSrc) = RoslynHover.GetOrBuildCompilation(csxSrc);

                int genOffset = CsxPositionMapper.ToGeneratedOffset(csxSrc, generatedSrc, csxLine, csxCol);
                if (genOffset < 0) return null;

                var model = compilation.GetSemanticModel(tree);
                var root  = tree.GetRoot();

                var token = root.FindToken(genOffset);
                var node  = token.Parent;
                if (node == null) return null;

                ISymbol? symbol = model.GetSymbolInfo(node).Symbol
                               ?? model.GetSymbolInfo(node).CandidateSymbols.FirstOrDefault();

                if (symbol == null)
                    symbol = model.GetDeclaredSymbol(node);

                if (symbol == null) return null;

                // Pre-compute reverse-mapping anchors
                int csxPreambleStart = CsxPositionMapper.FindPreambleStartLine(csxSrc);
                int genPreambleStart = RoslynSemanticTokens.FindGenPreambleStart(generatedSrc);
                var csxLines         = csxSrc.Split('\n');

                foreach (var location in symbol.Locations)
                {
                    if (!location.IsInSource) continue;
                    // Only care about locations inside our in-memory generated tree
                    if (location.SourceTree != tree) continue;

                    var lineSpan = location.GetLineSpan();
                    int genStartLine = lineSpan.StartLinePosition.Line;
                    int genStartChar = lineSpan.StartLinePosition.Character;
                    int genEndLine   = lineSpan.EndLinePosition.Line;
                    int genEndChar   = lineSpan.EndLinePosition.Character;

                    // Only map positions that fall inside the preamble
                    if (genStartLine < genPreambleStart) continue;

                    int csxStartLine = (genStartLine - genPreambleStart) + csxPreambleStart;
                    int csxEndLine   = (genEndLine   - genPreambleStart) + csxPreambleStart;

                    if (csxStartLine < 0 || csxStartLine >= csxLines.Length) continue;

                    string origStartLine = csxLines[csxStartLine];
                    int leadingWsStart   = CsxPositionMapper.CountLeadingWhitespace(origStartLine);
                    int csxStartChar     = Math.Max(0, genStartChar - CsxPositionMapper.PreambleColOffset) + leadingWsStart;

                    int csxEndChar;
                    if (csxEndLine >= 0 && csxEndLine < csxLines.Length)
                    {
                        string origEndLine = csxLines[csxEndLine];
                        int leadingWsEnd   = CsxPositionMapper.CountLeadingWhitespace(origEndLine);
                        csxEndChar = Math.Max(0, genEndChar - CsxPositionMapper.PreambleColOffset) + leadingWsEnd;
                    }
                    else
                    {
                        csxEndChar = csxStartChar;
                    }

                    return new
                    {
                        uri   = csxUri,
                        range = new
                        {
                            start = new { line = csxStartLine, character = csxStartChar },
                            end   = new { line = csxEndLine,   character = csxEndChar   },
                        },
                    };
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
