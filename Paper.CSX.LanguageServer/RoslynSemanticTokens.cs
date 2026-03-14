using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paper.CSX.LanguageServer
{
    /// <summary>
    /// Provides LSP semantic tokens for the C# preamble of a CSX file using Roslyn.
    /// Token types use VS Code's built-in names so themes automatically apply the right colours.
    /// </summary>
    internal static class RoslynSemanticTokens
    {
        // Must match the legend declared in SimpleLspServer capabilities (index = position in array).
        public static readonly string[] TokenTypes =
        [
            "namespace",     // 0
            "class",         // 1
            "struct",        // 2
            "interface",     // 3
            "enum",          // 4
            "typeParameter", // 5
            "function",      // 6
            "method",        // 7
            "property",      // 8
            "field",         // 9
            "variable",      // 10
            "parameter",     // 11
        ];

        private record struct TokenEntry(int Line, int Col, int Length, int TypeIndex);

        /// <summary>
        /// Returns the LSP-encoded flat token array (5 ints per token: deltaLine, deltaStartChar,
        /// length, tokenType, tokenModifiers) for all identifiers in the CSX preamble.
        /// </summary>
        public static int[] GetEncodedTokens(string csxSrc)
        {
            try
            {
                var (compilation, tree, generatedSrc) = RoslynHover.GetOrBuildCompilation(csxSrc);
                var model = compilation.GetSemanticModel(tree);
                var root  = tree.GetRoot();

                int genPreambleStart = FindGenPreambleStart(generatedSrc);
                int csxPreambleStart = CsxPositionMapper.FindPreambleStartLine(csxSrc);

                var csxLines = csxSrc.Split('\n');
                var entries  = new List<TokenEntry>();

                foreach (var node in root.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    var lineSpan = tree.GetLineSpan(node.Span);
                    int genLine  = lineSpan.StartLinePosition.Line;
                    int genCol   = lineSpan.StartLinePosition.Character;

                    // Only tokens inside the preamble (method body)
                    if (genLine < genPreambleStart) continue;

                    int csxLine = (genLine - genPreambleStart) + csxPreambleStart;
                    if (csxLine < 0 || csxLine >= csxLines.Length) continue;

                    // Reverse the column shift: genCol = (csxCol - leadingWs) + PreambleColOffset
                    string origLine = csxLines[csxLine];
                    int leadingWs   = origLine.Length - origLine.TrimStart().Length;
                    int csxCol      = genCol - CsxPositionMapper.PreambleColOffset + leadingWs;
                    if (csxCol < 0) csxCol = 0;

                    int? typeIndex = Classify(node, model);
                    if (typeIndex == null) continue;

                    entries.Add(new TokenEntry(csxLine, csxCol, node.Identifier.Text.Length, typeIndex.Value));
                }

                // LSP requires tokens sorted by position
                entries.Sort((a, b) => a.Line != b.Line ? a.Line - b.Line : a.Col - b.Col);
                return Encode(entries);
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// Finds the first line of the method body in the in-memory generated source by scanning
        /// for the opening '{' that follows the Render method signature.
        /// </summary>
        internal static int FindGenPreambleStart(string generatedSrc)
        {
            var lines = generatedSrc.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("public static UINode Render("))
                {
                    for (int j = i + 1; j < lines.Length; j++)
                        if (lines[j].Trim() == "{") return j + 1;
                }
            }
            return 14; // safe fallback
        }

        private static int? Classify(IdentifierNameSyntax node, SemanticModel model)
        {
            var symbol = model.GetSymbolInfo(node).Symbol
                      ?? model.GetSymbolInfo(node).CandidateSymbols.FirstOrDefault();
            if (symbol == null) return null;

            return symbol switch
            {
                INamespaceSymbol                                      => 0,  // namespace
                INamedTypeSymbol { TypeKind: TypeKind.Class }         => 1,  // class
                INamedTypeSymbol { TypeKind: TypeKind.Struct }        => 2,  // struct
                INamedTypeSymbol { TypeKind: TypeKind.Interface }     => 3,  // interface
                INamedTypeSymbol { TypeKind: TypeKind.Enum }          => 4,  // enum
                ITypeParameterSymbol                                   => 5,  // typeParameter
                IMethodSymbol { MethodKind: MethodKind.LocalFunction } => 6,  // function
                IMethodSymbol                                          => 7,  // method
                IPropertySymbol                                        => 8,  // property
                IFieldSymbol                                           => 9,  // field
                ILocalSymbol                                           => 10, // variable
                IParameterSymbol                                       => 11, // parameter
                _ => null
            };
        }

        private static int[] Encode(List<TokenEntry> tokens)
        {
            var data     = new List<int>(tokens.Count * 5);
            int prevLine = 0, prevCol = 0;
            foreach (var t in tokens)
            {
                int deltaLine = t.Line - prevLine;
                int deltaCol  = deltaLine == 0 ? t.Col - prevCol : t.Col;
                data.Add(deltaLine);
                data.Add(deltaCol);
                data.Add(t.Length);
                data.Add(t.TypeIndex);
                data.Add(0); // modifiers (none)
                prevLine = t.Line;
                prevCol  = t.Col;
            }
            return [.. data];
        }
    }
}
