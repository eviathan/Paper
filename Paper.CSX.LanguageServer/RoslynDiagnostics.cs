using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Paper.CSX.LanguageServer
{
    internal static class RoslynDiagnostics
    {
        private const int PreambleColOffset = 8;  // 2 × 4-space indent inside the method

        public static IEnumerable<object> Compile(string csxSrc)
        {
            try
            {
                // Count @import lines stripped from CSX so we can adjust line numbers
                var csxLines = csxSrc.Split('\n');
                int importLines = csxLines.TakeWhile(l => l.TrimStart().StartsWith("@import")).Count();

                var (preamble, jsxRaw, hoistedClasses, _) = CSXCompiler.ExtractPreambleAndJsx(csxSrc);
                if (string.IsNullOrWhiteSpace(preamble)) return [];

                // Hoist namespace `using` directives to file scope (they can't live inside a method body)
                var preambleLines = preamble.Split('\n');
                var extraUsings = preambleLines
                    .Where(l => { var t = l.Trim(); return t.StartsWith("using ") && t.EndsWith(";"); })
                    .Select(l => l.Trim()).Distinct().ToList();
                preamble = string.Join('\n', preambleLines
                    .Where(l => { var t = l.Trim(); return !(t.StartsWith("using ") && t.EndsWith(";")); }));
                string extraUsingsBlock = extraUsings.Count > 0 ? string.Join("\n", extraUsings) + "\n" : "";

                // Use a placeholder return so we can compile even with JSX present
                string returnExpr;
                try { returnExpr = CSXCompiler.Parse(jsxRaw); }
                catch { returnExpr = "null!"; }

                var indented = string.Join("\n        ", preamble.Split('\n').Select(l => l.Trim()));
                var methodBody = indented + "\n        return " + returnExpr + ";";

                // Fixed header lines: 8 base usings + blank = 9
                const int fixedHeaderLines = 9;
                int extraUsingLines = extraUsings.Count;
                int hoistedLines = string.IsNullOrEmpty(hoistedClasses) ? 0 : hoistedClasses.Split('\n').Length;
                // class decl + { + method sig + { = 4 more lines
                int preambleLineOffset = fixedHeaderLines + extraUsingLines + hoistedLines + 4;

                var source = $$"""
using System;
using System.Collections.Generic;
using System.Linq;
using Paper.Core.VirtualDom;
using Paper.Core.Styles;
using Paper.Core.Hooks;
using Paper.Core.Context;
using Paper.Core.Components;
{{extraUsingsBlock}}{{hoistedClasses}}public static class _LsDiag_
{
    public static UINode Render(Props props)
    {
        {{methodBody}}
    }
}
""";

                var tree = CSharpSyntaxTree.ParseText(source,
                    new CSharpParseOptions(LanguageVersion.Preview));

                var compilation = CSharpCompilation.Create(
                    "_LS_Diag_" + Guid.NewGuid().ToString("N"),
                    [tree],
                    RoslynMembers.GetRefs(),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                        nullableContextOptions: NullableContextOptions.Enable));

                int preambleLineCount = preamble.Split('\n').Length;

                // Diagnostics suppressed because the language-server Roslyn version may be
                // older than what the project actually compiles with, causing false positives.
                var suppressedIds = new HashSet<string>(StringComparer.Ordinal)
                {
                    "CS9176", // 'No target type' for collection expressions (C# 12 feature Roslyn may not know)
                };

                return compilation.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error && !suppressedIds.Contains(d.Id))
                    .Select(d =>
                    {
                        var span = d.Location.GetLineSpan();
                        int genLine = span.StartLinePosition.Line;
                        int genCol = span.StartLinePosition.Character;

                        // Only report errors inside the preamble (not in template wrapper or JSX)
                        if (genLine < preambleLineOffset) return null;
                        if (genLine >= preambleLineOffset + preambleLineCount) return null;

                        int csxLine = genLine - preambleLineOffset + importLines;
                        int csxCol = Math.Max(0, genCol - PreambleColOffset);

                        return (object?)new
                        {
                            range = new
                            {
                                start = new { line = csxLine, character = csxCol },
                                end = new { line = csxLine, character = csxCol + 1 },
                            },
                            severity = 1,
                            source = "paper-csx",
                            code = d.Id,
                            message = d.GetMessage(),
                        };
                    })
                    .Where(d => d != null)
                    .Cast<object>();
            }
            catch
            {
                return [];
            }
        }
    }
}