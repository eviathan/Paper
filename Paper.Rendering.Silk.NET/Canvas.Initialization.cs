using Paper.Core.Hooks;
using Paper.Core.Reconciler;
using Paper.Core.VirtualDom;
using Paper.Layout;
using Paper.Rendering.Silk.NET.Text;
using Paper.Rendering.Silk.NET.Utilities;
using Silk.NET.Input;
using Silk.NET.OpenGL;

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

            _reconciler = new Reconciler();

            var prevRequest = RenderScheduler.OnRenderRequested;
            RenderScheduler.OnRenderRequested = () =>
            {
                prevRequest?.Invoke();
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
        }

        private void DisposeResources()
        {
            _inputState.CaretBlinkTimer?.Dispose();
            _inputState.CaretBlinkTimer = null;

            _csxHotReload?.Dispose();
            _csxHotReload = null;

            _inputContext?.Dispose();
            _inputContext = null;

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
    }
}
