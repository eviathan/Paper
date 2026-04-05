using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Paper.CSX.LanguageServer
{
    public static class RoslynDiagnostics
    {
        private const int PreambleColOffset = 8;  // 2 × 4-space indent inside the in-memory generated method

        // Matches the props-injection line added by ExtractPreambleAndJsx for typed-props components.
        // e.g. `var props = props.As<AppProps>();`  These lines don't exist in the .csx source.
        private static readonly System.Text.RegularExpressions.Regex InjectedPropsLine =
            new System.Text.RegularExpressions.Regex(@"^var \w+ = props\.As<[^>]+>\(\);$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // Matches any line that contains a UINode function declaration.
        private static readonly System.Text.RegularExpressions.Regex UINodeFuncDecl =
            new System.Text.RegularExpressions.Regex(@"\bUINode\b",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        public static IEnumerable<object> Compile(string csxSrc)
        {
            try
            {
                var csxLines = csxSrc.Split('\n');
                int importLines = csxLines.TakeWhile(l => l.TrimStart().StartsWith("@import")).Count();

                var (preamble, jsxRaw, hoistedClasses, _) = CSXCompiler.ExtractPreambleAndJsx(csxSrc);
                if (string.IsNullOrWhiteSpace(preamble)) return [];

                // Find where the preamble actually starts in the .csx file.
                // importLines only covers @import lines; the real preamble comes after blank lines
                // and the `UINode Foo() {` function declaration.
                int csxPreambleStartLine = importLines; // safe fallback
                {
                    int lastFuncLine = -1;
                    for (int i = importLines; i < csxLines.Length; i++)
                        if (UINodeFuncDecl.IsMatch(csxLines[i])) lastFuncLine = i;

                    if (lastFuncLine >= 0)
                    {
                        // Opening brace may be on the declaration line or the next line
                        for (int i = lastFuncLine; i < Math.Min(lastFuncLine + 4, csxLines.Length); i++)
                        {
                            if (csxLines[i].Contains('{')) { csxPreambleStartLine = i + 1; break; }
                        }
                    }
                }

                // Count injected lines at the top of the generated preamble that have no
                // counterpart in the .csx source (e.g. `var props = props.As<AppProps>();`)
                int injectedPreambleLines = preamble.Split('\n')
                    .TakeWhile(l => InjectedPropsLine.IsMatch(l.Trim()))
                    .Count();

                // Hoist namespace `using` directives to file scope (they can't live inside a method body)
                var preambleLines = preamble.Split('\n');
                var extraUsings = preambleLines
                    .Where(l => { var trimmedLine = l.Trim(); return trimmedLine.StartsWith("using ") && trimmedLine.EndsWith(";"); })
                    .Select(l => l.Trim()).Distinct().ToList();
                preamble = string.Join('\n', preambleLines
                    .Where(l => { var trimmedLine = l.Trim(); return !(trimmedLine.StartsWith("using ") && trimmedLine.EndsWith(";")); }));
                string extraUsingsBlock = extraUsings.Count > 0 ? string.Join("\n", extraUsings) + "\n" : "";

                // Use a placeholder return so we can compile even with JSX present
                string returnExpr;
                try { returnExpr = CSXCompiler.Parse(jsxRaw); }
                catch { returnExpr = "null!"; }

                var indented = string.Join("\n        ", preamble.Split('\n').Select(l => l.Trim()));
                var methodBody = indented + "\n        return " + returnExpr + ";";

                // Source layout (0-indexed lines):
                //   0-7   : 8 base `using` statements
                //   8+    : extraUsingsBlock lines (each extraUsing adds 1 line)
                //   next  : hoistedClasses lines (count `\n` chars — avoids overcounting trailing newline)
                //   +4    : `public static class` + `{` + method signature + `{`
                //   next  : preamble starts here
                const int fixedHeaderLines = 8; // the 8 base usings only — no implicit blank line
                int extraUsingLines = extraUsings.Count;
                int hoistedLines = hoistedClasses.Count(c => c == '\n');
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

                // Regex to detect errors where the problematic type is an unresolved generic
                // type parameter ('T', 'T1', 'T2', ...).  These are cascade false-positives:
                // Roslyn 4.10 sometimes fails to infer T in `var (a, b, _) = UseState<T>(x)`
                // and then reports downstream errors with bare 'T' rather than the real type.
                // Real user errors always reference concrete types ('int', 'string', etc.).
                var unresolvedTypeParam = new System.Text.RegularExpressions.Regex(
                    @"'T\d*'", System.Text.RegularExpressions.RegexOptions.Compiled);

                return compilation.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error
                             && !suppressedIds.Contains(d.Id)
                             && !unresolvedTypeParam.IsMatch(d.GetMessage()))
                    .Select(d =>
                    {
                        var span = d.Location.GetLineSpan();
                        int genLine = span.StartLinePosition.Line;
                        int genCol = span.StartLinePosition.Character;

                        // Only report errors inside the preamble (not in template wrapper or JSX)
                        if (genLine < preambleLineOffset) return null;
                        if (genLine >= preambleLineOffset + preambleLineCount) return null;

                        // Map generated-code line back to .csx line.
                        // Subtract injected lines that have no .csx counterpart.
                        int csxLine = genLine - preambleLineOffset - injectedPreambleLines + csxPreambleStartLine;
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