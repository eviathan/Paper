using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Paper.Layout;
using Paper.Rendering.Silk.NET.Models;
using Paper.Rendering.Silk.NET.Text;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// The top-level Paper rendering surface. Creates a window, hosts the reconciler
    /// and layout engine, and renders the UI tree each frame.
    ///
    /// Usage:
    /// <code>
    ///   var canvas = new Canvas("My App", 1280, 720);
    ///   canvas.OnLoad    = (gl, ic, w, h) => host.Initialise(gl, ic, w, h);
    ///   canvas.PreRender = dt => host.Tick(dt);
    ///   canvas.Mount(() => MyAppComponent(Props.Empty));
    ///   canvas.Run();
    /// </code>
    /// </summary>
    public sealed partial class Canvas : IDisposable
    {
        private readonly string _title;
        private int _width;
        private int _height;
        private Func<UINode>? _rootFactory;
        private IWindow? _window;
        private GL? _gl;
        private RectBatch? _rects;
        private TexturedQuadRenderer? _viewports;
        private FontRegistry? _fontSet;
        private TextBatch? _text => _fontSet?.Default;
        private Core.Reconciler.Reconciler? _reconciler;
        private LayoutEngine? _layout;
        private ILayoutMeasurer? _measurer;
        private IInputContext? _inputContext;
        private CSXHotReload? _csxHotReload;
        private ImageTextureLoader? _imageLoader;
        private FiberRenderer? _renderer;
        private Core.Reconciler.Fiber? _pointerDownFiber;
        private string? _pointerDownFiberPath;

        private ClickState _clickState { get; set; } = new();
        private GLFWState _glfwState { get; set; } = new();
        private FramebufferState _framebufferState { get; set; } = new();
        private InputState _inputState { get; set; } = new();
        private RenderState _renderState { get; set; } = new();
        private ScrollState _scrollState { get; set; } = new();
        private UIState _uiState { get; set; } = new();

        /// <summary>
        /// When true, Paper reconciles every frame (useful for apps driven by external state).
        /// For production UI, prefer false so updates are event/state driven.
        /// </summary>
        public bool AlwaysRender { get; set; } = false;

        /// <summary>Ratio of physical framebuffer pixels to logical window pixels (e.g. 2 on Retina). Updated each frame.</summary>
        public float DpiScale { get; private set; } = 1f;

        /// <summary>Global style registry for this surface.</summary>
        public StyleRegistry Styles { get; } = new();

        /// <summary>Minimum window width in pixels. Null = no minimum.</summary>
        public int? MinimumWindowWidth { get; set; }

        /// <summary>Minimum window height in pixels. Null = no minimum.</summary>
        public int? MinimumWindowHeight { get; set; }

        /// <summary>Window border type. Default is Resizable.</summary>
        public WindowBorder WindowBorder { get; set; } = WindowBorder.Resizable;

        /// <summary>Convenience: set true for a frameless window (sets WindowBorder to Hidden). Default false.</summary>
        public bool Frameless
        {
            get => WindowBorder == WindowBorder.Hidden;
            set => WindowBorder = value ? WindowBorder.Hidden : WindowBorder.Resizable;
        }

        /// <summary>Called once after the GL context and input context are created.</summary>
        public Action<GL, IInputContext, int, int>? OnLoad { get; set; }

        /// <summary>Called at the start of each render frame, before Paper renders its UI.</summary>
        public Action<double>? PreRender { get; set; }

        public Canvas(string title = "Paper", int width = 1280, int height = 720)
        {
            _title = title;
            _width = width;
            _height = height;
        }

        /// <summary>Create a canvas with full control over windowing options.</summary>
        public Canvas(string title, int width, int height, WindowBorder windowBorder, bool isEventDriven = false, bool vsync = true)
        {
            _title = title;
            _width = width;
            _height = height;
            WindowBorder = windowBorder;
        }

        /// <summary>Releases all GPU and managed resources. Called automatically at the end of <see cref="Run"/>.</summary>
        public void Dispose() => DisposeResources();

        /// <summary>Set the root component factory.</summary>
        public void Mount(Func<Props, UINode> rootComponent)
        {
            _rootFactory = () => new UINode(rootComponent, Props.Empty);
        }

        /// <summary>Development helper: mount a CSX file and enable hot reload while running.</summary>
        public void MountCSXHotReload(string csxFilePath, string? scopeId = null)
        {
            var csxPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, $"../../../{csxFilePath}"));
            Console.WriteLine($"Paper.Playground: Loading {csxPath}");

            if (File.Exists(csxPath))
            {
                Console.WriteLine($"Mounting {csxFilePath} hot reload.");
                scopeId ??= Path.GetFileNameWithoutExtension(csxFilePath);

                _csxHotReload?.Dispose();
                _csxHotReload = new CSXHotReload(this, csxPath, scopeId);
                _csxHotReload.Start();

                Mount(_csxHotReload.RootComponent);
            }
            else
            {
                throw new InvalidOperationException($"Could not properly mount {csxFilePath}");
            }
        }

        /// <summary>Run the surface event loop (blocking). Disposes all GPU resources before returning.</summary>
        public void Run()
        {
            if (_rootFactory == null)
                throw new InvalidOperationException("Call Mount() before Run().");

            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(_width, _height);
            options.Title = _title;
            options.ShouldSwapAutomatically = true;
            options.VSync = true;
            options.IsEventDriven = false;
            options.PreferredStencilBufferBits = 8;
            options.WindowBorder = WindowBorder;

            _window = Window.Create(options);
            _window.Load += OnWindowLoad;
            _window.Render += OnRender;
            _window.Resize += OnResize;

            Console.WriteLine("Window created, running event loop...");
            _window.Initialize();

            try
            {
                RunLoop();
            }
            finally
            {
                DisposeResources();
            }
        }

        public void RequestRender()
        {
            _renderState.ExternalRenderRequested = true;
            _renderState.LayoutDirty = true;
        }

        public void Shutdown() => _window?.Close();

        /// <summary>
        /// Mark that a draw is needed and extend the animation deadline (keeps rendering alive
        /// for scrollbar fade / CSS transitions for up to <paramref name="animationSeconds"/> seconds).
        /// </summary>
        private void MarkDirty(double animationSeconds = 0)
        {
            _renderState.LayoutDirty = true;
            if (animationSeconds > 0)
            {
                double deadline = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond + animationSeconds;
                if (deadline > _renderState.AnimationDeadline)
                    _renderState.AnimationDeadline = deadline;
            }
        }

        /// <summary>
        /// Run loop. VSync makes DoRender() block until the next display refresh.
        /// When nothing needs drawing we sleep 4 ms so the thread yields CPU.
        /// </summary>
        private void RunLoop()
        {
            while (_window != null && !_window.IsClosing)
            {
                _window.DoEvents();
                if (_window.IsClosing) break;

                _window.DoUpdate();
                if (_window.IsClosing) break;

                var utcNow = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
                var isAnimating = utcNow < _renderState.AnimationDeadline;

                if (AlwaysRender || _renderState.LayoutDirty || isAnimating)
                    _window.DoRender();
                else
                    Thread.Sleep(4);
            }

            _window?.DoEvents();
        }
    }
}
