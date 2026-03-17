using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Paper.CSX.LanguageServer
{
    internal static class RoslynDiagnostics
    {
        // Number of lines before the preamble in the generated C# source template
        // (8 usings + blank + class + { + method sig + { = 13 lines, preamble starts at line 13)
        private const int PreambleLineOffset = 13;
        private const int PreambleColOffset = 8;  // 2 × 4-space indent inside the method

        public static IEnumerable<object> Compile(string csxSrc)
        {
            try
            {
                // Count @import lines stripped from CSX so we can adjust line numbers
                var csxLines = csxSrc.Split('\n');
                int importLines = csxLines.TakeWhile(l => l.TrimStart().StartsWith("@import")).Count();

                var (preamble, jsxRaw, _, _) = CSXCompiler.ExtractPreambleAndJsx(csxSrc);
                if (string.IsNullOrWhiteSpace(preamble)) return [];

                // Use a placeholder return so we can compile even with JSX present
                string returnExpr;
                try { returnExpr = CSXCompiler.Parse(jsxRaw); }
                catch { returnExpr = "null!"; }

                var indented = string.Join("\n        ", preamble.Split('\n').Select(l => l.Trim()));
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

public static class _LsDiag_
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
                        if (genLine < PreambleLineOffset) return null;
                        if (genLine >= PreambleLineOffset + preambleLineCount) return null;

                        int csxLine = genLine - PreambleLineOffset + importLines;
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