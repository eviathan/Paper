using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paper.CSX.LanguageServer
{
    internal static class RoslynSignatureHelp
    {
        private static readonly SymbolDisplayFormat _fmt = SymbolDisplayFormat.MinimallyQualifiedFormat;

        /// <summary>
        /// Returns a LSP SignatureHelp response object for the method call at the given CSX cursor,
        /// or <c>null</c> if no invocation is found.
        /// </summary>
        public static object? GetSignatureHelp(string csxSrc, int csxLine, int csxCol)
        {
            try
            {
                var (compilation, tree, generatedSrc) = RoslynHover.GetOrBuildCompilation(csxSrc);
                var model = compilation.GetSemanticModel(tree);

                var genOffset = CsxPositionMapper.ToGeneratedOffset(csxSrc, generatedSrc, csxLine, csxCol);
                genOffset = Math.Clamp(genOffset, 0, Math.Max(0, generatedSrc.Length - 1));

                var root  = tree.GetRoot();
                var token = root.FindToken(genOffset);

                // Walk up AST to find the nearest enclosing InvocationExpressionSyntax
                SyntaxNode? node = token.Parent;
                InvocationExpressionSyntax? invocation = null;
                while (node != null)
                {
                    if (node is InvocationExpressionSyntax inv)
                    {
                        invocation = inv;
                        break;
                    }
                    node = node.Parent;
                }

                if (invocation == null) return null;

                // Count argument separators (commas) before cursor to determine the active parameter
                var argList     = invocation.ArgumentList;
                int activeParam = 0;
                foreach (var sep in argList.Arguments.GetSeparators())
                {
                    if (sep.SpanStart < genOffset) activeParam++;
                    else break;
                }

                // Resolve all overloads
                var methodInfo = model.GetSymbolInfo(invocation);
                ISymbol[] candidates = methodInfo.Symbol != null
                    ? [methodInfo.Symbol]
                    : [.. methodInfo.CandidateSymbols];

                var signatures = candidates
                    .OfType<IMethodSymbol>()
                    .Select(m => BuildSignatureInfo(m, activeParam))
                    .ToArray();

                if (signatures.Length == 0) return null;

                // Pick the best-matching overload based on argument count
                int bestIdx = 0;
                int argCount = argList.Arguments.Count;
                for (int i = 0; i < signatures.Length; i++)
                {
                    var m = candidates.OfType<IMethodSymbol>().ElementAt(i);
                    if (m.Parameters.Length >= argCount)
                    {
                        bestIdx = i;
                        break;
                    }
                }

                return new
                {
                    signatures,
                    activeSignature = bestIdx,
                    activeParameter = activeParam,
                };
            }
            catch
            {
                return null;
            }
        }

        private static object BuildSignatureInfo(IMethodSymbol m, int activeParam)
        {
            var paramLabels = m.Parameters.Select(p =>
                p.Type.ToDisplayString(_fmt) + " " + p.Name).ToArray();

            var label      = $"{m.ReturnType.ToDisplayString(_fmt)} {m.Name}({string.Join(", ", paramLabels)})";
            var parameters = paramLabels.Select(pl => (object)new { label = pl }).ToArray();

            // Include XML documentation summary
            var xml = m.GetDocumentationCommentXml();
            if (!string.IsNullOrWhiteSpace(xml))
            {
                var match = Regex.Match(xml, @"<summary>(.*?)</summary>", RegexOptions.Singleline);
                if (match.Success)
                {
                    var summary = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(summary))
                        return new { label, parameters, documentation = new { kind = "markdown", value = summary } };
                }
            }

            return new { label, parameters };
        }
    }
}
