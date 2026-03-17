using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paper.CSX.CLI
{
    /// <summary>
    /// Paper project configuration (paper.json).
    /// </summary>
    public class PaperConfig
    {
        [JsonPropertyName("sourceDirectory")]
        public string SourceDirectory { get; set; } = "src";

        [JsonPropertyName("outputDirectory")]
        public string OutputDirectory { get; set; } = ".";

        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = "Paper.Generated";

        [JsonPropertyName("excludePatterns")]
        public List<string> ExcludePatterns { get; set; } = new() { "**/node_modules/**" };

        [JsonPropertyName("includePatterns")]
        public List<string> IncludePatterns { get; set; } = new() { "**/*.csx" };

        [JsonPropertyName("compilerOptions")]
        public CompilerOptions CompilerOptions { get; set; } = new();

        [JsonPropertyName("watchOptions")]
        public WatchOptions WatchOptions { get; set; } = new();
    }

    public class CompilerOptions
    {
        [JsonPropertyName("formatOutput")]
        public bool FormatOutput { get; set; } = true;

        [JsonPropertyName("validateSyntax")]
        public bool ValidateSyntax { get; set; } = true;

        [JsonPropertyName("maxFileSizeKB")]
        public int MaxFileSizeKB { get; set; } = 10240;

        [JsonPropertyName("enableWarnings")]
        public bool EnableWarnings { get; set; } = true;
    }

    public class WatchOptions
    {
        [JsonPropertyName("debounceMs")]
        public int DebounceMs { get; set; } = 100;

        [JsonPropertyName("includeCsss")]
        public bool IncludeCsss { get; set; } = true;

        [JsonPropertyName("autoRebuild")]
        public bool AutoRebuild { get; set; } = true;
    }

    /// <summary>
    /// Loads paper.json configuration from a directory.
    /// </summary>
    public static class ConfigLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        /// <summary>
        /// Loads configuration from paper.json in the specified directory.
        /// Returns default config if file doesn't exist.
        /// </summary>
        public static PaperConfig Load(string? directory = null)
        {
            string configPath = Path.Combine(directory ?? Directory.GetCurrentDirectory(), "paper.json");
            
            if (!File.Exists(configPath))
            {
                return new PaperConfig();
            }

            try
            {
                string json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<PaperConfig>(json, JsonOptions);
                return config ?? new PaperConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load paper.json: {ex.Message}");
                Console.WriteLine("Using default configuration.");
                return new PaperConfig();
            }
        }

        /// <summary>
        /// Saves configuration to paper.json.
        /// </summary>
        public static void Save(PaperConfig config, string? directory = null)
        {
            string configPath = Path.Combine(directory ?? Directory.GetCurrentDirectory(), "paper.json");
            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(configPath, json);
            Console.WriteLine($"Configuration saved to: {configPath}");
        }

        /// <summary>
        /// Creates a default paper.json in the specified directory.
        /// </summary>
        public static void CreateDefault(string? directory = null)
        {
            string configPath = Path.Combine(directory ?? Directory.GetCurrentDirectory(), "paper.json");
            
            if (File.Exists(configPath))
            {
                Console.WriteLine($"Warning: {configPath} already exists. Not overwriting.");
                return;
            }

            Save(new PaperConfig(), directory);
            Console.WriteLine($"Created default configuration: {configPath}");
        }
    }
}
