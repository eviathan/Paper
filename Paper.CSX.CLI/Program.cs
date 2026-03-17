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

            // Build with config command
            var buildConfigCommand = new Command("build:config", "Build using paper.json configuration");
            buildConfigCommand.SetHandler(BuildWithConfig);
            rootCommand.AddCommand(build_config: buildConfigCommand);

            // Watch command
            var watchCommand = new Command("watch", "Watch a directory for CSX file changes");
            var watchDirectoryArgument = new Argument<DirectoryInfo>("directory", "The directory to watch for CSX files");
            watchCommand.AddArgument(watchDirectoryArgument);
            watchCommand.SetHandler(WatchCSXFiles, watchDirectoryArgument);

            // Watch with config command
            var watchConfigCommand = new Command("watch:config", "Watch using paper.json configuration");
            watchConfigCommand.SetHandler(WatchWithConfig);
            rootCommand.AddCommand(watchConfigCommand);

            // Init config command
            var initCommand = new Command("init", "Create a default paper.json configuration file");
            var initDirectoryArgument = new Argument<DirectoryInfo?>("directory", "Directory to create config in (default: current)");
            initCommand.AddArgument(initDirectoryArgument);
            initCommand.SetHandler((dir) => ConfigLoader.CreateDefault(dir?.FullName), initDirectoryArgument);

            rootCommand.AddCommand(parseCommand);
            rootCommand.AddCommand(buildCommand);
            rootCommand.AddCommand(watchCommand);
            rootCommand.AddCommand(initCommand);

            return rootCommand.Invoke(args);
        }

        /// <summary>
        /// Validates that a path stays within the base directory (prevents path traversal attacks).
        /// Returns true if the path is safe, false otherwise.
        /// </summary>
        static bool IsPathSafe(string basePath, string targetPath)
        {
            try
            {
                var baseDir = Path.GetFullPath(basePath);
                var fullPath = Path.GetFullPath(targetPath);
                return fullPath.StartsWith(baseDir + Path.DirectorySeparatorChar) || fullPath == baseDir;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates a file path to ensure it's within the allowed directory and has a safe extension.
        /// </summary>
        static (bool IsValid, string? Error) ValidateFilePath(FileInfo file, string? baseDir = null)
        {
            if (!file.Exists)
                return (false, $"File not found: {file.FullName}");

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(file.FullName);
            }
            catch (Exception ex)
            {
                return (false, $"Invalid file path: {ex.Message}");
            }

            // Check extension
            var ext = Path.GetExtension(file.FullName).ToLowerInvariant();
            if (ext != ".csx")
                return (false, $"Invalid file extension '{ext}'. Only .csx files are allowed.");

            // Check path traversal if base directory is specified
            if (!string.IsNullOrEmpty(baseDir))
            {
                if (!IsPathSafe(baseDir, fullPath))
                    return (false, $"Path traversal detected: {file.FullName} is outside the base directory.");
            }

            return (true, null);
        }

        /// <summary>
        /// Validates a directory path.
        /// </summary>
        static (bool IsValid, string? Error) ValidateDirectoryPath(DirectoryInfo dir)
        {
            if (!dir.Exists)
                return (false, $"Directory not found: {dir.FullName}");

            try
            {
                _ = Path.GetFullPath(dir.FullName);
            }
            catch (Exception ex)
            {
                return (false, $"Invalid directory path: {ex.Message}");
            }

            return (true, null);
        }

        /// <summary>Generate a full C# file with namespace, class, and method (not just the parsed body).</summary>
        static string GenerateFullFile(FileInfo file, string content, string? projectRoot = null)
        {
            var fileName = Path.GetFileNameWithoutExtension(file.Name);
            if (string.IsNullOrEmpty(fileName)) fileName = "Component";
            var componentName = char.ToUpper(fileName[0]) + fileName.Substring(1) + "Component";
            var methodName = char.ToUpper(fileName[0]) + fileName.Substring(1);

            var (preamble, jsxContent, _, _) = CSXCompiler.ExtractPreambleAndJsx(content);
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
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using Paper.Core.Components;");
            sb.AppendLine("using Paper.Core.VirtualDom;");
            sb.AppendLine("using Paper.Core.Styles;");
            sb.AppendLine("using Paper.Core.Hooks;");
            sb.AppendLine("using Paper.Core.Context;");
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
            var (isValid, error) = ValidateFilePath(file, projectRoot);
            if (!isValid)
            {
                Console.WriteLine($"Error: {error}");
                return;
            }

            Console.WriteLine($"Parsing: {file.FullName}");
            
            // Read with size limit to prevent DoS
            long fileSize = file.Length;
            if (fileSize > 10 * 1024 * 1024) // 10MB limit
            {
                Console.WriteLine($"Error: File too large ({fileSize / 1024 / 1024}MB). Maximum size is 10MB.");
                return;
            }
            
            string content = File.ReadAllText(file.FullName);
            if (content.Length > 1_000_000) // 1MB of text
            {
                Console.WriteLine($"Error: File content too large ({content.Length / 1024}KB). Maximum is 1MB.");
                return;
            }

            string csharpCode = GenerateFullFile(file, content, projectRoot);

            string outputPath = Path.ChangeExtension(file.FullName, ".generated.cs");
            File.WriteAllText(outputPath, csharpCode);

            Console.WriteLine($"Generated: {outputPath}");
        }

        static void BuildCSXFiles(DirectoryInfo directory)
        {
            var (isValid, error) = ValidateDirectoryPath(directory);
            if (!isValid)
            {
                Console.WriteLine($"Error: {error}");
                return;
            }

            Console.WriteLine($"Building CSX files in: {directory.FullName}");

            string[] csxFiles = Directory.GetFiles(directory.FullName, "*.csx", SearchOption.AllDirectories);
            Console.WriteLine($"Found {csxFiles.Length} CSX files");

            int successCount = 0;
            int errorCount = 0;

            foreach (string filePath in csxFiles)
            {
                var file = new FileInfo(filePath);
                var (fileValid, fileError) = ValidateFilePath(file, directory.FullName);
                if (!fileValid)
                {
                    Console.WriteLine($"Warning: Skipping {filePath}: {fileError}");
                    errorCount++;
                    continue;
                }

                ParseCSXFile(file, directory.FullName);
                successCount++;
            }

            Console.WriteLine($"Build completed! ({successCount} succeeded, {errorCount} errors)");
        }

        static void WatchCSXFiles(DirectoryInfo directory)
        {
            var (isValid, error) = ValidateDirectoryPath(directory);
            if (!isValid)
            {
                Console.WriteLine($"Error: {error}");
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
                var file = new FileInfo(e.FullPath);
                var (fileValid, fileError) = ValidateFilePath(file, directory.FullName);
                if (!fileValid)
                {
                    Console.WriteLine($"Warning: Skipping {e.FullPath}: {fileError}");
                    return;
                }
                Console.WriteLine($"File changed: {e.FullPath}");
                ParseCSXFile(file, directory.FullName);
            };

            watcher.Created += (sender, e) =>
            {
                var file = new FileInfo(e.FullPath);
                var (fileValid, fileError) = ValidateFilePath(file, directory.FullName);
                if (!fileValid)
                {
                    Console.WriteLine($"Warning: Skipping {e.FullPath}: {fileError}");
                    return;
                }
                Console.WriteLine($"File created: {e.FullPath}");
                ParseCSXFile(file, directory.FullName);
            };

            watcher.Deleted += (sender, e) =>
            {
                Console.WriteLine($"File deleted: {e.FullPath}");
                string generatedFile = Path.ChangeExtension(e.FullPath, ".generated.cs");
                if (IsPathSafe(directory.FullName, generatedFile) && File.Exists(generatedFile))
                {
                    File.Delete(generatedFile);
                }
            };

            watcher.Renamed += (sender, e) =>
            {
                Console.WriteLine($"File renamed: {e.OldFullPath} -> {e.FullPath}");
                
                // Validate the new file
                var newFile = new FileInfo(e.FullPath);
                var (fileValid, fileError) = ValidateFilePath(newFile, directory.FullName);
                if (!fileValid)
                {
                    Console.WriteLine($"Warning: Skipping {e.FullPath}: {fileError}");
                    return;
                }

                string oldGeneratedFile = Path.ChangeExtension(e.OldFullPath, ".generated.cs");
                if (IsPathSafe(directory.FullName, oldGeneratedFile) && File.Exists(oldGeneratedFile))
                {
                    File.Delete(oldGeneratedFile);
                }
                ParseCSXFile(newFile, directory.FullName);
            };

            watcher.EnableRaisingEvents = true;
            Console.WriteLine("Press 'q' to stop watching...");

            while (Console.ReadKey(true).Key != ConsoleKey.Q) { }

            watcher.Dispose();
        }

        static void BuildWithConfig()
        {
            var config = ConfigLoader.Load();
            string baseDir = Directory.GetCurrentDirectory();
            string sourceDir = Path.Combine(baseDir, config.SourceDirectory);

            if (!Directory.Exists(sourceDir))
            {
                Console.WriteLine($"Error: Source directory not found: {sourceDir}");
                return;
            }

            Console.WriteLine($"Building with config from: {Path.Combine(baseDir, "paper.json")}");
            Console.WriteLine($"Source directory: {sourceDir}");

            // Find all matching files
            var files = new List<string>();
            foreach (var pattern in config.IncludePatterns)
            {
                try
                {
                    var matches = Directory.GetFiles(sourceDir, Path.GetFileName(pattern), SearchOption.AllDirectories);
                    files.AddRange(matches);
                }
                catch { }
            }

            // Apply exclude patterns (simple implementation)
            var excludedPatterns = config.ExcludePatterns.SelectMany(p => 
            {
                try
                {
                    return Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
                        .Where(f => f.Contains(p.Replace("**/", "").Replace("*", "")));
                }
                catch
                {
                    return Enumerable.Empty<string>();
                }
            }).ToHashSet();

            files = files.Where(f => !excludedPatterns.Contains(f)).Distinct().ToList();
            
            // Filter to only .csx files
            files = files.Where(f => f.EndsWith(".csx", StringComparison.OrdinalIgnoreCase)).ToList();

            Console.WriteLine($"Found {files.Count} CSX files");

            int successCount = 0;
            int errorCount = 0;

            foreach (string filePath in files)
            {
                var file = new FileInfo(filePath);
                var (fileValid, fileError) = ValidateFilePath(file, sourceDir);
                if (!fileValid)
                {
                    Console.WriteLine($"Warning: Skipping {filePath}: {fileError}");
                    errorCount++;
                    continue;
                }

                ParseCSXFile(file, sourceDir);
                successCount++;
            }

            Console.WriteLine($"Build completed! ({successCount} succeeded, {errorCount} errors)");
        }

        static void WatchWithConfig()
        {
            var config = ConfigLoader.Load();
            string baseDir = Directory.GetCurrentDirectory();
            string sourceDir = Path.Combine(baseDir, config.SourceDirectory);

            if (!Directory.Exists(sourceDir))
            {
                Console.WriteLine($"Error: Source directory not found: {sourceDir}");
                return;
            }

            Console.WriteLine($"Watching with config from: {Path.Combine(baseDir, "paper.json")}");
            Console.WriteLine($"Source directory: {sourceDir}");

            var watcher = new FileSystemWatcher(sourceDir)
            {
                Filter = "*.csx",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            watcher.Changed += (sender, e) =>
            {
                var file = new FileInfo(e.FullPath);
                var (fileValid, fileError) = ValidateFilePath(file, sourceDir);
                if (!fileValid)
                {
                    Console.WriteLine($"Warning: Skipping {e.FullPath}: {fileError}");
                    return;
                }
                Console.WriteLine($"File changed: {e.FullPath}");
                ParseCSXFile(file, sourceDir);
            };

            watcher.Created += (sender, e) =>
            {
                var file = new FileInfo(e.FullPath);
                var (fileValid, fileError) = ValidateFilePath(file, sourceDir);
                if (!fileValid)
                {
                    Console.WriteLine($"Warning: Skipping {e.FullPath}: {fileError}");
                    return;
                }
                Console.WriteLine($"File created: {e.FullPath}");
                ParseCSXFile(file, sourceDir);
            };

            watcher.Deleted += (sender, e) =>
            {
                Console.WriteLine($"File deleted: {e.FullPath}");
                string generatedFile = Path.ChangeExtension(e.FullPath, ".generated.cs");
                if (IsPathSafe(sourceDir, generatedFile) && File.Exists(generatedFile))
                {
                    File.Delete(generatedFile);
                }
            };

            watcher.Renamed += (sender, e) =>
            {
                Console.WriteLine($"File renamed: {e.OldFullPath} -> {e.FullPath}");
                
                var newFile = new FileInfo(e.FullPath);
                var (fileValid, fileError) = ValidateFilePath(newFile, sourceDir);
                if (!fileValid)
                {
                    Console.WriteLine($"Warning: Skipping {e.FullPath}: {fileError}");
                    return;
                }

                string oldGeneratedFile = Path.ChangeExtension(e.OldFullPath, ".generated.cs");
                if (IsPathSafe(sourceDir, oldGeneratedFile) && File.Exists(oldGeneratedFile))
                {
                    File.Delete(oldGeneratedFile);
                }
                ParseCSXFile(newFile, sourceDir);
            };

            watcher.EnableRaisingEvents = true;
            Console.WriteLine("Press 'q' to stop watching...");

            while (Console.ReadKey(true).Key != ConsoleKey.Q) { }

            watcher.Dispose();
        }
    }
}