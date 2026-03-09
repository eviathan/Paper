using Paper.Rendering.Silk.NET;

namespace Paper.Playground;

class Program
{
    static void Main(string[] args)
    {
        // Use C#-only UI to verify clicks/state work without CSX. Set to false to use App.csx hot reload.
        var useCSharpOnly = args.Contains("--csharp", StringComparer.OrdinalIgnoreCase);

        var app = new PaperSurface(useCSharpOnly ? "Paper — C# mode (buttons should work)" : "Paper — CSX mode", 800, 600);
        // Minimum size so content can fit or scroll; root has overflow: scroll so smaller windows scroll.
        app.MinimumWindowWidth = 400;
        app.MinimumWindowHeight = 500;
        // Do not set AlwaysRender = true; it causes full reconcile every frame and freezes UI when spamming.

        if (useCSharpOnly)
        {
            Console.WriteLine("Paper.Playground: using C# SimpleCounter (no CSX). Click buttons to test.");
            app.Mount(TestApp.SimpleCounter);
        }
        else if (args.Contains("--controls", StringComparer.OrdinalIgnoreCase))
        {
            var controlsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../src/ControlsApp.csx"));
            Console.WriteLine($"Paper.Playground: Controls CSX path = {controlsPath}");
            if (File.Exists(controlsPath))
            {
                Console.WriteLine("Mounting ControlsApp.csx hot reload.");
                app.MountCSXHotReload(controlsPath, scopeId: "ControlsApp");
            }
            else
            {
                Console.WriteLine("ControlsApp.csx not found — using SimpleCounter fallback.");
                app.Mount(TestApp.SimpleCounter);
            }
        }
        else if (args.Contains("--demo", StringComparer.OrdinalIgnoreCase))
        {
            var controlsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../src/DemoApp.csx"));
            Console.WriteLine($"Paper.Playground: DemoApp CSX path = {controlsPath}");
            if (File.Exists(controlsPath))
            {
                Console.WriteLine("Mounting DemoApp.csx hot reload.");
                app.MountCSXHotReload(controlsPath, scopeId: "DemoApp");
            }
            else
            {
                Console.WriteLine("DemoApp.csx not found — using SimpleCounter fallback.");
                app.Mount(TestApp.SimpleCounter);
            }
        }
        else
        {
            var csxPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../src/App.csx"));
            Console.WriteLine($"Paper.Playground: CSX path = {csxPath}");
            if (File.Exists(csxPath))
            {
                Console.WriteLine("Mounting App.csx hot reload.");
                app.MountCSXHotReload(csxPath, scopeId: "DemoApp");
            }
            else
            {
                Console.WriteLine("App.csx not found — using SimpleCounter fallback.");
                app.Mount(TestApp.SimpleCounter);
            }
        }

        app.Run();
    }
}
