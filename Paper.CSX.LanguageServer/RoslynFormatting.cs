using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp;
using Paper.CSX;

namespace Paper.CSX.LanguageServer
{
    internal static class RoslynFormatting
    {
        public static object[] Format(string csxSrc, string csxUri, JsonElement options)
        {
            try
            {
                var (compilation, tree, generatedSrc) = RoslynHover.GetOrBuildCompilation(csxSrc);
                
                var workspace = new AdhocWorkspace();
                var project = workspace.AddProject("temp", LanguageNames.CSharp);
                var document = workspace.AddDocument(project.Id, "temp.csx", csxSrc);
                
                var formattedDoc = Formatter.FormatAsync(document).GetAwaiter().GetResult();
                var formattedSrc = formattedDoc.GetTextAsync().GetAwaiter().GetResult().ToString();
                
                if (formattedSrc == csxSrc)
                    return Array.Empty<object>();
                
                var edits = ComputeTextEdits(csxSrc, formattedSrc);
                return edits;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[LSP] Formatting error: {ex.Message}");
                return Array.Empty<object>();
            }
        }
        
        private static object[] ComputeTextEdits(string oldText, string newText)
        {
            var edits = new List<object>();
            
            var oldLines = oldText.Split('\n');
            var newLines = newText.Split('\n');
            
            int commonPrefix = 0;
            int oldSuffix = oldLines.Length - 1;
            int newSuffix = newLines.Length - 1;
            
            while (commonPrefix < oldLines.Length && commonPrefix < newLines.Length && 
                   oldLines[commonPrefix] == newLines[commonPrefix])
                commonPrefix++;
            
            while (oldSuffix >= commonPrefix && newSuffix >= commonPrefix && 
                   oldLines[oldSuffix] == newLines[newSuffix])
            {
                oldSuffix--;
                newSuffix--;
            }
            
            if (commonPrefix > 0 || oldSuffix >= commonPrefix || newSuffix >= commonPrefix)
            {
                edits.Add(new
                {
                    range = new
                    {
                        start = new { line = commonPrefix, character = 0 },
                        end = new { line = oldSuffix + 1, character = 0 }
                    },
                    newText = string.Join("\n", newLines.Skip(commonPrefix).Take(newSuffix - commonPrefix + 1))
                });
            }
            
            return edits.ToArray();
        }
    }
}
