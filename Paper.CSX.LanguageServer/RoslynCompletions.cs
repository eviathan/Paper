using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Paper.CSX.LanguageServer
{
    /// <summary>
    /// Provides position-aware C# completions for the CSX preamble using Roslyn's
    /// <see cref="SemanticModel.LookupSymbols(int)"/>.
    /// </summary>
    internal static class RoslynCompletions
    {
        private static readonly SymbolDisplayFormat _fmt = SymbolDisplayFormat.MinimallyQualifiedFormat;

        /// <summary>
        /// Returns Roslyn completion items for all C# symbols accessible at the given CSX cursor position.
        /// </summary>
        public static object[] GetCompletions(string csxSrc, int csxLine, int csxCol)
        {
            try
            {
                var (compilation, tree, generatedSrc) = RoslynHover.GetOrBuildCompilation(csxSrc);
                var model = compilation.GetSemanticModel(tree);

                var genOffset = CsxPositionMapper.ToGeneratedOffset(csxSrc, generatedSrc, csxLine, csxCol);
                genOffset = Math.Clamp(genOffset, 0, generatedSrc.Length);

                var symbols = model.LookupSymbols(genOffset);
                return FormatSymbols(symbols);
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// Returns Roslyn member completions after "identifier." at the given CSX cursor position.
        /// Delegates to <see cref="RoslynMembers.GetMembers"/> which already handles this well.
        /// </summary>
        public static object[] GetMemberCompletions(string csxSrc, string identifier, int csxLine, int csxCol)
        {
            return RoslynMembers.GetMembers(csxSrc, identifier, 0);
        }

        private static object[] FormatSymbols(ImmutableArray<ISymbol> symbols)
        {
            var items  = new List<object>();
            // Deduplicate by name+kind so overloads don't produce duplicates (except methods which stack)
            var seenMethods = new HashSet<string>(StringComparer.Ordinal);
            var seenOther   = new HashSet<string>(StringComparer.Ordinal);

            foreach (var sym in symbols)
            {
                if (sym.Name.StartsWith('<') || sym.Name.StartsWith('_')) continue; // compiler-generated / internals

                var (insert, detail, kind) = sym switch
                {
                    ILocalSymbol local =>
                        (local.Name,    local.Type.ToDisplayString(_fmt) + " " + local.Name,     6),

                    IParameterSymbol param =>
                        (param.Name,    param.Type.ToDisplayString(_fmt) + " " + param.Name,     6),

                    IMethodSymbol m when m.MethodKind == MethodKind.Ordinary =>
                        (m.Name + "(", MethodSignature(m),                                        2),

                    IPropertySymbol p =>
                        (p.Name,        p.Type.ToDisplayString(_fmt) + " " + p.Name,            10),

                    IFieldSymbol f =>
                        (f.Name,        f.Type.ToDisplayString(_fmt) + " " + f.Name,             5),

                    INamedTypeSymbol t when t.TypeKind == TypeKind.Class =>
                        (t.Name,        "class " + t.ToDisplayString(_fmt),                       7),

                    INamedTypeSymbol t when t.TypeKind == TypeKind.Interface =>
                        (t.Name,        "interface " + t.ToDisplayString(_fmt),                   8),

                    INamedTypeSymbol t when t.TypeKind == TypeKind.Enum =>
                        (t.Name,        "enum " + t.ToDisplayString(_fmt),                       13),

                    INamedTypeSymbol t when t.TypeKind == TypeKind.Struct =>
                        (t.Name,        "struct " + t.ToDisplayString(_fmt),                      7),

                    INamespaceSymbol ns =>
                        (ns.Name,       "namespace " + ns.ToDisplayString(_fmt),                  9),

                    _ => (sym.Name, sym.Kind.ToString(), 6),
                };

                bool isMethod = sym is IMethodSymbol;
                var dedupeKey = sym.Name + ":" + kind;
                if (isMethod)
                {
                    // Allow multiple overloads but deduplicate identical signatures
                    if (!seenMethods.Add(detail)) continue;
                }
                else
                {
                    if (!seenOther.Add(dedupeKey)) continue;
                }

                items.Add(new
                {
                    label          = sym.Name,
                    kind,
                    detail,
                    insertText     = insert,
                    insertTextFormat = 1,
                    sortText       = SortPrefix(sym) + sym.Name,
                });
            }

            return [.. items];
        }

        /// <summary>Sort locals and parameters first, then everything else alphabetically.</summary>
        private static string SortPrefix(ISymbol sym) => sym switch
        {
            ILocalSymbol     => "0",
            IParameterSymbol => "1",
            IFieldSymbol     => "2",
            IPropertySymbol  => "3",
            IMethodSymbol    => "4",
            INamedTypeSymbol => "5",
            INamespaceSymbol => "6",
            _                => "9",
        };

        private static string MethodSignature(IMethodSymbol m)
        {
            var ps  = string.Join(", ", m.Parameters.Select(p =>
                p.Type.ToDisplayString(_fmt) + " " + p.Name));
            return $"{m.ReturnType.ToDisplayString(_fmt)} {m.Name}({ps})";
        }
    }
}
