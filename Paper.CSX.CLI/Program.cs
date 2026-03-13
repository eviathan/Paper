using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using CSharpier.Core.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Paper.CSX;

namespace Paper.CSX.CLI
{
    class Program
    {
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand("Paper CSX Compiler - Compiles CSX files to C#");

            // Parse command
            var parseCommand = new Command("parse", "Parse a single CSX file");
            var fileArgument = new Argument<FileInfo>("file", "The CSX file to parse");
            parseCommand.AddArgument(fileArgument);
            parseCommand.SetHandler((file) => ParseCSXFile(file, null), fileArgument);

            // Build command
            var buildCommand = new Command("build", "Build all CSX files in a directory");
            var directoryArgument = new Argument<DirectoryInfo>("directory", "The directory containing CSX files");
            buildCommand.AddArgument(directoryArgument);
            buildCommand.SetHandler(BuildCSXFiles, directoryArgument);

            // Watch command
            var watchCommand = new Command("watch", "Watch a directory for CSX file changes");
            var watchDirectoryArgument = new Argument<DirectoryInfo>("directory", "The directory to watch for CSX files");
            watchCommand.AddArgument(watchDirectoryArgument);
            watchCommand.SetHandler(WatchCSXFiles, watchDirectoryArgument);

            rootCommand.AddCommand(parseCommand);
            rootCommand.AddCommand(buildCommand);
            rootCommand.AddCommand(watchCommand);

            return rootCommand.Invoke(args);
        }

        /// <summary>Generate a full C# file with namespace, class, and method (not just the parsed body).</summary>
        static string GenerateFullFile(FileInfo file, string content, string? projectRoot = null)
        {
            var fileName = Path.GetFileNameWithoutExtension(file.Name);
            if (string.IsNullOrEmpty(fileName)) fileName = "Component";
            var componentName = char.ToUpper(fileName[0]) + fileName.Substring(1) + "Component";
            var methodName = char.ToUpper(fileName[0]) + fileName.Substring(1);

            var (preamble, jsxContent) = CSXCompiler.ExtractPreambleAndJsx(content);
            var parsedBody = string.IsNullOrWhiteSpace(jsxContent) ? "UI.Fragment()" : CSXCompiler.Parse(jsxContent);

            string ns = "Paper.Generated";
            if (!string.IsNullOrEmpty(projectRoot) && file.DirectoryName != null)
            {
                try
                {
                    var relative = Path.GetRelativePath(projectRoot, file.DirectoryName);
                    if (!string.IsNullOrEmpty(relative) && relative != ".")
                        ns = string.Join(".", relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Where(p => !string.IsNullOrEmpty(p))
                            .Select(p => char.ToUpper(p[0]) + p.Substring(1)));
                }
                catch { /* use default namespace */ }
            }

            var sb = new StringBuilder();
            sb.AppendLine("using Paper.Core.VirtualDom;");
            sb.AppendLine("using Paper.Core.Styles;");
            sb.AppendLine("using Paper.Core.Hooks;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
            sb.AppendLine($"public static partial class {componentName}");
            sb.AppendLine("{");
            sb.AppendLine($"    public static UINode {methodName}(Props props)");
            sb.AppendLine("    {");
            if (!string.IsNullOrEmpty(preamble))
            {
                foreach (var line in preamble.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    sb.AppendLine("        " + line.Trim());
            }
            sb.AppendLine($"        return {parsedBody};");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return FormatWithRoslyn(sb.ToString());
        }

        static string FormatWithRoslyn(string source)
        {
            return CSharpFormatter.FormatAsync(source).GetAwaiter().GetResult().Code;
        }

        static void ParseCSXFile(FileInfo file, string? projectRoot = null)
        {
            if (!file.Exists)
            {
                Console.WriteLine($"Error: File not found '{file.FullName}'");
                return;
            }

            Console.WriteLine($"Parsing: {file.FullName}");
            string content = File.ReadAllText(file.FullName);

            string csharpCode = GenerateFullFile(file, content, projectRoot);

            string outputPath = Path.ChangeExtension(file.FullName, ".generated.cs");
            File.WriteAllText(outputPath, csharpCode);

            Console.WriteLine($"Generated: {outputPath}");
        }

        static void BuildCSXFiles(DirectoryInfo directory)
        {
            if (!directory.Exists)
            {
                Console.WriteLine($"Error: Directory not found '{directory.FullName}'");
                return;
            }

            Console.WriteLine($"Building CSX files in: {directory.FullName}");

            string[] csxFiles = Directory.GetFiles(directory.FullName, "*.csx", SearchOption.AllDirectories);
            Console.WriteLine($"Found {csxFiles.Length} CSX files");

            foreach (string filePath in csxFiles)
            {
                ParseCSXFile(new FileInfo(filePath), directory.FullName);
            }

            Console.WriteLine("Build completed!");
        }

        static void WatchCSXFiles(DirectoryInfo directory)
        {
            if (!directory.Exists)
            {
                Console.WriteLine($"Error: Directory not found '{directory.FullName}'");
                return;
            }

            Console.WriteLine($"Watching CSX files in: {directory.FullName}");

            var watcher = new FileSystemWatcher(directory.FullName)
            {
                Filter = "*.csx",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            watcher.Changed += (sender, e) =>
            {
                Console.WriteLine($"File changed: {e.FullPath}");
                ParseCSXFile(new FileInfo(e.FullPath), directory.FullName);
            };

            watcher.Created += (sender, e) =>
            {
                Console.WriteLine($"File created: {e.FullPath}");
                ParseCSXFile(new FileInfo(e.FullPath), directory.FullName);
            };

            watcher.Deleted += (sender, e) =>
            {
                Console.WriteLine($"File deleted: {e.FullPath}");
                string generatedFile = Path.ChangeExtension(e.FullPath, ".generated.cs");
                if (File.Exists(generatedFile))
                {
                    File.Delete(generatedFile);
                }
            };

            watcher.Renamed += (sender, e) =>
            {
                Console.WriteLine($"File renamed: {e.OldFullPath} -> {e.FullPath}");
                string oldGeneratedFile = Path.ChangeExtension(e.OldFullPath, ".generated.cs");
                if (File.Exists(oldGeneratedFile))
                {
                    File.Delete(oldGeneratedFile);
                }
                ParseCSXFile(new FileInfo(e.FullPath), directory.FullName);
            };

            watcher.EnableRaisingEvents = true;
            Console.WriteLine("Press 'q' to stop watching...");

            while (Console.ReadKey(true).Key != ConsoleKey.Q) { }

            watcher.Dispose();
        }
    }
}