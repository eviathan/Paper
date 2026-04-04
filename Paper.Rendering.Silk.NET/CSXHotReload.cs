using Paper.Core.VirtualDom;
using Paper.CSX;
using Paper.CSX.Runtime;
using Paper.CSSS;

namespace Paper.Rendering.Silk.NET
{
    internal sealed class CSXHotReload : IDisposable
    {
        private readonly Canvas _surface;
        private readonly string _csxFilePath;
        private readonly string _csxDir;
        private readonly string _scopeId;

        private FileSystemWatcher? _watcher;
        // Watchers for imported .csx files (keyed by absolute path)
        private readonly Dictionary<string, FileSystemWatcher> _csxImportWatchers = new(StringComparer.OrdinalIgnoreCase);
        private System.Threading.Timer? _csssPollTimer;
        // Tracks last-seen write time for each watched CSSS file
        private readonly Dictionary<string, DateTime> _csssModTimes = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new();

        private volatile Func<Props, UINode> _impl = _ => UI.Text("Loading CSX...");
        private CSXCompiledComponent? _compiled;

        public Func<Props, UINode> RootComponent { get; }

        public CSXHotReload(Canvas surface, string csxFilePath, string scopeId)
        {
            _surface = surface;
            _csxFilePath = csxFilePath;
            _csxDir = Path.GetDirectoryName(csxFilePath) ?? ".";
            _scopeId = scopeId;

            RootComponent = props => _impl(props);
        }

        public void Start()
        {
            CompileAndSwap();

            var fileName = Path.GetFileName(_csxFilePath);

            _watcher = new FileSystemWatcher(_csxDir)
            {
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += (_, __) => DebouncedCompile();
            _watcher.Created += (_, __) => DebouncedCompile();
            _watcher.Renamed += (_, __) => DebouncedCompile();

            // Poll CSSS files for changes — more reliable than FileSystemWatcher on macOS where
            // editors save via atomic rename and the watcher may receive events for a temp filename.
            InitCSSSTracking();
            _csssPollTimer = new System.Threading.Timer(_ => PollCSSS(), null,
                TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300));
        }

        private void DebouncedCompile()
        {
            // No debounce: every file change triggers a recompile so all events are handled.
            _ = Task.Run(() => CompileAndSwap());
        }

        // ── CSX import watching ───────────────────────────────────────────────

        private void UpdateCSXImportWatchers(string csxSource)
        {
            var importPaths = ExtractCSXImports(csxSource, _csxDir).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Remove watchers for paths no longer imported
            foreach (var removed in _csxImportWatchers.Keys.Except(importPaths, StringComparer.OrdinalIgnoreCase).ToList())
            {
                _csxImportWatchers[removed].Dispose();
                _csxImportWatchers.Remove(removed);
            }

            // Add watchers for newly imported paths
            foreach (var absPath in importPaths)
            {
                if (_csxImportWatchers.ContainsKey(absPath)) continue;
                var importDir = Path.GetDirectoryName(absPath) ?? ".";
                var importFile = Path.GetFileName(absPath);
                try
                {
                    var w = new FileSystemWatcher(importDir)
                    {
                        Filter = importFile,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                        EnableRaisingEvents = true,
                    };
                    w.Changed += (_, __) => DebouncedCompile();
                    w.Created += (_, __) => DebouncedCompile();
                    w.Renamed += (_, __) => DebouncedCompile();
                    _csxImportWatchers[absPath] = w;
                    Console.Error.WriteLine($"[Paper] Watching imported CSX: {Path.GetFileName(absPath)}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Paper] Failed to watch imported CSX {absPath}: {ex.Message}");
                }
            }
        }

        private static IEnumerable<string> ExtractCSXImports(string csxSource, string baseDir)
        {
            foreach (var line in csxSource.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("@import", StringComparison.OrdinalIgnoreCase)) continue;
                int q1 = trimmed.IndexOfAny('"', '\'');
                int q2 = q1 >= 0 ? trimmed.IndexOfAny('"', '\'', q1 + 1) : -1;
                if (q1 < 0 || q2 <= q1) continue;
                string importPath = trimmed[(q1 + 1)..q2];
                if (!importPath.EndsWith(".csx", StringComparison.OrdinalIgnoreCase)) continue;
                yield return Path.GetFullPath(Path.Combine(baseDir, importPath));
            }
        }

        // ── CSSS polling ──────────────────────────────────────────────────────

        private void InitCSSSTracking()
        {
            // Co-located <same>.csss — normalise to absolute path
            var colocated = Path.GetFullPath(Path.ChangeExtension(_csxFilePath, ".csss"));
            TrackCSSS(colocated);

            // @import "*.csss" declared in the CSX source
            try
            {
                var csx = File.ReadAllText(_csxFilePath);
                foreach (var p in ExtractCSSSImports(csx, Path.GetDirectoryName(_csxFilePath) ?? "."))
                    TrackCSSS(p);
            }
            catch { /* ignore — CompileAndSwap will report any file errors */ }

            Console.Error.WriteLine($"[Paper] CSSS polling: {string.Join(", ", _csssModTimes.Keys.Select(Path.GetFileName))}");
        }

        private void TrackCSSS(string absPath)
        {
            absPath = Path.GetFullPath(absPath);
            if (!_csssModTimes.ContainsKey(absPath))
                _csssModTimes[absPath] = File.Exists(absPath) ? File.GetLastWriteTimeUtc(absPath) : DateTime.MinValue;
        }

        private void PollCSSS()
        {
            foreach (var (path, lastSeen) in _csssModTimes.ToList())
            {
                if (!File.Exists(path)) continue;
                var current = File.GetLastWriteTimeUtc(path);
                if (current == lastSeen) continue;
                Console.Error.WriteLine($"[Paper] CSSS changed: {Path.GetFileName(path)} ({lastSeen:T} → {current:T})");
                _csssModTimes[path] = current;
                _ = Task.Run(() => ReloadCSSS(path));
            }
        }

        private void ReloadCSSS(string csssPath)
        {
            try
            {
                var sheet = CSSSLoader.Reload(csssPath);
                _surface.Styles.ReplaceCSSSSheet(csssPath, sheet);
                _surface.RequestRender();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Paper] CSSS reload error ({Path.GetFileName(csssPath)}): {ex.Message}");
            }
        }

        private void CompileAndSwap()
        {
            try
            {
                var csx = File.ReadAllText(_csxFilePath);

                // Optional co-located scoped CSSS: <same>.csss
                var csssPath = Path.ChangeExtension(_csxFilePath, ".csss");
                if (File.Exists(csssPath))
                {
                    var csss = File.ReadAllText(csssPath);
                    var map = ScopedCSSSCompiler.CompileScoped(csss, _scopeId);
                    foreach (var (cls, style) in map)
                        _surface.Styles.SetClass(cls, style);
                }

                // Optional co-located CSSS sheet: <same>.csss
                var colocatedCSSSPath = Path.ChangeExtension(_csxFilePath, ".csss");
                if (File.Exists(colocatedCSSSPath))
                {
                    var sheet = CSSSLoader.Reload(colocatedCSSSPath);
                    _surface.Styles.ReplaceCSSSSheet(colocatedCSSSPath, sheet);
                }

                // Load any @import "*.csss" declared at the top of the CSX file
                foreach (var importPath in ExtractCSSSImports(csx, Path.GetDirectoryName(_csxFilePath) ?? "."))
                {
                    TrackCSSS(importPath); // ensure newly-added imports are polled
                    var sheet = CSSSLoader.Reload(importPath);
                    _surface.Styles.ReplaceCSSSSheet(importPath, sheet);
                }

                // Track and watch any @import "*.csx" files so changes trigger recompile.
                UpdateCSXImportWatchers(csx);

                var compiled = CSXRuntimeCompiler.Compile(csx, componentClassName: "CSXHotComponent", baseDir: _csxDir);

                var prev = _compiled;
                _compiled = compiled;
                _impl = compiled.Render;

                // Try to unload the previous compilation to avoid leaks.
                if (prev != null)
                {
                    prev.LoadContext.Unload();
                }

                _surface.RequestRender();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Paper] CSX hot reload error: " + ex.ToString());
                _impl = _ => UI.Box(
                    new PropsBuilder()
                        .Style(new Paper.Core.Styles.StyleSheet
                        {
                            Width = Paper.Core.Styles.Length.Percent(100),
                            Height = Paper.Core.Styles.Length.Percent(100),
                            Background = new Paper.Core.Styles.PaperColour(0.25f, 0.05f, 0.05f, 1f),
                            Padding = new Paper.Core.Styles.Thickness(Paper.Core.Styles.Length.Px(16)),
                        })
                        .Children(
                            UI.Text("CSX hot reload error:", new Paper.Core.Styles.StyleSheet { FontSize = Paper.Core.Styles.Length.Px(18) }),
                            UI.Text(ex.Message, new Paper.Core.Styles.StyleSheet { FontSize = Paper.Core.Styles.Length.Px(14) })
                        )
                        .Build()
                );

                _surface.RequestRender();
            }
        }

        /// <summary>
        /// Scan a .csx source file for top-level @import "*.csss" lines.
        /// Returns absolute paths of referenced .csss files.
        /// </summary>
        private static IEnumerable<string> ExtractCSSSImports(string csxSource, string baseDir)
        {
            foreach (var line in csxSource.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("@import", StringComparison.OrdinalIgnoreCase)) continue;
                int q1 = trimmed.IndexOfAny('"', '\'');
                int q2 = q1 >= 0 ? trimmed.IndexOfAny('"', '\'', q1 + 1) : -1;
                if (q1 < 0 || q2 <= q1) continue;
                string importPath = trimmed[(q1 + 1)..q2];
                if (!importPath.EndsWith(".csss", StringComparison.OrdinalIgnoreCase)) continue;
                yield return Path.GetFullPath(Path.Combine(baseDir, importPath));
            }
        }

        public void Dispose()
        {
            _watcher?.Dispose();
            foreach (var w in _csxImportWatchers.Values) w.Dispose();
            _csxImportWatchers.Clear();
            _csssPollTimer?.Dispose();
            if (_compiled != null)
                _compiled.LoadContext.Unload();
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
            int ab = s.IndexOfAny(a, b, startIndex);
            int ic = s.IndexOf(c, startIndex);
            if (ab < 0) return ic;
            if (ic < 0) return ab;
            return Math.Min(ab, ic);
        }
    }
}
