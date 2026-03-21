using System.Linq;
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

            // Load BCL references from the .NET SDK reference pack (not runtime assemblies).
            // Ref-pack DLLs have XML doc files right beside them, so List<T>, string, etc.
            // all get full documentation. Runtime assemblies (System.Private.CoreLib) don't
            // have matching XML files, so GetDocumentationCommentXml() would return empty.
            var refPackDir = FindNetRefPackDir();
            if (refPackDir != null)
            {
                foreach (var dll in Directory.GetFiles(refPackDir, "*.dll"))
                {
                    try
                    {
                        var xml = Path.ChangeExtension(dll, ".xml");
                        DocumentationProvider? docProvider = File.Exists(xml)
                            ? new XmlDocProvider(xml)
                            : null;
                        list.Add(MetadataReference.CreateFromFile(dll,
                            MetadataReferenceProperties.Assembly, docProvider));
                    }
                    catch { /* skip */ }
                }
            }

            // Add Paper assemblies on top (these are not in the ref pack)
            var paperAssemblies = new[]
            {
                typeof(Paper.Core.VirtualDom.UINode).Assembly,
                typeof(Paper.Core.Styles.StyleSheet).Assembly,
                typeof(Paper.Core.Hooks.Hooks).Assembly,
                typeof(Paper.Core.Components.Primitives).Assembly,
                typeof(Paper.CSX.CSXCompiler).Assembly,
            };

            var seenNames = new HashSet<string>(
                list.Select(r => Path.GetFileNameWithoutExtension(r.Display ?? "")),
                StringComparer.OrdinalIgnoreCase);

            foreach (var asm in paperAssemblies.DistinctBy(a => a.Location))
            {
                if (asm.IsDynamic || string.IsNullOrWhiteSpace(asm.Location)) continue;
                if (!seenNames.Add(Path.GetFileNameWithoutExtension(asm.Location))) continue;
                try
                {
                    var xmlPath = Path.ChangeExtension(asm.Location, ".xml");
                    DocumentationProvider? docProvider = File.Exists(xmlPath)
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
        /// Finds the .NET SDK reference-assembly directory for the currently running TFM.
        /// E.g. /usr/local/share/dotnet/packs/Microsoft.NETCore.App.Ref/10.0.x/ref/net10.0/
        /// </summary>
        private static string? FindNetRefPackDir()
        {
            try
            {
                var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment
                    .GetRuntimeDirectory()
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dotnetRoot = Path.GetDirectoryName(
                    Path.GetDirectoryName(Path.GetDirectoryName(runtimeDir)));
                if (dotnetRoot == null) return null;

                var refBaseDir = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
                if (!Directory.Exists(refBaseDir)) return null;

                // Pick the newest version
                foreach (var verDir in Directory.GetDirectories(refBaseDir).OrderByDescending(d => d))
                {
                    var refDir = Path.Combine(verDir, "ref");
                    if (!Directory.Exists(refDir)) continue;
                    // Pick the TFM directory whose name matches the running version (net10.0, net9.0, …)
                    var tfmDirs = Directory.GetDirectories(refDir).OrderByDescending(d => d);
                    foreach (var tfmDir in tfmDirs)
                        if (Directory.GetFiles(tfmDir, "*.dll").Length > 0)
                            return tfmDir;
                }
            }
            catch { /* best effort */ }
            return null;
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

        /// <summary>
        /// Returns member completions for a complex expression (e.g. "list.Where(x => x > 0)").
        /// Builds a sentinel snippet to resolve the expression's type via Roslyn.
        /// </summary>
        public static object[] GetMembersForExpression(string csxSrc, string expression)
        {
            try
            {
                var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(csxSrc);
                if (string.IsNullOrWhiteSpace(preamble)) return [];

                var normalizedPreamble2 = System.Text.RegularExpressions.Regex.Replace(
                    preamble,
                    @"\bvar\s+(\w+)\s*=\s*\[[^\[\]]*\]",
                    "List<object> $1 = new List<object>()");
                normalizedPreamble2 = System.Text.RegularExpressions.Regex.Replace(
                    normalizedPreamble2, @"=\s*\[\s*\]", "= default!");

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
        {{normalizedPreamble2}}
        var __sentinel__ = {{expression}};
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
                var root  = tree.GetRoot();

                // Find the __sentinel__ variable declarator
                var sentinel = root.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax>()
                    .FirstOrDefault(v => v.Identifier.Text == "__sentinel__");
                if (sentinel?.Initializer == null) return [];

                var typeInfo = model.GetTypeInfo(sentinel.Initializer.Value);
                if (typeInfo.Type == null || typeInfo.Type.TypeKind == TypeKind.Error) return [];

                return BuildMemberItems(typeInfo.Type, model, sentinel.SpanStart);
            }
            catch { return []; }
        }

        public static object[] GetMembers(string csxSrc, string identifier, int cursorOffset)
        {
            try
            {
                // Extract preamble (C# code before the JSX return expression)
                var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(csxSrc);
                if (string.IsNullOrWhiteSpace(preamble)) return [];

                // Normalize C# 12 collection expressions so older Roslyn can resolve types.
                // var x = [...] → List<object> x = new List<object>()  (any content)
                // T x = []     → T x = default!                        (empty, explicitly typed)
                var normalizedPreamble = System.Text.RegularExpressions.Regex.Replace(
                    preamble,
                    @"\bvar\s+(\w+)\s*=\s*\[[^\[\]]*\]",
                    "List<object> $1 = new List<object>()");
                normalizedPreamble = System.Text.RegularExpressions.Regex.Replace(
                    normalizedPreamble, @"=\s*\[\s*\]", "= default!");

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
        {{normalizedPreamble}}
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

            // Instance members via LookupSymbols (includes inherited members + may include some extension methods)
            var symbols = model.LookupSymbols(position, container: type, includeReducedExtensionMethods: true);
            foreach (var sym in symbols)
            {
                if (sym.Name.StartsWith('<') || sym.Name.StartsWith('.')) continue;
                if (!seen.Add(sym.Name + sym.Kind)) continue;
                AddItem(items, sym);
            }

            // LookupSymbols doesn't reliably surface LINQ extension methods.
            // Try Roslyn namespace navigation first, then fall back to IEnumerable<T> detection.
            AddExtensionMethods(items, seen, type, model.Compilation, "System.Linq.Enumerable");
            AddExtensionMethods(items, seen, type, model.Compilation, "System.Linq.Queryable");

            // Guaranteed fallback: if the type implements IEnumerable<T>, add LINQ completions
            // directly using the actual runtime type. This works even when Roslyn metadata
            // lookup fails due to version mismatches or type forwarding.
            AddLinqIfEnumerable(items, seen, type);

            return [.. items];
        }

        private static void AddExtensionMethods(
            List<object> items, HashSet<string> seen,
            ITypeSymbol type, Compilation compilation, string typeFqn)
        {
            try
            {
                // GetTypeByMetadataName can miss types due to .NET runtime type forwarding;
                // navigate the global namespace tree as the primary strategy.
                var parts    = typeFqn.Split('.');
                var typeName = parts[^1];
                INamespaceSymbol? ns = compilation.GlobalNamespace;
                for (int i = 0; i < parts.Length - 1 && ns != null; i++)
                    ns = ns.GetNamespaceMembers().FirstOrDefault(n => n.Name == parts[i]);
                var extType = ns?.GetTypeMembers(typeName).FirstOrDefault()
                           ?? compilation.GetTypeByMetadataName(typeFqn);
                if (extType == null) return;

                foreach (var m in extType.GetMembers().OfType<IMethodSymbol>())
                {
                    if (!m.IsExtensionMethod || m.DeclaredAccessibility != Accessibility.Public) continue;
                    var reduced = m.ReduceExtensionMethod(type);
                    if (reduced == null) continue;
                    if (!seen.Add(reduced.Name + reduced.Kind)) continue;

                    items.Add(new
                    {
                        label            = reduced.Name,
                        kind             = 2,
                        detail           = MethodSignature(reduced),
                        insertText       = reduced.Name + "(",
                        insertTextFormat = 1,
                    });
                }
            }
            catch { /* best effort */ }
        }

        /// <summary>
        /// If <paramref name="type"/> implements <c>IEnumerable&lt;T&gt;</c>, adds the common LINQ
        /// extension methods using the actual runtime <see cref="System.Linq.Enumerable"/> type.
        /// This is reliable when Roslyn metadata navigation fails.
        /// </summary>
        private static void AddLinqIfEnumerable(List<object> items, HashSet<string> seen, ITypeSymbol type)
        {
            try
            {
                // Find IEnumerable<T> in the type's interface list
                ITypeSymbol? elementType = null;
                foreach (var iface in type.AllInterfaces)
                {
                    if (iface.Name == "IEnumerable" && iface.TypeArguments.Length == 1)
                    {
                        elementType = iface.TypeArguments[0];
                        break;
                    }
                }
                // Also handle IEnumerable<T> directly
                if (elementType == null && type is INamedTypeSymbol nt
                    && nt.Name == "IEnumerable" && nt.TypeArguments.Length == 1)
                    elementType = nt.TypeArguments[0];

                if (elementType == null) return;

                var T = elementType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

                var methods = new (string name, string detail)[]
                {
                    ("Where",             $"Where(Func<{T}, bool> predicate) → IEnumerable<{T}>"),
                    ("Select",            $"Select<TResult>(Func<{T}, TResult> selector) → IEnumerable<TResult>"),
                    ("SelectMany",        $"SelectMany<TResult>(Func<{T}, IEnumerable<TResult>> selector) → IEnumerable<TResult>"),
                    ("OrderBy",           $"OrderBy<TKey>(Func<{T}, TKey> keySelector) → IOrderedEnumerable<{T}>"),
                    ("OrderByDescending", $"OrderByDescending<TKey>(Func<{T}, TKey> keySelector) → IOrderedEnumerable<{T}>"),
                    ("ThenBy",            $"ThenBy<TKey>(Func<{T}, TKey> keySelector) → IOrderedEnumerable<{T}>"),
                    ("GroupBy",           $"GroupBy<TKey>(Func<{T}, TKey> keySelector) → IEnumerable<IGrouping<TKey,{T}>>"),
                    ("ToList",            $"ToList() → List<{T}>"),
                    ("ToArray",           $"ToArray() → {T}[]"),
                    ("ToDictionary",      $"ToDictionary<TKey>(Func<{T}, TKey> keySelector) → Dictionary<TKey,{T}>"),
                    ("ToHashSet",         $"ToHashSet() → HashSet<{T}>"),
                    ("ToLookup",          $"ToLookup<TKey>(Func<{T}, TKey> keySelector) → ILookup<TKey,{T}>"),
                    ("First",             $"First() → {T}"),
                    ("FirstOrDefault",    $"FirstOrDefault() → {T}?"),
                    ("Last",              $"Last() → {T}"),
                    ("LastOrDefault",     $"LastOrDefault() → {T}?"),
                    ("Single",            $"Single() → {T}"),
                    ("SingleOrDefault",   $"SingleOrDefault() → {T}?"),
                    ("ElementAt",         $"ElementAt(int index) → {T}"),
                    ("ElementAtOrDefault",$"ElementAtOrDefault(int index) → {T}?"),
                    ("Count",             $"Count() → int"),
                    ("LongCount",         $"LongCount() → long"),
                    ("Any",               $"Any() → bool"),
                    ("All",               $"All(Func<{T}, bool> predicate) → bool"),
                    ("Contains",          $"Contains({T} value) → bool"),
                    ("Sum",               $"Sum(Func<{T}, int> selector) → int"),
                    ("Min",               $"Min() → {T}"),
                    ("Max",               $"Max() → {T}"),
                    ("Average",           $"Average(Func<{T}, double> selector) → double"),
                    ("Aggregate",         $"Aggregate(Func<{T},{T},{T}> func) → {T}"),
                    ("Concat",            $"Concat(IEnumerable<{T}> second) → IEnumerable<{T}>"),
                    ("Union",             $"Union(IEnumerable<{T}> second) → IEnumerable<{T}>"),
                    ("Intersect",         $"Intersect(IEnumerable<{T}> second) → IEnumerable<{T}>"),
                    ("Except",            $"Except(IEnumerable<{T}> second) → IEnumerable<{T}>"),
                    ("Distinct",          $"Distinct() → IEnumerable<{T}>"),
                    ("Reverse",           $"Reverse() → IEnumerable<{T}>"),
                    ("Skip",              $"Skip(int count) → IEnumerable<{T}>"),
                    ("SkipWhile",         $"SkipWhile(Func<{T}, bool> predicate) → IEnumerable<{T}>"),
                    ("Take",              $"Take(int count) → IEnumerable<{T}>"),
                    ("TakeWhile",         $"TakeWhile(Func<{T}, bool> predicate) → IEnumerable<{T}>"),
                    ("Zip",               $"Zip<TSecond>(IEnumerable<TSecond> second) → IEnumerable<({T},TSecond)>"),
                    ("DefaultIfEmpty",    $"DefaultIfEmpty() → IEnumerable<{T}>"),
                    ("Cast",              $"Cast<TResult>() → IEnumerable<TResult>"),
                    ("OfType",            $"OfType<TResult>() → IEnumerable<TResult>"),
                    ("AsEnumerable",      $"AsEnumerable() → IEnumerable<{T}>"),
                };

                foreach (var (name, detail) in methods)
                {
                    if (!seen.Add(name + Microsoft.CodeAnalysis.SymbolKind.Method)) continue;
                    items.Add(new
                    {
                        label            = name,
                        kind             = 2,
                        detail,
                        insertText       = name + "(",
                        insertTextFormat = 1,
                    });
                }
            }
            catch { /* best effort */ }
        }

        private static void AddItem(List<object> items, ISymbol sym)
        {
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
                label            = sym.Name,
                kind,
                detail,
                insertText       = insert,
                insertTextFormat = 1,
            });
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