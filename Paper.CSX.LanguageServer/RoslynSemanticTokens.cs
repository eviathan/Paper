using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Paper.CSX;

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
            "operator",      // 12  — used for => lambda arrow
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

                // Only emit tokens for the preamble — stop before the JSX return statement.
                var (preamble, _, _, _) = Paper.CSX.CSXCompiler.ExtractPreambleAndJsx(csxSrc);
                int genPreambleEnd = genPreambleStart + (preamble?.Split('\n').Length ?? 0);

                var csxLines = csxSrc.Split('\n');
                var entries  = new List<TokenEntry>();

                foreach (var node in root.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    var lineSpan = tree.GetLineSpan(node.Span);
                    int genLine  = lineSpan.StartLinePosition.Line;
                    int genCol   = lineSpan.StartLinePosition.Character;

                    // Only tokens inside the preamble (not JSX return)
                    if (genLine < genPreambleStart || genLine >= genPreambleEnd) continue;

                    // Skip compiler-generated identifiers
                    var identText = node.Identifier.Text;
                    if (identText.StartsWith("__") || identText.StartsWith("<>") || identText.Contains('<')) continue;

                    int csxLine = (genLine - genPreambleStart) + csxPreambleStart;
                    if (csxLine < 0 || csxLine >= csxLines.Length) continue;

                    // Reverse the column shift: genCol = (csxCol - leadingWs) + PreambleColOffset
                    string origLine = csxLines[csxLine];
                    int leadingWs   = CsxPositionMapper.CountLeadingWhitespace(origLine);
                    int csxCol      = genCol - CsxPositionMapper.PreambleColOffset + leadingWs;
                    if (csxCol < 0) csxCol = 0;

                    int? typeIndex = Classify(node, model);
                    if (typeIndex == null) continue;

                    entries.Add(new TokenEntry(csxLine, csxCol, node.Identifier.Text.Length, typeIndex.Value));
                }

                // Emit semantic tokens for => (lambda arrow) in the preamble.
                // Roslyn SyntaxKind.EqualsGreaterThanToken is the only source of => in code (not strings/comments).
                foreach (var token in root.DescendantTokens())
                {
                    if (token.RawKind != (int)SyntaxKind.EqualsGreaterThanToken) continue;

                    var lineSpan = tree.GetLineSpan(token.Span);
                    int genLine  = lineSpan.StartLinePosition.Line;
                    int genCol   = lineSpan.StartLinePosition.Character;

                    if (genLine < genPreambleStart || genLine >= genPreambleEnd) continue;

                    int csxLine = (genLine - genPreambleStart) + csxPreambleStart;
                    if (csxLine < 0 || csxLine >= csxLines.Length) continue;

                    string origLine = csxLines[csxLine];
                    int leadingWs   = CsxPositionMapper.CountLeadingWhitespace(origLine);
                    int csxCol      = genCol - CsxPositionMapper.PreambleColOffset + leadingWs;
                    if (csxCol < 0) csxCol = 0;

                    entries.Add(new TokenEntry(csxLine, csxCol, 2, 12)); // length=2 (=>), type=operator
                }

                // Extend to JSX {…} expression zones
                AddJsxInterpolationTokens(csxSrc, model, generatedSrc, entries);

                // LSP requires tokens sorted by position
                entries.Sort((a, b) => a.Line != b.Line ? a.Line - b.Line : a.Col - b.Col);
                return Encode(entries);
            }
            catch
            {
                return [];
            }
        }

        // ── JSX interpolation token classification ────────────────────────────

        private static readonly Regex IdentifierPattern = new(@"\b([A-Za-z_]\w*)\b", RegexOptions.Compiled);

        /// <summary>
        /// Scans the JSX portion of the CSX source for <c>{…}</c> interpolation zones and emits
        /// semantic tokens for any identifiers that resolve to in-scope C# symbols.
        /// </summary>
        private static void AddJsxInterpolationTokens(
            string csxSrc,
            SemanticModel model,
            string generatedSrc,
            List<TokenEntry> entries)
        {
            // Pre-build the lookup set from the preamble's scope so we avoid repeated model calls.
            int preambleEndOffset = GetPreambleEndOffset(generatedSrc);
            if (preambleEndOffset < 0) return;

            var inScopeSymbols = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var sym in model.LookupSymbols(preambleEndOffset))
            {
                int? typeIdx = sym switch
                {
                    ILocalSymbol     => 10,
                    IParameterSymbol => 11,
                    IMethodSymbol { MethodKind: MethodKind.LocalFunction } => 6,
                    IMethodSymbol    => 7,
                    IPropertySymbol  => 8,
                    IFieldSymbol     => 9,
                    INamedTypeSymbol { TypeKind: TypeKind.Class }     => 1,
                    INamedTypeSymbol { TypeKind: TypeKind.Struct }    => 2,
                    INamedTypeSymbol { TypeKind: TypeKind.Interface } => 3,
                    _                => null,
                };
                if (typeIdx.HasValue && !inScopeSymbols.ContainsKey(sym.Name))
                    inScopeSymbols[sym.Name] = typeIdx.Value;
            }
            if (inScopeSymbols.Count == 0) return;

            // Build a set of (line,col) already covered by preamble tokens to avoid duplicates.
            var covered = new HashSet<(int, int)>(entries.Select(e => (e.Line, e.Col)));

            var csxLines = csxSrc.Split('\n');
            int jsxStart = FindJsxStartLine(csxLines);
            if (jsxStart < 0) return;

            // Scan from the return-statement line onwards for {…} zones.
            foreach (var (zoneLine, zoneCol, zoneText) in ExtractJsxZones(csxLines, jsxStart))
            {
                var zoneLines = zoneText.Split('\n');
                foreach (Match m in IdentifierPattern.Matches(zoneText))
                {
                    var name = m.Groups[1].Value;
                    if (!inScopeSymbols.TryGetValue(name, out int typeIdx)) continue;

                    // Skip keywords and common non-variable identifiers
                    if (CSharpKeywords.Contains(name)) continue;

                    // Compute absolute (line, col) in the CSX source
                    int relLine = CountNewlines(zoneText, m.Index);
                    int relCol  = m.Index - (relLine == 0 ? 0 : zoneText.LastIndexOf('\n', m.Index) + 1);
                    int absLine = zoneLine + relLine;
                    int absCol  = relLine == 0 ? zoneCol + relCol : relCol;

                    if (absLine < 0 || absLine >= csxLines.Length) continue;
                    if (covered.Contains((absLine, absCol))) continue;

                    entries.Add(new TokenEntry(absLine, absCol, name.Length, typeIdx));
                    covered.Add((absLine, absCol));
                }
            }
        }

        /// <summary>Returns the offset in the generated source at the end of the preamble (just before "return").</summary>
        private static int GetPreambleEndOffset(string generatedSrc)
        {
            int returnIdx = generatedSrc.IndexOf("\n        return ", StringComparison.Ordinal);
            if (returnIdx < 0) returnIdx = generatedSrc.IndexOf("\nreturn ", StringComparison.Ordinal);
            return returnIdx > 0 ? returnIdx : -1;
        }

        /// <summary>Finds the 0-indexed line number in the CSX source where the JSX return statement begins.</summary>
        private static int FindJsxStartLine(string[] csxLines)
        {
            for (int i = 0; i < csxLines.Length; i++)
            {
                var trimmed = csxLines[i].TrimStart();
                if (trimmed.StartsWith("return (", StringComparison.Ordinal) ||
                    trimmed.StartsWith("return(<",  StringComparison.Ordinal) ||
                    trimmed.StartsWith("return <",  StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Yields all <c>{…}</c> interpolation zones in the JSX portion of the CSX source.
        /// Returns (startLine, startCol, zoneText) tuples — the position of the character
        /// immediately after the opening <c>{</c>, and the text up to the matching <c>}</c>.
        /// </summary>
        private static IEnumerable<(int line, int col, string text)> ExtractJsxZones(
            string[] csxLines, int jsxStartLine)
        {
            int depth       = 0;       // brace depth relative to JSX context
            int zoneStartLn = 0;
            int zoneStartCl = 0;
            var zoneBuilder = new System.Text.StringBuilder();
            bool inZone       = false;
            bool inSingleQ    = false;
            bool inDoubleQ    = false;
            bool inTemplateLit = false;

            for (int ln = jsxStartLine; ln < csxLines.Length; ln++)
            {
                var line = csxLines[ln];
                for (int col = 0; col < line.Length; col++)
                {
                    char c = line[col];
                    char next = col + 1 < line.Length ? line[col + 1] : '\0';

                    // Track string contexts so braces inside strings don't count
                    if (!inDoubleQ && !inTemplateLit && c == '\'' && (col == 0 || line[col - 1] != '\\'))
                    { inSingleQ = !inSingleQ; }
                    else if (!inSingleQ && !inTemplateLit && c == '"' && (col == 0 || line[col - 1] != '\\'))
                    { inDoubleQ = !inDoubleQ; }
                    else if (!inSingleQ && !inDoubleQ && c == '`' && (col == 0 || line[col - 1] != '\\'))
                    { inTemplateLit = !inTemplateLit; }

                    if (inSingleQ || inDoubleQ || inTemplateLit)
                    {
                        if (inZone) zoneBuilder.Append(c);
                        continue;
                    }

                    if (c == '{')
                    {
                        if (depth == 0)
                        {
                            // Opening a new top-level JSX interpolation zone
                            inZone       = true;
                            zoneStartLn  = ln;
                            zoneStartCl  = col + 1;
                            zoneBuilder.Clear();
                        }
                        else if (inZone)
                        {
                            zoneBuilder.Append(c);
                        }
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0 && inZone)
                        {
                            yield return (zoneStartLn, zoneStartCl, zoneBuilder.ToString());
                            inZone = false;
                        }
                        else if (inZone)
                        {
                            zoneBuilder.Append(c);
                        }
                    }
                    else if (inZone)
                    {
                        zoneBuilder.Append(c);
                    }
                }
                // End of line — if inside a zone, add a newline to preserve line offsets
                if (inZone) zoneBuilder.Append('\n');
            }
        }

        private static int CountNewlines(string s, int upTo)
        {
            int count = 0;
            for (int i = 0; i < upTo && i < s.Length; i++)
                if (s[i] == '\n') count++;
            return count;
        }

        // C# keywords that look like identifiers but are never symbols
        private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
        {
            "true","false","null","new","this","base","typeof","sizeof","nameof","default",
            "if","else","for","foreach","while","do","switch","case","break","continue","return",
            "var","void","int","long","short","byte","float","double","decimal","bool","char","string","object",
            "class","struct","interface","enum","namespace","using","static","public","private","protected",
            "internal","readonly","const","override","virtual","abstract","sealed","async","await",
            "in","out","ref","params","is","as","where","select","from","let","join","on","equals","into",
            "throw","try","catch","finally","lock","checked","unchecked","fixed","unsafe",
        };

        /// <summary>
        /// Finds the first line of the method body in the in-memory generated source by scanning
        /// for the opening '{' that follows the Render method signature.
        /// </summary>
        internal static int FindGenPreambleStart(string generatedSrc)
        {
            var lines = generatedSrc.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("public static UINode "))
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
