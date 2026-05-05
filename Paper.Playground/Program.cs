using Paper.Rendering.Silk.NET;
using Paper.Core.Dock;
using Paper.Core.VirtualDom;

namespace Paper.Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--dock")
            {
                Console.WriteLine("Paper.Playground: Testing Manual Dock.");

                var session = new DockWindowSession();
                var panels  = TestDockComponent.Panels;

                var canvas = new Canvas("Paper", 800, 700)
                {
                    MinimumWindowWidth  = 400,
                    MinimumWindowHeight = 500,
                    DisableVSync        = true,
                    DockSession         = session,
                };
                canvas.Mount(props => TestDockComponent.TestDock(session, props));

                DockWindowFactory.Wire(session, panels, primaryCanvas: canvas, primaryWindowId: "main");
                CanvasManager.Run(canvas);
            }
            else
            {
                var canvas = new Canvas("Paper", 800, 700)
                {
                    MinimumWindowWidth  = 400,
                    MinimumWindowHeight = 500,
                };
                canvas.MountCSXHotReload("src/App.csx", scopeId: "DemoApp");
                canvas.Run();
            }
        }
    }
}