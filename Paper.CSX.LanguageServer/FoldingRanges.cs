using Microsoft.CodeAnalysis.CSharp.Syntax;
using Paper.CSX;

namespace Paper.CSX.LanguageServer
{
    internal static class FoldingRanges
    {
        public static object[] GetFoldingRanges(string csxSrc)
        {
            try
            {
                var ranges = new List<object>();
                var csxLines = csxSrc.Split('\n');

                // ── C# block folding via Roslyn ────────────────────────────────────────
                var (compilation, tree, generatedSrc) = RoslynHover.GetOrBuildCompilation(csxSrc);
                int genPreambleStart = RoslynSemanticTokens.FindGenPreambleStart(generatedSrc);
                int csxPreambleStart = CsxPositionMapper.FindPreambleStartLine(csxSrc);
                var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(csxSrc);
                int genPreambleEnd = genPreambleStart + (preamble?.Split('\n').Length ?? 0);

                foreach (var block in tree.GetRoot().DescendantNodes().OfType<BlockSyntax>())
                {
                    var span = tree.GetLineSpan(block.Span);
                    int genStart = span.StartLinePosition.Line;
                    int genEnd   = span.EndLinePosition.Line;

                    if (genStart < genPreambleStart || genStart >= genPreambleEnd) continue;
                    if (genEnd - genStart < 1) continue;

                    int csxStart = (genStart - genPreambleStart) + csxPreambleStart;
                    int csxEnd   = (genEnd   - genPreambleStart) + csxPreambleStart;
                    if (csxStart < 0 || csxEnd >= csxLines.Length) continue;

                    ranges.Add(new { startLine = csxStart, endLine = csxEnd });
                }

                // ── JSX element folding via line scan ─────────────────────────────────
                // Match <TagName at start of non-self-closing tag, then find its </TagName>
                var openStack = new Stack<(string tag, int line)>();
                for (int i = 0; i < csxLines.Length; i++)
                {
                    var ln = csxLines[i].TrimStart();

                    // Self-closing or already-closed tags — skip
                    // Open tag: starts with <Tag (uppercase) and does NOT end with />
                    var openMatch = System.Text.RegularExpressions.Regex.Match(ln,
                        @"^<([A-Z][a-zA-Z0-9]*)[\s>]");
                    if (openMatch.Success && !csxLines[i].TrimEnd().EndsWith("/>"))
                    {
                        openStack.Push((openMatch.Groups[1].Value, i));
                        continue;
                    }

                    // Close tag: </Tag>
                    var closeMatch = System.Text.RegularExpressions.Regex.Match(ln,
                        @"^</([A-Z][a-zA-Z0-9]*)>");
                    if (closeMatch.Success && openStack.Count > 0)
                    {
                        var closeName = closeMatch.Groups[1].Value;
                        // Pop until we find the matching open tag
                        while (openStack.Count > 0)
                        {
                            var (openName, openLine) = openStack.Pop();
                            if (openName == closeName)
                            {
                                if (i - openLine > 1)
                                    ranges.Add(new { startLine = openLine, endLine = i });
                                break;
                            }
                        }
                    }
                }

                return ranges.ToArray();
            }
            catch
            {
                return [];
            }
        }
    }
}
