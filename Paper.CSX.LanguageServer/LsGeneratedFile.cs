using System.Text;
using Paper.CSX;

namespace Paper.CSX.LanguageServer
{
    /// <summary>
    /// Generates and writes the .generated.cs file alongside the .csx source so that
    /// the C# extension (OmniSharp / C# Dev Kit) can provide full language intelligence
    /// (hover, code actions, go-to-definition, etc.) on the generated code.
    /// </summary>
    internal static class LsGeneratedFile
    {
        // Indentation used for the method body in the generated file.
        // Must match PREAMBLE_COL_OFFSET in extension.ts.
        internal const int PreambleIndent = 12;

        /// <summary>
        /// Builds the full C# file content (namespace / class / method wrapper + preamble)
        /// without CSharpier formatting for speed. The structure mirrors what
        /// Paper.CSX.CLI.Program.GenerateFullFile produces.
        /// </summary>
        public static string BuildContent(string csxSrc, string csxFilePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(csxFilePath);
            if (string.IsNullOrEmpty(fileName)) fileName = "Component";
            var componentName = char.ToUpper(fileName[0]) + fileName[1..] + "Component";
            var methodName    = char.ToUpper(fileName[0]) + fileName[1..];

            string preamble;
            string jsxContent;
            string hoistedClasses;
            try
            {
                var baseDir = Path.GetDirectoryName(csxFilePath) ?? "";
                (preamble, jsxContent, hoistedClasses, _) = CSXCompiler.ExtractPreambleAndJsx(csxSrc, baseDir);
            }
            catch
            {
                return "";
            }

            string parsedBody;
            try
            {
                parsedBody = string.IsNullOrWhiteSpace(jsxContent)
                    ? "UI.Fragment()"
                    : CSXCompiler.Parse(jsxContent);
            }
            catch
            {
                parsedBody = "null!";
            }

            // Hoist any `using X.Y;` lines from the preamble to the file-scope using block so the
            // generated file compiles correctly when the CSX author writes using directives.
            // Use StringSplitOptions.None to preserve blank lines — position mapping depends on
            // each CSX preamble line mapping 1:1 to a generated source line.
            var preambleLines = (preamble ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var extraUsings = preambleLines
                .Where(l => { var trimmedLine = l.Trim(); return trimmedLine.StartsWith("using ", StringComparison.Ordinal) && trimmedLine.EndsWith(";"); })
                .Select(l => l.Trim())
                .Distinct()
                .ToList();
            var bodyLines = preambleLines
                .Where(l => { var trimmedLine = l.Trim(); return !(trimmedLine.StartsWith("using ", StringComparison.Ordinal) && trimmedLine.EndsWith(";")); })
                .ToArray();

            // Compute the base indent of the CSX preamble (smallest leading whitespace of any
            // non-blank body line). Relative indentation is preserved by subtracting this and
            // adding PreambleIndent, so continuation lines in multi-line expressions look correct.
            int baseIndent = bodyLines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Length - l.TrimStart().Length)
                .DefaultIfEmpty(0)
                .Min();

            var indent = new string(' ', PreambleIndent);
            var sb = new StringBuilder();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using Paper.Core.Components;");
            sb.AppendLine("using Paper.Core.VirtualDom;");
            sb.AppendLine("using Paper.Core.Styles;");
            sb.AppendLine("using Paper.Core.Hooks;");
            sb.AppendLine("using Paper.Core.Context;");
            foreach (var u in extraUsings)
                sb.AppendLine(u);
            sb.AppendLine();
            sb.AppendLine("namespace Paper.Generated");
            sb.AppendLine("{");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(hoistedClasses))
            {
                foreach (var line in hoistedClasses.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
                    sb.AppendLine(string.IsNullOrWhiteSpace(line) ? "" : "    " + line.Trim());
                sb.AppendLine();
            }
            sb.AppendLine($"    public static partial class {componentName}");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static UINode {methodName}(Props props)");
            sb.AppendLine("        {");
            foreach (var line in bodyLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    sb.AppendLine();                    // preserve blank lines for accurate position mapping
                else
                {
                    int leadWs = line.Length - line.TrimStart().Length;
                    int relativeIndent = Math.Max(0, leadWs - baseIndent);
                    sb.AppendLine(indent + new string(' ', relativeIndent) + line.Trim());
                }
            }
            sb.AppendLine($"{indent}return {parsedBody};");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
