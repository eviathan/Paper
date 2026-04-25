using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Paper.CSX
{
    [Generator]
    public partial class CSXSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // No initialization needed
        }

        public void Execute(GeneratorExecutionContext context)
        {
            foreach (var additionalFile in context.AdditionalFiles)
            {
                if (additionalFile.Path.EndsWith(".csx"))
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(additionalFile.Path);
                        var componentName = char.ToUpper(fileName[0]) + fileName.Substring(1) + "Component";

                        var fileNode = new CSXFileNode
                        {
                            Path = additionalFile.Path,
                            Namespace = GetNamespaceFromFilePath(additionalFile.Path)
                        };

                        var component = new CSXComponent
                        {
                            Name = componentName
                        };

                        component.Methods.Add(new CSXMethod
                        {
                            Name = fileName,
                            Body = additionalFile.GetText()!.ToString()
                        });

                        fileNode.Components.Add(component);

                        var baseDir = Path.GetDirectoryName(additionalFile.Path);
                        var source = GenerateCode(fileNode, baseDir);
                        var generatedFileName = componentName + ".generated.cs";
                        context.AddSource(generatedFileName, SourceText.From(source, Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "CSX001",
                                "CSX Generation Error",
                                $"Failed to generate C# code from {Path.GetFileName(additionalFile.Path)}: {ex.Message}",
                                "CSX",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true
                            ),
                            Location.Create(additionalFile.Path, new TextSpan(0, 0), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 0)))
                        ));
                    }
                }
            }
        }

        private string GetNamespaceFromFilePath(string filePath)
        {
            // Use a simple approach: namespace is based on the directory structure
            // relative to a known root. We'll compute it relative to the solution directory.
            
            // Try to find the solution directory by walking up from the file
            var dir = Path.GetDirectoryName(filePath);
            var solutionRoot = FindSolutionRoot(dir);
            
            if (solutionRoot != null && dir.StartsWith(solutionRoot))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, dir);
                var namespaceParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();
                
                if (namespaceParts.Length > 0)
                {
                    return string.Join(".", namespaceParts.Select(part =>
                        char.ToUpper(part[0]) + part.Substring(1)
                    ));
                }
            }
            
            return "Paper.Generated";
        }

        private string? FindSolutionRoot(string? directory)
        {
            var dir = directory;
            while (dir != null)
            {
                // Check for .sln file
                if (Directory.GetFiles(dir, "*.sln").Any())
                    return dir;
                
                // Check for common framework directory name
                if (dir.EndsWith("Paper", StringComparison.OrdinalIgnoreCase) ||
                    dir.EndsWith("Archipelago.Engine", StringComparison.OrdinalIgnoreCase))
                {
                    // Walk up one more level to get the root
                    var parent = Path.GetDirectoryName(dir);
                    return parent ?? dir;
                }
                
                var parentDir = Path.GetDirectoryName(dir);
                if (parentDir == dir) break;
                dir = parentDir;
            }
            
            return null;
        }

        private string GenerateCode(CSXFileNode fileNode, string? baseDir = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using Paper.Core.Components;");
            sb.AppendLine("using Paper.Core.VirtualDom;");
            sb.AppendLine("using Paper.Core.Styles;");
            sb.AppendLine("using Paper.Core.Hooks;");
            sb.AppendLine("using Paper.Core.Context;");

            // Collect hoisted using directives from all CSX methods before the namespace
            var hoistedUsings = new HashSet<string>();
            var methodsData = new List<(string MethodName, string Preamble, string MethodBody)>();

            foreach (var component in fileNode.Components)
            {
                foreach (var method in component.Methods)
                {
                    var (preamble, csxContent, _, _) = CSXCompiler.ExtractPreambleAndJsx(method.Body, baseDir);
                    if (!string.IsNullOrEmpty(preamble))
                    {
                        var preambleLines = preamble.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in preambleLines)
                        {
                            var trimmedLine = line.Trim();
                            if (trimmedLine.StartsWith("using ") && trimmedLine.EndsWith(";"))
                            {
                                hoistedUsings.Add(trimmedLine);
                            }
                        }
                    }
                    methodsData.Add((method.Name, preamble, csxContent));
                }
            }

            // Write hoisted using directives
            foreach (var usingLine in hoistedUsings.OrderBy(u => u))
            {
                sb.AppendLine(usingLine);
            }
            sb.AppendLine();

            sb.AppendLine($"namespace {fileNode.Namespace};");
            sb.AppendLine();

            // Generate classes and methods
            int methodIndex = 0;
            foreach (var component in fileNode.Components)
            {
                sb.AppendLine($"public static class {component.Name}");
                sb.AppendLine("{");

                for (int i = 0; i < component.Methods.Count; i++)
                {
                    var method = component.Methods[i];
                    var (_, preamble, csxContent) = methodsData[methodIndex++];

                    sb.AppendLine($"    public static UINode {method.Name}(Props props)");
                    sb.AppendLine("    {");

                    if (!string.IsNullOrEmpty(preamble))
                    {
                        var preambleLines = preamble.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in preambleLines)
                        {
                            var trimmedLine = line.Trim();
                            // Skip using directives (already hoisted)
                            if (trimmedLine.StartsWith("using ") && trimmedLine.EndsWith(";"))
                                continue;
                            if (!string.IsNullOrWhiteSpace(trimmedLine))
                                sb.AppendLine("        " + trimmedLine);
                        }
                    }
                    if (!string.IsNullOrEmpty(csxContent))
                    {
                        var parsedBody = CSXCompiler.Parse(csxContent);
                        sb.AppendLine($"        return {parsedBody};");
                    }
                    else
                    {
                        sb.AppendLine("        return UI.Fragment();");
                    }

                    sb.AppendLine("    }");
                    sb.AppendLine();
                }

                sb.AppendLine("}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string? ExtractCSXFromString(string methodBody)
        {
            var cleanBody = methodBody.Trim().TrimEnd(';');

            if (cleanBody.StartsWith("@\""))
            {
                int endIndex = cleanBody.LastIndexOf('\"');
                if (endIndex > 0)
                {
                    return cleanBody.Substring(2, endIndex - 2);
                }
            }

            if (cleanBody.StartsWith("\""))
            {
                return cleanBody.TrimStart('\"').TrimEnd('\"');
            }

            if (cleanBody.Contains('+'))
            {
                var sb = new StringBuilder();
                var parts = cleanBody.Split('+');
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (trimmed.StartsWith("@\""))
                    {
                        int endIndex = trimmed.LastIndexOf('\"');
                        if (endIndex > 0)
                            sb.Append(trimmed.Substring(2, endIndex - 2));
                    }
                    else if (trimmed.StartsWith("\""))
                    {
                        sb.Append(trimmed.TrimStart('\"').TrimEnd('\"'));
                    }
                }
            // Generate the source code as string
            var generatedCode = sb.ToString();
            
            // Format the generated code using Roslyn
            var syntaxTree = CSharpSyntaxTree.ParseText(generatedCode);
            var root = syntaxTree.GetRoot().NormalizeWhitespace();
            generatedCode = root.ToFullString();
            
            return generatedCode;
        }

            return null;
        }
    }
}
