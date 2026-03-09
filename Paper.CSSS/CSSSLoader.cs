namespace Paper.CSSS
{
    /// <summary>
    /// Loads and caches compiled CSSS style sheets from the file system.
    /// Supports @import directives (resolved relative to the importing file).
    /// </summary>
    public static class CSSSLoader
    {
        private static readonly Dictionary<string, CSSSSheet> _cache =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new();

        /// <summary>Load a .csss file, returning a cached CSSSSheet. Resolves @import directives.</summary>
        public static CSSSSheet Load(string filePath)
        {
            string abs = Path.GetFullPath(filePath);
            lock (_lock)
            {
                if (_cache.TryGetValue(abs, out var cached)) return cached;
                return LoadInternal(abs);
            }
        }

        /// <summary>Force reload a file (call after the file has changed on disk).</summary>
        public static CSSSSheet Reload(string filePath)
        {
            string abs = Path.GetFullPath(filePath);
            lock (_lock)
            {
                _cache.Remove(abs);
                return LoadInternal(abs);
            }
        }

        /// <summary>Remove a file from the cache without reloading.</summary>
        public static void Invalidate(string filePath)
        {
            lock (_lock) _cache.Remove(Path.GetFullPath(filePath));
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private static CSSSSheet LoadInternal(string absPath)
        {
            string source = File.Exists(absPath) ? File.ReadAllText(absPath) : "";
            string baseDir = Path.GetDirectoryName(absPath) ?? ".";
            string resolved = ResolveImports(source, baseDir, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { absPath });

            var map   = CSSSCompiler.Compile(resolved);
            var sheet = CSSSSheet.FromDictionary(absPath, map);
            _cache[absPath] = sheet;
            return sheet;
        }

        /// <summary>
        /// Inline @import "other.csss" directives by reading and prepending their content.
        /// The visited set prevents circular imports.
        /// </summary>
        private static string ResolveImports(string source, string baseDir, HashSet<string> visited)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var rawLine in source.Split('\n'))
            {
                var trimmed = rawLine.Trim();
                if (trimmed.StartsWith("@import", StringComparison.OrdinalIgnoreCase))
                {
                    int q1 = trimmed.IndexOfAny('"', '\'');
                    int q2 = q1 >= 0 ? trimmed.IndexOfAny('"', '\'', q1 + 1) : -1;
                    if (q1 >= 0 && q2 > q1)
                    {
                        string importPath = trimmed[(q1 + 1)..q2];
                        string absImport  = Path.GetFullPath(Path.Combine(baseDir, importPath));
                        if (!visited.Contains(absImport) && File.Exists(absImport))
                        {
                            visited.Add(absImport);
                            string importSrc = File.ReadAllText(absImport);
                            string importDir = Path.GetDirectoryName(absImport) ?? baseDir;
                            sb.AppendLine(ResolveImports(importSrc, importDir, visited));
                            continue;
                        }
                    }
                    continue; // skip unresolved or circular @import
                }
                sb.AppendLine(rawLine);
            }
            return sb.ToString();
        }
    }

    file static class StringExtensions
    {
        public static int IndexOfAny(this string s, char a, char b, int startIndex = 0)
        {
            int ia = s.IndexOf(a, startIndex);
            int ib = s.IndexOf(b, startIndex);
            if (ia < 0) return ib;
            if (ib < 0) return ia;
            return Math.Min(ia, ib);
        }

        public static int IndexOfAny(this string s, char a, char b, char c, int startIndex = 0)
        {
            int iab = s.IndexOfAny(a, b, startIndex);
            int ic  = s.IndexOf(c, startIndex);
            if (iab < 0) return ic;
            if (ic  < 0) return iab;
            return Math.Min(iab, ic);
        }
    }
}
