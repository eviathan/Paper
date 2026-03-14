using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paper.CSX.LanguageServer
{
    internal static class RoslynHover
    {
        // Cache the last compilation so repeated hovers on the same file are fast.
        // Keyed by the CSX source text hash; evicted when source changes.
        private static string? _cachedHash;
        private static (CSharpCompilation compilation, SyntaxTree tree, string generatedSrc)? _cached;
        private static readonly object _lock = new();

        public static object? GetHover(string csxSrc, string word)
        {
            try
            {
                var (compilation, tree, _) = GetOrBuildCompilation(csxSrc);
                var model = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();

                // Walk every identifier node whose text matches the hovered word.
                // Collect the best symbol/type found across all occurrences — the
                // correct occurrence is usually the one with the most specific info.
                ISymbol? bestSymbol = null;
                ITypeSymbol? bestType = null;

                foreach (var node in root.DescendantNodes().OfType<IdentifierNameSyntax>()
                            .Where(n => n.Identifier.Text == word))
                {
                    // Prefer a concrete symbol (local, parameter, method, property…)
                    var sym = model.GetSymbolInfo(node).Symbol
                        ?? model.GetSymbolInfo(node).CandidateSymbols.FirstOrDefault();
                    if (sym != null && sym.Kind != SymbolKind.ErrorType)
                    {
                        // Parameters and locals are the most precise match — stop immediately.
                        if (sym is IParameterSymbol or ILocalSymbol)
                        {
                            bestSymbol = sym;
                            break;
                        }
                        bestSymbol ??= sym;
                    }

                    // Fall back to type inference
                    var t = model.GetTypeInfo(node).Type;
                    if (t != null && t.TypeKind != TypeKind.Error)
                        bestType ??= t;
                }

                if (bestSymbol != null)
                {
                    var text = FormatSymbol(bestSymbol);
                    if (text != null) return MkHover(text);
                }

                if (bestType != null)
                {
                    var display = bestType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    return MkHover($"```csharp\n{display}\n```");
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // ── Compilation cache ─────────────────────────────────────────────────────

        internal static (CSharpCompilation compilation, SyntaxTree tree, string generatedSrc) GetOrBuildCompilation(string csxSrc)
        {
            // Simple hash: length + checksum of first 512 chars + last 512 chars
            var hash = ComputeHash(csxSrc);
            lock (_lock)
            {
                if (_cachedHash == hash && _cached.HasValue)
                    return _cached.Value;
            }

            var built = BuildCompilation(csxSrc);

            lock (_lock)
            {
                _cachedHash = hash;
                _cached = built;
            }
            return built;
        }

        private static string ComputeHash(string s)
        {
            // Fast non-cryptographic hash sufficient for cache invalidation
            ulong h = 14695981039346656037UL;
            foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
            return h.ToString("x16");
        }

        private static (CSharpCompilation, SyntaxTree, string) BuildCompilation(string csxSrc)
        {
            var (preamble, jsxRaw, hoistedClasses, _) = CSXCompiler.ExtractPreambleAndJsx(csxSrc);

            // Hoist namespace `using` directives to file scope (they can't live inside a method body)
            var preambleLines = preamble.Split('\n');
            var extraUsings = preambleLines
                .Where(l => { var t = l.Trim(); return t.StartsWith("using ") && t.EndsWith(";"); })
                .Select(l => l.Trim())
                .Distinct()
                .ToList();
            preamble = string.Join('\n', preambleLines
                .Where(l => { var t = l.Trim(); return !(t.StartsWith("using ") && t.EndsWith(";")); }));
            string extraUsingsBlock = extraUsings.Count > 0 ? string.Join("\n", extraUsings) + "\n" : "";

            // If there is no JSX return expression, wrap an empty body
            string returnExpr;
            try { returnExpr = string.IsNullOrWhiteSpace(jsxRaw) ? "null!" : CSXCompiler.Parse(jsxRaw); }
            catch { returnExpr = "null!"; }

            // Re-indent preamble the same way RoslynDiagnostics does
            var indented = string.Join("\n        ", (preamble ?? "").Split('\n').Select(l => l.Trim()));
            var methodBody = indented + "\n        return " + returnExpr + ";";

            var source = $$"""
using System;
using System.Collections.Generic;
using System.Linq;
using Paper.Core.VirtualDom;
using Paper.Core.Styles;
using Paper.Core.Hooks;
using Paper.Core.Context;
using Paper.Core.Components;
{{extraUsingsBlock}}
{{hoistedClasses}}
public static class _LsHover_
{
    public static UINode Render(Props props)
    {
        {{methodBody}}
    }
}
""";

            var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
            var compilation = CSharpCompilation.Create(
                "_LS_Hover_" + Guid.NewGuid().ToString("N"),
                [tree],
                RoslynMembers.GetRefs(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable));

            return (compilation, tree, source);
        }

        // ── Symbol formatting ─────────────────────────────────────────────────────

        private static readonly SymbolDisplayFormat _fmt =
            SymbolDisplayFormat.MinimallyQualifiedFormat;

        private static string? FormatSymbol(ISymbol symbol)
        {
            var code = symbol switch
            {
                ILocalSymbol local =>
                    $"{local.Type.ToDisplayString(_fmt)} {local.Name}",

                IParameterSymbol param =>
                    $"{param.Type.ToDisplayString(_fmt)} {param.Name}",

                IMethodSymbol method =>
                    $"{method.ReturnType.ToDisplayString(_fmt)} {method.Name}" +
                    $"({string.Join(", ", method.Parameters.Select(p => p.Type.ToDisplayString(_fmt) + " " + p.Name))})",

                IPropertySymbol prop =>
                    $"{prop.Type.ToDisplayString(_fmt)} {prop.Name}",

                IFieldSymbol field =>
                    $"{field.Type.ToDisplayString(_fmt)} {field.Name}",

                ITypeSymbol type =>
                    type.ToDisplayString(_fmt),

                _ => null
            };

            if (code == null) return null;

            // Label — matches VS/Rider style: "(local variable) int i"
            var kind = symbol switch
            {
                ILocalSymbol => "local variable",
                IParameterSymbol => "parameter",
                IMethodSymbol => "method",
                IPropertySymbol => "property",
                IFieldSymbol => "field",
                ITypeSymbol => "type",
                _ => symbol.Kind.ToString().ToLower(),
            };

            var sb = new System.Text.StringBuilder();
            sb.Append($"({kind}) `{code}`");

            // Append XML documentation summary if available
            var xml = symbol.GetDocumentationCommentXml();
            if (!string.IsNullOrWhiteSpace(xml))
            {
                var m = System.Text.RegularExpressions.Regex.Match(xml,
                    @"<summary>(.*?)</summary>",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                if (m.Success)
                {
                    var summary = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(summary))
                        sb.Append("\n\n").Append(summary);
                }
            }

            return sb.ToString();
        }

        private static object MkHover(string md) => new
        {
            contents = new { kind = "markdown", value = md },
        };
    }
}