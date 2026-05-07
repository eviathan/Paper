using Paper.Core.Hooks;
using Paper.Core.Reconciler;
using Paper.Core.VirtualDom;
using Paper.Layout;
using Paper.Rendering.Silk.NET.Text;
using Paper.Rendering.Silk.NET.Utilities;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.IO;
using System.Runtime.InteropServices;

namespace Paper.Rendering.Silk.NET
{
    public sealed partial class Canvas
    {
        private void OnWindowLoad()
        {
            _gl = GL.GetApi(_window!);
            _rects = new RectBatch(_gl);
            _viewports = new TexturedQuadRenderer(_gl);
            _imageLoader = new ImageTextureLoader(_gl);
            _layout = new LayoutEngine();
            _measurer = new FallbackLayoutMeasurer();

            _gl.Enable(EnableCap.Blend);
            _gl.BlendFuncSeparate(
                BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                BlendingFactor.Zero, BlendingFactor.One
            );

            var fontRegistry = LoadFonts(_gl);
            if (fontRegistry.Default != null)
            {
                _fontSet = fontRegistry;
                _measurer = new SilkTextMeasurer(fontRegistry);
            }

            _renderer = new FiberRenderer(_rects!, _viewports!, _fontSet, _width, _height, _gl)
            {
                GetScrollOffset = path =>
                    _scrollState.ScrollOffsets.TryGetValue(path, out var value) ? value : (0f, 0f),
                GetScrollbarOpacity = path =>
                {
                    if (!_scrollState.ScrollbarLastActive.TryGetValue(path, out double lastActiveTime))
                        return 0f;

                    double elapsed = (DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond) - lastActiveTime;
                    const double visibleSeconds = 1.2, fadeSeconds = 0.4;

                    if (elapsed < visibleSeconds) return 1f;
                    if (elapsed >= visibleSeconds + fadeSeconds) return 0f;
                    return 1f - (float)((elapsed - visibleSeconds) / fadeSeconds);
                },
                GetImageTexture = path =>
                    _imageLoader?.GetOrLoad(PaperUtility.ResolveImagePath(path)).Handle ?? 0,
                GetImageResult = path =>
                {
                    var result = _imageLoader != null ? _imageLoader.GetOrLoad(PaperUtility.ResolveImagePath(path)) : default;
                    return result.Handle != 0 ? (result.Handle, result.Width, result.Height) : (0u, 0, 0);
                }
            };

            var prevRequest = RenderScheduler.OnRenderRequested;
            _reconciler = new Reconciler();
            var reconRequest = RenderScheduler.OnRenderRequested;
            RenderScheduler.OnRenderRequested = () =>
            {
                prevRequest?.Invoke();
                reconRequest?.Invoke();
                _renderState.LayoutDirty = true;
            };
            _reconciler.Mount(_rootFactory!());
            _renderState.LayoutDirty = true;

            _width = _window!.Size.X;
            _height = _window.Size.Y;

            ApplyMinimumWindowSizeLimits();
            InitGlfwCursors();

            var frameBufferSize = _window!.FramebufferSize;
            var inputContext = _window.CreateInput();
            _inputContext = inputContext;

            foreach (var mouse in inputContext.Mice)
            {
                mouse.MouseDown += OnMouseButtonDown;
                mouse.MouseUp += OnMouseButtonUp;
                mouse.MouseMove += OnMouseMove;
                mouse.Scroll += OnMouseScroll;
            }

            foreach (var keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += OnKeyDown;
                keyboard.KeyUp += OnKeyUp;
                keyboard.KeyChar += OnKeyChar;
            }

            OnLoad?.Invoke(_gl, inputContext, _width, _height);

            // Apply macOS unified title bar style after window creation
            ConfigureMacOSUnifiedTitleBar();
        }

        private void DisposeResources()
        {
            _inputState.CaretBlinkTimer?.Dispose();
            _inputState.CaretBlinkTimer = null;

            _csxHotReload?.Dispose();
            _csxHotReload = null;

            _inputContext?.Dispose();
            _inputContext = null;

            // Make this canvas's GL context current before deleting its GL objects.
            // If another window rendered last, its context is still current on this thread.
            // GL delete calls act on the current context, so without this we would delete
            // handles from the wrong context — corrupting the other window's VAOs/textures.
            try { _window?.GLContext?.MakeCurrent(); } catch { }

            _imageLoader?.Dispose();
            _imageLoader = null;

            _rects?.Dispose();
            _rects = null;

            _fontSet?.Dispose();
            _fontSet = null;

            _viewports?.Dispose();
            _viewports = null;

            DestroyGlfwCursors();

            _window?.Dispose();
            _window = null;
        }

        /// <summary>Discovers and loads all TTF font families from the Assets/fonts directory.</summary>
        private static FontRegistry LoadFonts(GL gl)
        {
            var fontRegistry = new FontRegistry();
            var fontDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "fonts");

            if (!Directory.Exists(fontDirectory))
                return fontRegistry;

            foreach (var regularPath in Directory.GetFiles(fontDirectory, "*.ttf"))
            {
                var fontName = Path.GetFileNameWithoutExtension(regularPath);

                if (fontName.EndsWith("-bold", StringComparison.OrdinalIgnoreCase)) continue;
                if (fontName.EndsWith("-italic", StringComparison.OrdinalIgnoreCase)) continue;
                if (fontName.EndsWith("-bolditalic", StringComparison.OrdinalIgnoreCase)) continue;
                if (fontName.EndsWith("bold", StringComparison.OrdinalIgnoreCase) && fontName.Length > 4) continue;

                bool isIconFont = fontName.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0;

                var atlasLoader = isIconFont
                    ? (Func<GL, string, Dictionary<int, PaperFontAtlas>>)((glContext, path) => PaperFontLoader.LoadIconSet(glContext, path))
                    : (glContext, path) => PaperFontLoader.LoadSet(glContext, path);

                var regular = new PaperFontSet(atlasLoader(gl, regularPath), gl);

                PaperFontSet? bold = null;
                foreach (var suffix in new[] { "-bold", "-Bold", "bold", "Bold" })
                {
                    var boldPath = Path.Combine(fontDirectory, fontName + suffix + ".ttf");
                    if (File.Exists(boldPath)) { bold = new PaperFontSet(PaperFontLoader.LoadSet(gl, boldPath), gl); break; }
                }

                PaperFontSet? italic = null;
                foreach (var suffix in new[] { "-italic", "-Italic" })
                {
                    var italicPath = Path.Combine(fontDirectory, fontName + suffix + ".ttf");
                    if (File.Exists(italicPath)) { italic = new PaperFontSet(PaperFontLoader.LoadSet(gl, italicPath), gl); break; }
                }

                PaperFontSet? boldItalic = null;
                foreach (var suffix in new[] { "-bolditalic", "-BoldItalic", "-bold-italic", "-Bold-Italic" })
                {
                    var boldItalicPath = Path.Combine(fontDirectory, fontName + suffix + ".ttf");
                    if (File.Exists(boldItalicPath)) { boldItalic = new PaperFontSet(PaperFontLoader.LoadSet(gl, boldItalicPath), gl); break; }
                }

                fontRegistry.Register(fontName.ToLowerInvariant(), regular, bold, italic, boldItalic);
            }

            return fontRegistry;
        }

        // Objective-C runtime interop
        [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib")]
        private static extern IntPtr sel_registerName(string strName);

        [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, byte arg);

        [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, UIntPtr arg);

        [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        // Selectors
        private static readonly IntPtr sel_setTitlebarAppearsTransparent = sel_registerName("setTitlebarAppearsTransparent:");
        private static readonly IntPtr sel_setTitleVisibility = sel_registerName("setTitleVisibility:");
        private static readonly IntPtr sel_setStyleMask = sel_registerName("setStyleMask:");
        private static readonly IntPtr sel_setCollectionBehavior = sel_registerName("setCollectionBehavior:");
        private static readonly IntPtr sel_styleMask = sel_registerName("styleMask");

        // Constants
        private const ulong NSWindowStyleMaskFullSizeContentView = 1 << 15;
        private const ulong NSWindowCollectionBehaviorFullScreenAuxiliary = 1 << 8;
        private const byte NSWindowTitleVisibilityHidden = 1;

        // Lazy-loaded function pointer for glfwGetCocoaWindow (resolved from Silk.NET's native glfw library)
        private static IntPtr _glfwGetCocoaWindowPtr;

        private static unsafe void EnsureGlfwGetCocoaWindow()
        {
            if (_glfwGetCocoaWindowPtr != IntPtr.Zero) return;
            try
            {
                // Determine RID subdirectory: osx-arm64 or osx-x64
                string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
                string baseDir = AppContext.BaseDirectory;
                string libPath = Path.Combine(baseDir, "runtimes", arch, "native", "libglfw.3.dylib");
                if (!File.Exists(libPath))
                {
                    Console.WriteLine($"[macOS] GLFW library not found at {libPath}");
                    return;
                }

                IntPtr lib = NativeLibrary.Load(libPath);
                IntPtr proc = NativeLibrary.GetExport(lib, "glfwGetCocoaWindow");
                _glfwGetCocoaWindowPtr = proc;
                Console.WriteLine("[macOS] Resolved glfwGetCocoaWindow.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[macOS] Failed to resolve glfwGetCocoaWindow: {ex.Message}");
            }
        }

        // Configures native macOS window to use unified title bar (transparent, traffic lights overlay)
        private unsafe void ConfigureMacOSUnifiedTitleBar()
        {
            // Only execute on macOS
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return;
            if (_window == null) return;
            try
            {
                // Resolve glfwGetCocoaWindow from the native GLFW library
                EnsureGlfwGetCocoaWindow();
                if (_glfwGetCocoaWindowPtr == IntPtr.Zero)
                {
                    Console.WriteLine("[macOS] glfwGetCocoaWindow delegate not available.");
                    return;
                }

                // Get the GLFWwindow pointer from the window handle
                IntPtr glfwWindowPtr = _window.Handle;
                if (glfwWindowPtr == IntPtr.Zero)
                {
                    Console.WriteLine("[macOS] Window handle is zero.");
                    return;
                }

                // Get the NSWindow from the GLFW window
                var glfwGetCocoaWindow = (delegate* unmanaged<IntPtr, IntPtr>)_glfwGetCocoaWindowPtr;
                IntPtr nsWindow = glfwGetCocoaWindow(glfwWindowPtr);
                if (nsWindow == IntPtr.Zero)
                {
                    Console.WriteLine("[macOS] glfwGetCocoaWindow returned null.");
                    return;
                }

                // Hide window title (NSWindowTitleVisibilityHidden = 1)
                objc_msgSend(nsWindow, sel_setTitleVisibility, (UIntPtr)NSWindowTitleVisibilityHidden);

                // Make title bar transparent
                objc_msgSend(nsWindow, sel_setTitlebarAppearsTransparent, (UIntPtr)1);

                // Extend content view under title bar
                UIntPtr currentMask = (UIntPtr)objc_msgSend(nsWindow, sel_styleMask);
                UIntPtr newMask = currentMask | (UIntPtr)NSWindowStyleMaskFullSizeContentView;
                objc_msgSend(nsWindow, sel_setStyleMask, newMask);

                // Enable full-screen auxiliary
                objc_msgSend(nsWindow, sel_setCollectionBehavior, (UIntPtr)NSWindowCollectionBehaviorFullScreenAuxiliary);

                Console.WriteLine("[macOS] Unified title bar applied successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[macOS] Unified title bar failed: {ex.Message}");
            }
        }

    }
}