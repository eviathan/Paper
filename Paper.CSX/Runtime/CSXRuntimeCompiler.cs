using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

namespace Paper.CSX.Runtime
{
    public sealed record CSXCompiledComponent(
        Func<Paper.Core.VirtualDom.Props, Paper.Core.VirtualDom.UINode> Render,
        AssemblyLoadContext LoadContext);

    public static class CSXRuntimeCompiler
    {
        private static string FormatSourceCode(string source)
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            using var workspace = new AdhocWorkspace();
            var formattedRoot = Formatter.Format(root, workspace);
            return formattedRoot.ToFullString();
        }

        public static CSXCompiledComponent Compile(string csxSource, string componentClassName = "CSXHotComponent", string? baseDir = null)
        {
            var (preamble, jsxContent, hoistedClasses, _) = CSXCompiler.ExtractPreambleAndJsx(csxSource, baseDir);
            string expr = CSXCompiler.Parse(jsxContent);

            // Extract `using` directives from the preamble and hoist them to file level.
            var preambleLines = preamble.Split('\n');
            var extraUsings = preambleLines
                .Where(l => { var t = l.Trim(); return t.StartsWith("using ") && t.EndsWith(";"); })
                .Select(l => l.Trim())
                .Distinct()
                .ToList();
            preamble = string.Join('\n', preambleLines
                .Where(l => { var t = l.Trim(); return !(t.StartsWith("using ") && t.EndsWith(";")); }));

            string methodBody;
            if (string.IsNullOrWhiteSpace(preamble))
            {
                methodBody = $"return {expr};";
            }
            else
            {
                var indented = string.Join("\n        ", preamble.Split('\n').Select(l => l.Trim()));
                methodBody = indented + "\n        return " + expr + ";";
            }

            string extraUsingsBlock = extraUsings.Count > 0 ? string.Join("\n", extraUsings) + "\n" : "";

            string source = $$"""
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
public static class {{componentClassName}}
{
    public static UINode Render(Props props)
    {
        {{methodBody}}
    }
}
""";

            var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

            // Ensure System.Console (and other runtime assemblies) are loaded so they appear in refs
            _ = typeof(Console).Assembly;

            var refs = new List<MetadataReference>();
            
            // First add any explicitly configured extra references
            lock (_extraReferences)
            {
                foreach (var asm in _extraReferences)
                {
                    if (!asm.IsDynamic && !string.IsNullOrWhiteSpace(asm.Location))
                        refs.Add(MetadataReference.CreateFromFile(asm.Location));
                }
            }
            
            // Then add all loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                if (string.IsNullOrWhiteSpace(asm.Location)) continue;
                refs.Add(MetadataReference.CreateFromFile(asm.Location));
            }

            var compilation = CSharpCompilation.Create(
                assemblyName: "Paper.CSX.Runtime_" + Guid.NewGuid().ToString("N"),
                syntaxTrees: new[] { syntaxTree },
                references: refs,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var peStream = new MemoryStream();
            var emit = compilation.Emit(peStream);
            if (!emit.Success)
            {
                var errors = emit.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString())
                    .ToList();
                var diag = string.Join("\n", errors);
                var errorMsg = diag.Length > 0 ? diag : "Unknown CSX compilation error.";
                throw new Exception(errorMsg);
            }

            peStream.Position = 0;

            var alc = new AssemblyLoadContext("Paper.CSXHotReload_" + Guid.NewGuid().ToString("N"), isCollectible: true);
            var asmLoaded = alc.LoadFromStream(peStream);

            var type = asmLoaded.GetType(componentClassName) ?? throw new Exception("Compiled type not found.");
            var mi = type.GetMethod("Render", BindingFlags.Public | BindingFlags.Static) ?? throw new Exception("Render method not found.");

            var del = (Func<Paper.Core.VirtualDom.Props, Paper.Core.VirtualDom.UINode>)mi.CreateDelegate(
                typeof(Func<Paper.Core.VirtualDom.Props, Paper.Core.VirtualDom.UINode>));

            return new CSXCompiledComponent(del, alc);
        }

        private static readonly List<Assembly> _extraReferences = new();

        public static void AddReferences(params Assembly[] assemblies)
        {
            lock (_extraReferences)
            {
                foreach (var asm in assemblies)
                {
                    if (asm != null && !_extraReferences.Contains(asm))
                        _extraReferences.Add(asm);
                }
            }
        }

        /// <summary>
        /// Simple formatting pass that adds indentation without using Roslyn Formatter.
        /// The Roslyn Formatter has a bug where it fails to properly indent code inside 
        /// lambdas when the opening { is on the same line as the lambda arrow =>.
        /// </summary>
        private static string SimpleFormat(string source)
        {
            var lines = source.Split('\n');
            var result = new List<string>();
            int baseIndent = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    result.Add("");
                    continue;
                }

                // Decrease indent for lines starting with closing braces (including with trailing semicolons/chains)
                if (line.StartsWith("}") || line.StartsWith("});") || line.StartsWith("});"))
                {
                    baseIndent = Math.Max(0, baseIndent - 1);
                }

                // Add the line with current indentation
                result.Add(new string(' ', baseIndent * 4) + line);

                // Increase indent for lines starting with opening braces or ending with {
                if (line.EndsWith("{") || line.EndsWith("=> {"))
                {
                    baseIndent++;
                }
            }

            return string.Join("\n", result);
        }
    }
}

