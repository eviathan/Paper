using Paper.Rendering.Silk.NET;
using Paper.Core.Dock;
using System.Collections.Generic;
using Paper.Core.VirtualDom;
using Paper.Core.Styles;
using Paper.Core.Hooks;

namespace Paper.Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            var canvas = new Canvas("Paper", 800, 700)
            {
                MinimumWindowWidth = 400,
                MinimumWindowHeight = 500
            };

            if (args.Length > 0 && args[0] == "--dock")
            {
                Console.WriteLine("Paper.Playground: Testing Manual Dock.");
                canvas.Mount(TestDockComponent.TestDock);
            }
            else
            {
                canvas.MountCSXHotReload("src/App.csx", scopeId: "DemoApp");
            }

            canvas.Run();
        }
    }    
}