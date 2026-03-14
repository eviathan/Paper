using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paper.CSX.LanguageServer
{
    internal static class RoslynMembers
    {
        // Cache the compilation references — expensive to build every keystroke
        private static MetadataReference[]? _refs;

        internal static MetadataReference[] GetRefs()
        {
            if (_refs != null) return _refs;
            var list = new List<MetadataReference>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                if (string.IsNullOrWhiteSpace(asm.Location)) continue;
                try
                {
                    var xmlPath = FindXmlDoc(asm.Location);
                    DocumentationProvider? docProvider = xmlPath != null
                        ? new XmlDocProvider(xmlPath)
                        : null;
                    list.Add(MetadataReference.CreateFromFile(asm.Location,
                        MetadataReferenceProperties.Assembly, docProvider));
                }
                catch { /* skip */ }
            }
            _refs = [.. list];
            return _refs;
        }

        /// <summary>
        /// Looks for the XML documentation file for an assembly DLL.
        /// Checks the DLL directory first, then the .NET SDK reference assembly packs.
        /// </summary>
        private static string? FindXmlDoc(string dllPath)
        {
            // 1. Same directory as the DLL
            var xmlSameDir = Path.ChangeExtension(dllPath, ".xml");
            if (File.Exists(xmlSameDir)) return xmlSameDir;

            // 2. .NET SDK reference assembly packs
            //    RuntimeDirectory is e.g. /usr/local/share/dotnet/shared/Microsoft.NETCore.App/10.0.x/
            //    We walk up to the dotnet root and search packs/Microsoft.NETCore.App.Ref/*/ref/net*/
            try
            {
                // TrimEnd removes the trailing separator so GetDirectoryName walks up correctly
                var runtimeDir  = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()
                                      .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dotnetRoot  = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(runtimeDir)));
                if (dotnetRoot == null) return null;

                var refBaseDir  = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
                if (!Directory.Exists(refBaseDir)) return null;

                var fileName    = Path.GetFileNameWithoutExtension(dllPath) + ".xml";
                // Try the newest version first
                foreach (var verDir in Directory.GetDirectories(refBaseDir).OrderByDescending(d => d))
                {
                    var refDir = Path.Combine(verDir, "ref");
                    if (!Directory.Exists(refDir)) continue;
                    foreach (var tfmDir in Directory.GetDirectories(refDir))
                    {
                        var candidate = Path.Combine(tfmDir, fileName);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { /* best effort */ }

            return null;
        }

        public static object[] GetMembers(string csxSrc, string identifier, int cursorOffset)
        {
            try
            {
                // Extract preamble (C# code before the JSX return expression)
                var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(csxSrc);
                if (string.IsNullOrWhiteSpace(preamble)) return [];

                // Build a compilable snippet that includes the preamble and a
                // sentinel call to help locate the identifier's type
                var source = $$"""
using System;
using System.Collections.Generic;
using System.Linq;
using Paper.Core.VirtualDom;
using Paper.Core.Styles;
using Paper.Core.Hooks;
using Paper.Core.Context;

public static class _LsCompletion_
{
    public static void Render(Props props)
    {
        {{preamble}}
        _ = {{identifier}};
    }
}
""";

                var tree = CSharpSyntaxTree.ParseText(source,
                    new CSharpParseOptions(LanguageVersion.Preview));

                var compilation = CSharpCompilation.Create(
                    "_LS_" + Guid.NewGuid().ToString("N"),
                    [tree],
                    GetRefs(),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                        nullableContextOptions: NullableContextOptions.Enable));

                var model = compilation.GetSemanticModel(tree);

                // Find the sentinel `_ = identifier;` IdentifierNameSyntax
                var root = tree.GetRoot();
                var identNodes = root.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(n => n.Identifier.Text == identifier)
                    .ToList();

                ITypeSymbol? type = null;
                foreach (var node in identNodes)
                {
                    var info = model.GetTypeInfo(node);
                    if (info.Type != null && info.Type.TypeKind != TypeKind.Error)
                    {
                        type = info.Type;
                        break;
                    }
                }

                if (type == null) return [];

                // Find the position of the sentinel identifier in the generated source
                int symbolPosition = identNodes.FirstOrDefault()?.SpanStart ?? 0;

                return BuildMemberItems(type, model, symbolPosition);
            }
            catch
            {
                return [];
            }
        }

        private static object[] BuildMemberItems(ITypeSymbol type, SemanticModel model, int position)
        {
            var items = new List<object>();
            var seen  = new HashSet<string>(StringComparer.Ordinal);

            // LookupSymbols with a container returns both declared instance members AND
            // applicable extension methods in scope (e.g. LINQ) — GetMembers() alone misses the latter.
            var symbols = model.LookupSymbols(position, container: type, includeReducedExtensionMethods: true);

            foreach (var sym in symbols)
            {
                if (sym.Name.StartsWith('<') || sym.Name.StartsWith('.')) continue;
                if (!seen.Add(sym.Name + sym.Kind)) continue;

                var (insert, detail, kind) = sym switch
                {
                    IMethodSymbol m when m.MethodKind is MethodKind.Ordinary or MethodKind.ReducedExtension
                        => (m.Name + "(", MethodSignature(m), 2),
                    IPropertySymbol p
                        => (p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), 10),
                    IFieldSymbol f
                        => (f.Name, f.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), 5),
                    IEventSymbol e
                        => (e.Name, "event " + e.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), 23),
                    _ => (sym.Name, sym.Kind.ToString(), 6),
                };

                items.Add(new
                {
                    label          = sym.Name,
                    kind,
                    detail,
                    insertText     = insert,
                    insertTextFormat = 1,
                });
            }

            return [.. items];
        }

        private static string MethodSignature(IMethodSymbol m)
        {
            var ps = string.Join(", ", m.Parameters.Select(p =>
                p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + " " + p.Name));
            var ret = m.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            return $"{ret} {m.Name}({ps})";
        }
    }
}