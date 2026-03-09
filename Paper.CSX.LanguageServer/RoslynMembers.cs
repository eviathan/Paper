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
                try { list.Add(MetadataReference.CreateFromFile(asm.Location)); }
                catch { /* skip */ }
            }
            _refs = [.. list];
            return _refs;
        }

        public static object[] GetMembers(string csxSrc, string identifier, int cursorOffset)
        {
            try
            {
                // Extract preamble (C# code before the JSX return expression)
                var (preamble, _) = CSXParser.ExtractPreambleAndJsx(csxSrc);
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

                return BuildMemberItems(type, model);
            }
            catch
            {
                return [];
            }
        }

        private static object[] BuildMemberItems(ITypeSymbol type, SemanticModel model)
        {
            var items = new List<object>();
            // Walk the type hierarchy to collect accessible instance members
            var seen = new HashSet<string>(StringComparer.Ordinal);

            var current = type;
            while (current != null)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member.IsStatic) continue;
                    if (member.DeclaredAccessibility != Accessibility.Public) continue;
                    if (member.Name.StartsWith('.')) continue; // compiler-generated
                    if (!seen.Add(member.Name)) continue;

                    var (insert, detail, kind) = member switch
                    {
                        IMethodSymbol m when m.MethodKind == MethodKind.Ordinary
                            => (m.Name + "(", MethodSignature(m), 2),     // Method
                        IPropertySymbol p
                            => (p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), 10),  // Property
                        IFieldSymbol f
                            => (f.Name, f.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), 5),   // Field
                        IEventSymbol e
                            => (e.Name, "event " + e.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), 23), // Event
                        _ => (member.Name, member.Kind.ToString(), 6),
                    };

                    items.Add(new
                    {
                        label = member.Name,
                        kind,
                        detail,
                        insertText = insert,
                        insertTextFormat = 1,
                    });
                }

                current = current.BaseType;
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