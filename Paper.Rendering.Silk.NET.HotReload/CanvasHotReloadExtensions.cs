using System.Reflection;
using Paper.CSX.Runtime;
using Paper.Rendering.Silk.NET;

namespace Paper.Rendering.Silk.NET.HotReload
{
    /// <summary>
    /// Optional CSX hot-reload support for <see cref="Canvas"/>, split out of
    /// Paper.Rendering.Silk.NET so consumers who only want to *render* Paper UI (e.g. a game
    /// embedding it via <c>PaperEmbeddedSurface</c>) aren't forced to pull in Roslyn.
    /// </summary>
    public static class CanvasHotReloadExtensions
    {
        /// <summary>
        /// Configure additional assemblies to be available during CSX hot-reload compilation.
        /// Call this before <see cref="MountCSXHotReload"/> to ensure game engine types are
        /// accessible.
        /// </summary>
        public static void AddCSXReferences(this Canvas canvas, params Assembly[] assemblies) =>
            CSXRuntimeCompiler.AddReferences(assemblies);

        /// <summary>
        /// Mounts a <c>.csx</c> file as the canvas's root component and hot-reloads it (plus any
        /// co-located/imported <c>.csss</c> stylesheet) whenever the source changes on disk.
        /// Returns the <see cref="CSXHotReload"/> instance — the caller owns its lifetime
        /// (dispose it, e.g. before mounting a different file, same as any other <see cref="IDisposable"/>).
        /// </summary>
        public static CSXHotReload MountCSXHotReload(this Canvas canvas, string csxFilePath, string? scopeId = null)
        {
            // Dev: source lives 3 dirs above the bin output (bin/Debug/net10.0 → project root)
            var devPath       = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, $"../../../{csxFilePath}"));
            // Published: files are copied alongside the binary (CopyToOutputDirectory)
            var publishedPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, csxFilePath));
            var csxPath       = File.Exists(devPath) ? devPath : publishedPath;
            Console.WriteLine($"Paper.Playground: Loading {csxPath}");

            if (!File.Exists(csxPath))
                throw new InvalidOperationException($"Could not properly mount {csxFilePath}");

            Console.WriteLine($"Mounting {csxFilePath} hot reload.");
            scopeId ??= Path.GetFileNameWithoutExtension(csxFilePath);

            // Ensure Paper.Icons is loaded into the AppDomain before the Roslyn runtime compiler
            // scans GetAssemblies() — it's only loaded lazily on first GL use otherwise, which is
            // too late for the initial hot-reload compile.
            CSXRuntimeCompiler.AddReferences(typeof(Paper.Icons.IconSets).Assembly);

            var hotReload = new CSXHotReload(canvas, csxPath, scopeId);
            hotReload.Start();
            canvas.Mount(hotReload.RootComponent);
            return hotReload;
        }
    }
}
