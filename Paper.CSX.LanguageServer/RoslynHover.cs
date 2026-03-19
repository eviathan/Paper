using System.Text.RegularExpressions;
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

        public static object? GetHover(string csxSrc, string word, int line, int character)
        {
            try
            {
                var (compilation, tree, generatedSrc) = GetOrBuildCompilation(csxSrc);
                var model = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();

                // Try position-aware hover first: map CSX position → generated C# position
                // If position is in JSX area (before preamble), this returns -1 and we fall through
                int genOffset = CsxPositionMapper.ToGeneratedOffset(csxSrc, generatedSrc, line, character);
                
                if (genOffset >= 0)
                {
                    // Find the token at the mapped position
                    var token = root.FindToken(genOffset);
                    
                    // First, check if the token itself is an identifier
                    if (token.IsKind(SyntaxKind.IdentifierToken))
                    {
                        var parent = token.Parent;
                        if (parent != null)
                        {
                            var sym = model.GetSymbolInfo(parent).Symbol
                                ?? model.GetSymbolInfo(parent).CandidateSymbols.FirstOrDefault();
                            
                            if (sym != null && sym.Kind != SymbolKind.ErrorType)
                            {
                                var text = FormatSymbol(sym);
                                if (text != null) return MkHover(text);
                            }

                            var typeInfo = model.GetTypeInfo(parent);
                            if (typeInfo.Type != null && typeInfo.Type.TypeKind != TypeKind.Error)
                            {
                                var display = typeInfo.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                                return MkHover($"```csharp\n{display}\n```");
                            }
                        }
                    }
                    
                    // Also check the token's parent for identifier patterns (e.g., member access)
                    if (token.Parent != null)
                    {
                        // Try member access expressions: foo.Bar - hover on 'Bar'
                        if (token.Parent is MemberAccessExpressionSyntax memberAccess)
                        {
                            var sym = model.GetSymbolInfo(memberAccess).Symbol;
                            if (sym != null && sym.Kind != SymbolKind.ErrorType)
                            {
                                var text = FormatSymbol(sym);
                                if (text != null) return MkHover(text);
                            }
                        }
                        
                        // Try invocation expressions: Method()
                        if (token.Parent is InvocationExpressionSyntax invocation)
                        {
                            var sym = model.GetSymbolInfo(invocation).Symbol;
                            if (sym != null && sym.Kind != SymbolKind.ErrorType)
                            {
                                var text = FormatSymbol(sym);
                                if (text != null) return MkHover(text);
                            }
                        }
                    }
                }

                // Fallback: position-aware identifier search within the preamble area only
                // Find preamble line range in generated source
                int preambleStartLine = RoslynSemanticTokens.FindGenPreambleStart(generatedSrc);
                int preambleEndLine = generatedSrc.Split('\n').Length - 1;
                
                // Search only preamble nodes that match the word
                var preambleNodes = root.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(n => n.Identifier.Text == word)
                    .Where(n => n.Span.Start >= 0); // Could add preamble line filtering here

                ISymbol? bestSymbol = null;
                ITypeSymbol? bestType = null;

                foreach (var node in preambleNodes)
                {
                    var sym = model.GetSymbolInfo(node).Symbol
                        ?? model.GetSymbolInfo(node).CandidateSymbols.FirstOrDefault();
                    if (sym != null && sym.Kind != SymbolKind.ErrorType)
                    {
                        if (sym is IParameterSymbol or ILocalSymbol)
                        {
                            bestSymbol = sym;
                            break;
                        }
                        bestSymbol ??= sym;
                    }

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
            catch (Exception ex)
            {
                // Log errors so we can debug
                Console.Error.WriteLine($"[RoslynHover] Error: {ex.Message}");
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

            // Label — matches VS/Rider style: "(extension) …" for extension methods
            var kind = symbol switch
            {
                ILocalSymbol     => "local variable",
                IParameterSymbol => "parameter",
                IMethodSymbol m when m.IsExtensionMethod || m.MethodKind == MethodKind.ReducedExtension
                                 => "extension",
                IMethodSymbol    => "method",
                IPropertySymbol  => "property",
                IFieldSymbol     => "field",
                ITypeSymbol      => "type",
                _                => symbol.Kind.ToString().ToLower(),
            };

            // Count sibling overloads
            int overloads = 0;
            if (symbol is IMethodSymbol ms)
            {
                var container = ms.ContainingType ?? ms.ReceiverType as INamedTypeSymbol;
                if (container != null)
                    overloads = container.GetMembers(ms.Name).OfType<IMethodSymbol>().Count() - 1;
            }

            var sb = new System.Text.StringBuilder();
            sb.Append($"({kind}) `{code}`");
            if (overloads > 0)
                sb.Append($" (+ {overloads} overload{(overloads > 1 ? "s" : "")})");

            // Render XML documentation: summary, returns, exceptions
            var xml = symbol.GetDocumentationCommentXml();
            AppendXmlDocs(sb, xml);

            return sb.ToString();
        }

        // Resolve <see cref="T:Full.Name"/> → short name, generic backticks → <T>
        private static string ResolveCref(string cref)
        {
            var name = System.Text.RegularExpressions.Regex.Replace(cref, @"^[A-Z]:", "");
            var dot  = name.LastIndexOf('.');
            name = dot >= 0 ? name[(dot + 1)..] : name;
            name = System.Text.RegularExpressions.Regex.Replace(name, @"`\d+", "<T>");
            var paren = name.IndexOf('(');
            if (paren >= 0) name = name[..paren];
            return name;
        }

        private static string CleanXml(string raw)
        {
            const RegexOptions S = RegexOptions.Singleline;
            var s = raw.Trim();
            s = Regex.Replace(s, @"<see\s+cref=""([^""]+)""\s*/>",          m => ResolveCref(m.Groups[1].Value));
            s = Regex.Replace(s, @"<see\s+cref=""([^""]+)""\s*>.*?</see>",  m => ResolveCref(m.Groups[1].Value), S);
            s = Regex.Replace(s, @"<paramref\s+name=""([^""]+)""\s*/>",     m => m.Groups[1].Value);
            s = Regex.Replace(s, @"<typeparamref\s+name=""([^""]+)""\s*/>", m => m.Groups[1].Value);
            s = Regex.Replace(s, @"<[^>]+>", "");
            s = Regex.Replace(s, @"\s+", " ");
            return s.Trim();
        }

        private static void AppendXmlDocs(System.Text.StringBuilder sb, string? xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return;

            const RegexOptions S = RegexOptions.Singleline;

            var summaryM = Regex.Match(xml, @"<summary>(.*?)</summary>", S);
            if (summaryM.Success)
            {
                var summary = CleanXml(summaryM.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(summary))
                    sb.Append("\n\n").Append(summary);
            }

            var returnsM = Regex.Match(xml, @"<returns>(.*?)</returns>", S);
            if (returnsM.Success)
            {
                var ret = CleanXml(returnsM.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(ret))
                    sb.Append("\n\n**Returns:** ").Append(ret);
            }

            // Extract exception type from cref attribute, not inner text
            foreach (Match exM in Regex.Matches(xml, @"<exception\s+cref=""([^""]*)""\s*>(.*?)</exception>", S))
            {
                var exType = ResolveCref(exM.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(exType))
                    sb.Append("\n\n**Throws:** ").Append(exType);
            }
        }

        private static object MkHover(string md) => new
        {
            contents = new { kind = "markdown", value = md },
        };
    }
}