using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Paper.CSX;

namespace Paper.CSX.LanguageServer
{
    /// <summary>
    /// Provides LSP inlay hints showing inferred types for <c>var</c> declarations
    /// in the CSX preamble.
    /// </summary>
    internal static class RoslynInlayHints
    {
        public static object[] GetInlayHints(string csxSrc)
        {
            try
            {
                var (compilation, tree, generatedSrc) = RoslynHover.GetOrBuildCompilation(csxSrc);
                var model = compilation.GetSemanticModel(tree);

                int genPreambleStart = RoslynSemanticTokens.FindGenPreambleStart(generatedSrc);
                int csxPreambleStart = CsxPositionMapper.FindPreambleStartLine(csxSrc);
                var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(csxSrc);
                int genPreambleEnd = genPreambleStart + (preamble?.Split('\n').Length ?? 0);

                var csxLines = csxSrc.Split('\n');
                var hints = new List<object>();

                foreach (var decl in tree.GetRoot().DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
                {
                    // Only `var` declarations
                    if (!decl.Declaration.Type.IsVar) continue;

                    var lineSpan = tree.GetLineSpan(decl.Span);
                    int genLine  = lineSpan.StartLinePosition.Line;
                    if (genLine < genPreambleStart || genLine >= genPreambleEnd) continue;

                    int csxLine = (genLine - genPreambleStart) + csxPreambleStart;
                    if (csxLine < 0 || csxLine >= csxLines.Length) continue;

                    string origLine = csxLines[csxLine];
                    int leadingWs   = CsxPositionMapper.CountLeadingWhitespace(origLine);

                    foreach (var variable in decl.Declaration.Variables)
                    {
                        var typeInfo = model.GetTypeInfo(decl.Declaration.Type);
                        if (typeInfo.Type == null || typeInfo.Type.TypeKind == TypeKind.Error) continue;

                        var typeLabel = typeInfo.Type.ToDisplayString(
                            SymbolDisplayFormat.MinimallyQualifiedFormat);

                        // Position hint after the variable name
                        var varSpan  = tree.GetLineSpan(variable.Identifier.Span);
                        int genVarCol = varSpan.StartLinePosition.Character;
                        int csxVarCol = Math.Max(0, genVarCol - CsxPositionMapper.PreambleColOffset) + leadingWs;
                        int hintCol   = csxVarCol + variable.Identifier.Text.Length;

                        hints.Add(new
                        {
                            position = new { line = csxLine, character = hintCol },
                            label    = $": {typeLabel}",
                            kind     = 1,          // 1 = Type
                            paddingLeft = false,
                            paddingRight = false,
                        });
                    }
                }

                return hints.ToArray();
            }
            catch
            {
                return [];
            }
        }
    }
}
