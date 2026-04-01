using Paper.Rendering.Silk.NET;
using Paper.Core.Dock;
using System.Collections.Generic;
using Paper.Core.VirtualDom;
using Paper.Core.Styles;
using Paper.Core.Hooks;

namespace Paper.Playground;

class Program
{
    static void Main(string[] args)
    {
        var app = new PaperSurface("❖ Paper ❖", 800, 700);
        app.MinimumWindowWidth = 400;
        app.MinimumWindowHeight = 500;

        if (args.Length > 0 && args[0] == "--dock")
        {
            Console.WriteLine("Paper.Playground: Testing Manual Dock.");
            
            app.Mount(props => TestDockComponent.TestDock(props));
        }
        else
        {
            var csxPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../src/App.csx"));
            Console.WriteLine($"Paper.Playground: Loading {csxPath}");
            if (File.Exists(csxPath))
            {
                Console.WriteLine("Mounting App.csx hot reload.");
                app.MountCSXHotReload(csxPath, scopeId: "DemoApp");
            }
        }

        app.Run();
    }
}
