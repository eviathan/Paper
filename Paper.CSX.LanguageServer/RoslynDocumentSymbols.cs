using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paper.CSX.LanguageServer
{
    internal static class RoslynDocumentSymbols
    {
        // LSP SymbolKind values
        private const int KindVariable = 13;
        private const int KindFunction  = 12;

        /// <summary>
        /// Returns an array of LSP DocumentSymbol objects for all local variable declarations
        /// found in the CSX preamble.
        /// </summary>
        public static object[] GetDocumentSymbols(string csxSrc, string csxUri)
        {
            try
            {
                var (compilation, tree, generatedSrc) = RoslynHover.GetOrBuildCompilation(csxSrc);

                int genPreambleStart = RoslynSemanticTokens.FindGenPreambleStart(generatedSrc);
                int csxPreambleStart = CsxPositionMapper.FindPreambleStartLine(csxSrc);
                var csxLines         = csxSrc.Split('\n');

                var symbols = new List<object>();

                foreach (var decl in tree.GetRoot().DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
                {
                    var lineSpan    = tree.GetLineSpan(decl.Span);
                    int genDeclLine = lineSpan.StartLinePosition.Line;

                    // Only symbols inside the preamble (method body)
                    if (genDeclLine < genPreambleStart) continue;

                    int csxDeclLine = (genDeclLine - genPreambleStart) + csxPreambleStart;
                    if (csxDeclLine < 0 || csxDeclLine >= csxLines.Length) continue;

                    string origLine   = csxLines[csxDeclLine];
                    int    leadingWs  = CsxPositionMapper.CountLeadingWhitespace(origLine);

                    int genDeclEndLine  = lineSpan.EndLinePosition.Line;
                    int csxDeclEndLine  = (genDeclEndLine - genPreambleStart) + csxPreambleStart;
                    int genDeclStartCol = lineSpan.StartLinePosition.Character;
                    int genDeclEndCol   = lineSpan.EndLinePosition.Character;

                    int csxDeclStartChar = Math.Max(0, genDeclStartCol - CsxPositionMapper.PreambleColOffset) + leadingWs;

                    int csxDeclEndChar;
                    if (csxDeclEndLine >= 0 && csxDeclEndLine < csxLines.Length)
                    {
                        string origEndLine = csxLines[csxDeclEndLine];
                        int    leadingWsEnd = CsxPositionMapper.CountLeadingWhitespace(origEndLine);
                        csxDeclEndChar = Math.Max(0, genDeclEndCol - CsxPositionMapper.PreambleColOffset) + leadingWsEnd;
                    }
                    else
                    {
                        csxDeclEndChar = csxDeclStartChar;
                    }

                    foreach (var variable in decl.Declaration.Variables)
                    {
                        var varSpan    = tree.GetLineSpan(variable.Identifier.Span);
                        int genVarLine = varSpan.StartLinePosition.Line;
                        int genVarCol  = varSpan.StartLinePosition.Character;

                        int csxVarLine = (genVarLine - genPreambleStart) + csxPreambleStart;
                        if (csxVarLine < 0 || csxVarLine >= csxLines.Length) continue;

                        string origVarLine  = csxLines[csxVarLine];
                        int    leadingWsVar = CsxPositionMapper.CountLeadingWhitespace(origVarLine);
                        int    csxVarChar   = Math.Max(0, genVarCol - CsxPositionMapper.PreambleColOffset) + leadingWsVar;
                        int    nameLen      = variable.Identifier.Text.Length;

                        symbols.Add(new
                        {
                            name  = variable.Identifier.Text,
                            kind  = KindVariable,
                            range = new
                            {
                                start = new { line = csxDeclLine, character = csxDeclStartChar },
                                end   = new { line = csxDeclEndLine < csxLines.Length ? csxDeclEndLine : csxDeclLine,
                                              character = csxDeclEndChar },
                            },
                            selectionRange = new
                            {
                                start = new { line = csxVarLine, character = csxVarChar },
                                end   = new { line = csxVarLine, character = csxVarChar + nameLen },
                            },
                        });
                    }
                }

                // Also emit symbols for local functions (CSX helper functions)
                foreach (var fn in tree.GetRoot().DescendantNodes().OfType<LocalFunctionStatementSyntax>())
                {
                    var lineSpan    = tree.GetLineSpan(fn.Span);
                    int genFnLine   = lineSpan.StartLinePosition.Line;
                    if (genFnLine < genPreambleStart) continue;

                    int csxFnLine = (genFnLine - genPreambleStart) + csxPreambleStart;
                    if (csxFnLine < 0 || csxFnLine >= csxLines.Length) continue;

                    string origLine  = csxLines[csxFnLine];
                    int    leadingWs = CsxPositionMapper.CountLeadingWhitespace(origLine);

                    var nameSpan   = tree.GetLineSpan(fn.Identifier.Span);
                    int genNameCol = nameSpan.StartLinePosition.Character;
                    int csxNameCol = Math.Max(0, genNameCol - CsxPositionMapper.PreambleColOffset) + leadingWs;
                    int nameLen    = fn.Identifier.Text.Length;

                    int genEndLine = lineSpan.EndLinePosition.Line;
                    int csxEndLine = (genEndLine - genPreambleStart) + csxPreambleStart;
                    int genEndCol  = lineSpan.EndLinePosition.Character;
                    string origEndLine = csxEndLine >= 0 && csxEndLine < csxLines.Length ? csxLines[csxEndLine] : origLine;
                    int csxEndCol = Math.Max(0, genEndCol - CsxPositionMapper.PreambleColOffset)
                                    + (CsxPositionMapper.CountLeadingWhitespace(origEndLine));

                    symbols.Add(new
                    {
                        name  = fn.Identifier.Text,
                        kind  = KindFunction,
                        range = new
                        {
                            start = new { line = csxFnLine, character = leadingWs },
                            end   = new { line = Math.Min(csxEndLine, csxLines.Length - 1), character = csxEndCol },
                        },
                        selectionRange = new
                        {
                            start = new { line = csxFnLine, character = csxNameCol },
                            end   = new { line = csxFnLine, character = csxNameCol + nameLen },
                        },
                    });
                }

                return symbols.ToArray();
            }
            catch
            {
                return [];
            }
        }
    }
}
