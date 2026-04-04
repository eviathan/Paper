using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Paper.Core.Reconciler;
using Paper.Core.VirtualDom;
using Paper.Core.Events;
using Paper.Core.Styles;
using Paper.Core.Hooks;
using Paper.Layout;
using Paper.Rendering.Silk.NET.Text;
using Silk.NET.Input;
using MouseButton = Silk.NET.Input.MouseButton;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;
using Silk.NET.GLFW;
using Paper.Rendering.Silk.NET.Utilities;
using Paper.Rendering.Silk.NET.Models;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// The top-level Paper rendering surface. Creates a window, hosts the reconciler
    /// and layout engine, and renders the UI tree each frame.
    ///
    /// Usage:
    /// <code>
    ///   var surface = new PaperSurface("My App", 1280, 720);
    ///   surface.OnLoad    = (gl, ic, w, h) => host.Initialise(gl, ic, w, h);
    ///   surface.PreRender = dt => host.Tick(dt);
    ///   surface.Mount(() => MyAppComponent(Props.Empty));
    ///   surface.Run();
    /// </code>
    /// </summary>
    public sealed class Canvas : IDisposable
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
        private Reconciler? _reconciler;
        private LayoutEngine? _layout;
        private ILayoutMeasurer? _measurer;
        private IInputContext? _inputContext;
        private CSXHotReload? _csxHotReload;
        private ImageTextureLoader? _imageLoader;
        private FiberRenderer? _renderer;
        private Fiber? _pointerDownFiber;

        private ClickState _clickState { get; set; } = new();
        private GLFWState _glfwState { get; set; } = new();
        private FramebufferState _framBufferState { get; set; } = new();
        private InputState _inputState { get; set; } = new();
        private RenderState _renderState { get; set; } = new();
        private ScrollState _scrollState { get; set; } = new();
        private UIState _uiState { get; set; } = new();

        /// <summary>
        /// When true, Paper reconciles every frame (useful for apps driven by external state).
        /// For production UI, prefer false so updates are event/state driven.
        /// </summary>
        public bool AlwaysRender { get; set; } = false;

        /// <summary>
        /// Global style registry for this surface (typically filled with component-scoped class styles).
        /// </summary>
        public StyleRegistry Styles { get; } = new();

        /// <summary>
        /// Minimum window width in pixels. When set, the window cannot be resized smaller than this (enforced in resize handler).
        /// Null = no minimum (default).
        /// </summary>
        public int? MinimumWindowWidth { get; set; }

        /// <summary>
        /// Minimum window height in pixels. When set, the window cannot be resized smaller than this (enforced in resize handler).
        /// Null = no minimum (default).
        /// </summary>
        public int? MinimumWindowHeight { get; set; }

        // ── Lifecycle hooks ───────────────────────────────────────────────────

        /// <summary>
        /// Called once after the GL context and input context are created.
        /// Use this to initialise the embedded engine host.
        /// </summary>
        public Action<GL, IInputContext, int, int>? OnLoad { get; set; }

        /// <summary>
        /// Called at the start of each render frame, before Paper renders its UI.
        /// Use this to tick the embedded engine so it writes to its game-view FBO.
        /// </summary>
        public Action<double>? PreRender { get; set; }

        public Canvas(string title = "Paper", int width = 1280, int height = 720)
        {
            _title = title;
            _width = width;
            _height = height;
        }
        
        /// <summary>
        /// Releases all GPU and managed resources. Called automatically at the end of <see cref="Run"/>.
        /// Safe to call manually if <see cref="Run"/> was not used (e.g. in tests or headless scenarios).
        /// </summary>
        public void Dispose() => DisposeResources();

        /// <summary>
        /// Set the root component. This should be a function component that takes Props.
        /// </summary>
        public void Mount(Func<Props, UINode> rootComponent)
        {
            _rootFactory = () => new UINode(rootComponent, Props.Empty);
        }

        /// <summary>
        /// Development helper: mount a CSX file and enable hot reload while running.
        /// Compiles CSX into an in-memory component and swaps implementation on file changes.
        /// </summary>
        public void MountCSXHotReload(string csxFilePath, string? scopeId = null)
        {
            var csxPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, $"../../../{csxFilePath}"));
            Console.WriteLine($"Paper.Playground: Loading {csxPath}");

            if (File.Exists(csxPath))
            {
                Console.WriteLine($"Mounting {csxFilePath} hot reload.");
                scopeId ??= Path.GetFileNameWithoutExtension(csxFilePath);

                _csxHotReload?.Dispose();
                _csxHotReload = new CSXHotReload(this, csxFilePath, scopeId);
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

            _window = Window.Create(options);

            // TODO: Add user ability to configure custom event handlers
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

        /// <summary>
        /// Run loop. VSync (enabled above) makes DoRender() block until the next display refresh,
        /// so it naturally paces renders at the display frame rate (60 or 120 Hz).
        /// When nothing needs drawing we sleep 4 ms so the thread yields CPU.
        /// </summary>
        private void RunLoop()
        {
            while (_window != null && !_window.IsClosing)
            {
                _window.DoEvents();

                if (_window.IsClosing)
                    break;
                    
                _window.DoUpdate();

                if (_window.IsClosing)
                    break;

                var utcNow = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
                var isAnimating = utcNow < _renderState.AnimationDeadline;

                if (_renderState.LayoutDirty || isAnimating)
                    _window.DoRender();
                else
                    Thread.Sleep(4);
            }

            _window?.DoEvents();
        }

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

            // TODO: Move this font code into its own class then call it from here
            var fontRegistry = new FontRegistry();
            var fontDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "fonts");

            if (Directory.Exists(fontDirectory))
            {
                foreach (var regularPath in Directory.GetFiles(fontDirectory, "*.ttf"))
                {
                    var fname = Path.GetFileNameWithoutExtension(regularPath);

                    if (fname.EndsWith("-bold", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (fname.EndsWith("-italic", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (fname.EndsWith("-bolditalic", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (fname.EndsWith("bold", StringComparison.OrdinalIgnoreCase) && fname.Length > 4)
                        continue;
                        
                    bool isIconFont = fname.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0;

                    var atlasLoader = isIconFont
                        ? (Func<GL, string, Dictionary<int, PaperFontAtlas>>)((gl, fontPath) => PaperFontLoader.LoadIconSet(gl, fontPath))
                        : (gl, fontPath) => PaperFontLoader.LoadSet(gl, fontPath);

                    var regular = new PaperFontSet(atlasLoader(_gl, regularPath), _gl);
                    
                    PaperFontSet? bold = null;
                    foreach (var suffix in new[] { "-bold", "-Bold", "bold", "Bold" })
                    {
                        var boldPath = Path.Combine(fontDirectory, fname + suffix + ".ttf");
                        if (File.Exists(boldPath))
                        {
                            bold = new PaperFontSet(PaperFontLoader.LoadSet(_gl, boldPath), _gl);
                            break;
                        }
                    }

                    PaperFontSet? italic = null;
                    foreach (var suffix in new[] { "-italic", "-Italic" })
                    {
                        var italicPath = Path.Combine(fontDirectory, fname + suffix + ".ttf");
                        if (File.Exists(italicPath))
                        {
                            italic = new PaperFontSet(PaperFontLoader.LoadSet(_gl, italicPath), _gl);
                            break;
                        }
                    }

                    PaperFontSet? boldItalic = null;
                    foreach (var suffix in new[] { "-bolditalic", "-BoldItalic", "-bold-italic", "-Bold-Italic" })
                    {
                        var boldItalicPath = Path.Combine(fontDirectory, fname + suffix + ".ttf");
                        if (File.Exists(boldItalicPath))
                        {
                            boldItalic = new PaperFontSet(PaperFontLoader.LoadSet(_gl, boldItalicPath), _gl);
                            break;
                        }
                    }

                    fontRegistry.Register(fname.ToLowerInvariant(), regular, bold, italic, boldItalic);
                }
            }

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
                    if (!_scrollState.ScrollbarLastActive.TryGetValue(path, out double t))
                        return 0f;
                        
                    double elapsed = (DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond) - t;
                    const double visible = 1.2, fade = 0.4;

                    if (elapsed < visible)
                        return 1f;

                    if (elapsed >= visible + fade)
                        return 0f;
                        
                    return 1f - (float)((elapsed - visible) / fade);
                },
                GetImageTexture = path =>
                    _imageLoader?.GetOrLoad(PaperUtility.ResolveImagePath(path)).Handle ?? 0,
                GetImageResult = path =>
                {
                    var r = _imageLoader != null ? _imageLoader.GetOrLoad(PaperUtility.ResolveImagePath(path)) : default;
                    return r.Handle != 0 ? (r.Handle, r.Width, r.Height) : (0u, 0, 0);
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

            OnLoad?.Invoke(_gl, inputContext, frameBufferSize.X, frameBufferSize.Y);
        }

        private void OnMouseButtonDown(IMouse mouse, MouseButton button)
        {
            if (_reconciler?.Root == null || _window == null) return;

            if (_renderer != null)
            {
                var (fbx, fby) = PaperUtility.ToLayoutCoords(mouse.Position);
                foreach (var kvp in _renderer.RenderedScrollbars)
                {
                    var sb = kvp.Value;
                    if (fbx >= sb.TrackX && fbx <= sb.TrackX + 6f &&
                        fby >= sb.ThumbY && fby <= sb.ThumbY + sb.ThumbH)
                    {
                        _scrollState.ScrollbarDragPath = kvp.Key;
                        _scrollState.ScrollbarDragAnchorY = fby;
                        _scrollState.ScrollbarDragAnchorScroll = _scrollState.ScrollOffsets.TryGetValue(kvp.Key, out var sv) ? sv.scrollY : 0f;
                        return;
                    }
                }
            }

            var (lx, ly) = PaperUtility.ToLayoutCoords(mouse.Position);
            var target = HitTestAll(lx, ly);
            _uiState.Pressed = target;
            _uiState.PressedPath = target != null ? PaperUtility.GetPathString(target) : null;
            if (button == MouseButton.Left) _pointerDownFiber = target;

            Fiber? dragCandidate = target;
            while (dragCandidate != null && dragCandidate.Props.OnDragStart == null)
                dragCandidate = dragCandidate.Parent;

            if (button == MouseButton.Left && dragCandidate != null)
            {
                _uiState.DragSource = dragCandidate;
                _uiState.DragSourcePath = PaperUtility.GetPathString(dragCandidate);
                _uiState.DragData = null;
                _uiState.DragActive = false;
                _uiState.DragStartX = lx;
                _uiState.DragStartY = ly;
                _uiState.DragOver = null;
            }
            else
            {
                _uiState.DragSource = null;
                _uiState.DragSourcePath = null;
                _uiState.DragActive = false;
            }

            DispatchPointer(target, new PointerEvent
            {
                Type = PointerEventType.Down,
                X = lx,
                Y = ly,
                Button = button == MouseButton.Left ? 0 : button == MouseButton.Right ? 1 : 2,
            });

            Fiber? focusTarget = PaperUtility.GetInputAncestorOrSelf(target) ?? target;
            if (PaperUtility.IsFocusable(focusTarget))
            {
                SetFocus(focusTarget);
                if (focusTarget != null && focusTarget.Type is string ft && PaperUtility.IsTextInput(ft))
                {
                    _inputState.LastInputActivityTicks = Environment.TickCount64;
                    StartCaretBlinkTimer();
                    var (scrollX, scrollY) = GetTotalScrollForPath(_inputState.FocusedPath ?? "");
                    float inputScroll = (focusTarget == _inputState.Focused && focusTarget.Type is string it && PaperUtility.IsTextInput(it)) ? _inputState.InputScrollX : 0f;
                    int idx = GetCaretIndexFromX(focusTarget, lx, ly, scrollX, scrollY, inputScroll);
                    _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = _inputState.InputSelAnchor = idx;
                    _inputState.InputSelecting = true;
                    RequestRender();
                }
            }
        }
        
        private void DispatchDrag(Fiber? target, DragEvent e)
        {
            if (target == null) return;
            var path = PaperUtility.PathToRoot(target);
            for (int i = path.Count - 1; i >= 0; i--)
            {
                var fiber = path[i];
                var handler = e.Type switch
                {
                    DragEventType.DragStart => fiber.Props.OnDragStart,
                    DragEventType.Drag => fiber.Props.OnDrag,
                    DragEventType.DragEnd => fiber.Props.OnDragEnd,
                    DragEventType.DragEnter => fiber.Props.OnDragEnter,
                    DragEventType.DragOver => fiber.Props.OnDragOver,
                    DragEventType.DragLeave => fiber.Props.OnDragLeave,
                    DragEventType.Drop => fiber.Props.OnDrop,
                    _ => null,
                };
                handler?.Invoke(e);
            }
        }

        private void OnMouseButtonUp(IMouse mouse, MouseButton button)
        {
            if (_reconciler?.Root == null || _window == null) return;

            if (_scrollState.ScrollbarDragPath != null) { _scrollState.ScrollbarDragPath = null; return; }

            var (lx, ly) = PaperUtility.ToLayoutCoords(mouse.Position);
            var target = HitTestAll(lx, ly);

            // Finish drag-and-drop
            if (button == MouseButton.Left && _uiState.DragActive && _uiState.DragSource != null)
            {
                var dropEvt = new DragEvent { Type = DragEventType.Drop, X = lx, Y = ly, Data = _uiState.DragData };
                DispatchDrag(target, dropEvt);

                var endEvt = new DragEvent { Type = DragEventType.DragEnd, X = lx, Y = ly, Data = _uiState.DragData };
                DispatchDrag(_uiState.DragSource, endEvt);

                if (_uiState.DragOver != null)
                {
                    DispatchDrag(_uiState.DragOver, new DragEvent { Type = DragEventType.DragLeave, X = lx, Y = ly, Data = _uiState.DragData });
                    _uiState.DragOver = null;
                }
                _uiState.DragSource = null;
                _uiState.DragSourcePath = null;
                _uiState.DragActive = false;
                _uiState.DragData = null;
            }
            else if (button == MouseButton.Left)
            {
                _uiState.DragSource = null;
                _uiState.DragSourcePath = null;
                _uiState.DragActive = false;
            }

            // If the mouse was released somewhere other than the originally-pressed fiber,
            // dispatch PointerUp to that fiber too so handlers like OnTrackUp always fire.
            if (button == MouseButton.Left && _pointerDownFiber != null && !ReferenceEquals(_pointerDownFiber, target))
            {
                DispatchPointer(_pointerDownFiber, new PointerEvent
                {
                    Type = PointerEventType.Up,
                    X = lx,
                    Y = ly,
                    Button = 0,
                });
            }
            if (button == MouseButton.Left) _pointerDownFiber = null;

            DispatchPointer(target, new PointerEvent
            {
                Type = PointerEventType.Up,
                X = lx,
                Y = ly,
                Button = button == MouseButton.Left ? 0 : button == MouseButton.Right ? 1 : 2,
            });

            // Click if press+release on same control. Use path so we still match after the tree is replaced by a reconcile (new fiber instances).
            bool sameControl = target != null && _uiState.PressedPath != null && PaperUtility.GetPathString(target) == _uiState.PressedPath;
            if (button == MouseButton.Left && target != null && sameControl)
            {
                var now = DateTime.UtcNow;
                bool isDouble = (now - _clickState.LastClickAtUtc).TotalMilliseconds < 350;
                _clickState.LastClickAtUtc = now;
                if (!isDouble)
                    _clickState.LastClickWasDoubleOnInput = false;

                DispatchPointer(target, new PointerEvent
                {
                    Type = isDouble ? PointerEventType.DoubleClick : PointerEventType.Click,
                    X = lx,
                    Y = ly,
                    Button = 0,
                });

                // Double-click in Input/Textarea: macOS-style — first double-click selects word, second (triple-click) selects all.
                if (isDouble && _inputState.Focused != null && _inputState.FocusedPath != null)
                {
                    Fiber? inputFiber = PaperUtility.GetInputAncestorOrSelf(target);
                    if (inputFiber != null && PaperUtility.GetPathString(inputFiber) == _inputState.FocusedPath &&
                        inputFiber.Type is string fit && PaperUtility.IsTextInput(fit))
                    {
                        var cur = _inputState.InputText ?? inputFiber.Props?.Text ?? "";
                        int len = cur.Length;
                        bool sameInputAsLastDouble = _clickState.LastClickWasDoubleOnInput && _clickState.LastDoubleClickInputPath == _inputState.FocusedPath &&
                            (now - _clickState.LastDoubleClickAtUtc).TotalMilliseconds < 350;
                        if (sameInputAsLastDouble)
                        {
                            // Triple-click: select all
                            _inputState.InputSelStart = 0;
                            _inputState.InputSelEnd = len;
                            _inputState.InputCaret = len;
                        }
                        else
                        {
                            // Double-click: select word at caret (set by mousedown of second click)
                            var (wStart, wEnd) = PaperUtility.GetWordBounds(cur, _inputState.InputCaret);
                            _inputState.InputSelStart = wStart;
                            _inputState.InputSelEnd = wEnd;
                            _inputState.InputCaret = wEnd;
                        }
                        _inputState.LastInputActivityTicks = Environment.TickCount64;
                        _clickState.LastClickWasDoubleOnInput = true;
                        _clickState.LastDoubleClickInputPath = _inputState.FocusedPath;
                        _clickState.LastDoubleClickAtUtc = now;
                    }
                    else
                    {
                        _clickState.LastClickWasDoubleOnInput = false;
                    }
                }
                else
                {
                    _clickState.LastClickWasDoubleOnInput = false;
                }

                // Use target from current tree (has correct Props/OnClick); _pressed may be from old tree.
                Fiber? clickTarget = PaperUtility.GetClickTarget(target);
                if (clickTarget != null)
                {
                    try
                    {
                        if (isDouble && clickTarget.Props.OnDoubleClick != null)
                            clickTarget.Props.OnDoubleClick.Invoke();
                        else if (clickTarget.Type is string ct && ct == ElementTypes.Checkbox && clickTarget.Props.OnCheckedChange != null)
                            clickTarget.Props.OnCheckedChange(!clickTarget.Props.Checked);
                        else
                            clickTarget.Props.OnClick?.Invoke();
                        if (_window != null)
                            _window.Title = _title + " — clicked";
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[Paper] Click handler error: " + ex.ToString());
                    }
                    RequestRender();
                }
            }

            _uiState.Pressed = null;
            _uiState.PressedPath = null;
            _inputState.InputSelecting = false;
        }

        /// <summary>Total scroll offset affecting a node (sum of ancestor path scrolls).</summary>
        private (float scrollX, float scrollY) GetTotalScrollForPath(string path)
        {
            float sx = 0f, sy = 0f;
            if (string.IsNullOrEmpty(path)) return (sx, sy);
            var parts = path.Split('.');
            for (int i = 1; i < parts.Length; i++) // proper prefixes only (not self)
            {
                var prefix = string.Join(".", parts, 0, i);
                if (_scrollState.ScrollOffsets.TryGetValue(prefix, out var v)) { sx += v.scrollX; sy += v.scrollY; }
            }
            return (sx, sy);
        }

        /// <summary>Character index in the input's text for the given layout x/y (e.g. from mouse). Uses content area and scroll.</summary>
        private int GetCaretIndexFromX(Fiber fiber, float lx, float ly, float scrollX = 0f, float scrollY = 0f, float inputScrollX = 0f)
        {
            if (_text == null) return 0;
            var style = fiber.ComputedStyle;
            var lb = fiber.Layout;
            float padLeft = (style.Padding ?? Thickness.Zero).Left.Resolve(lb.Width);
            float padTop = (style.Padding ?? Thickness.Zero).Top.Resolve(lb.Height);
            float contentLeft = lb.AbsoluteX - scrollX + padLeft - inputScrollX;
            float contentTop = lb.AbsoluteY - scrollY + padTop;
            float contentX = lx - contentLeft;
            string text = fiber.Props?.Text ?? "";
            if (text.Length == 0) return 0;
            var atlas = _text.Atlas;
            float baseSize = atlas.BaseSize > 0 ? atlas.BaseSize : 16f;
            float fontPx = style.FontSize is { } fs && !fs.IsAuto ? fs.Resolve(baseSize) : baseSize;
            float scale = baseSize > 0 ? fontPx / baseSize : 1f;
            float lineHeight = (_fontSet?.LineHeight(fontPx) ?? baseSize) * Math.Max(0.5f, style.LineHeight ?? 1.4f);
            float contentWidth = lb.Width - padLeft - (style.Padding ?? Thickness.Zero).Right.Resolve(lb.Width);
            if (contentWidth <= 0) contentWidth = lb.Width;

            string? fiberType = fiber.Type as string;
            bool isMultiline = fiberType == ElementTypes.Textarea || fiberType == ElementTypes.MarkdownEditor;

            if (isMultiline)
            {
                float contentY = ly - contentTop;
                if (contentY < 0) return 0;
                var logicalLines = text.Split('\n');
                int charOffset = 0;
                float y = 0f;
                for (int li = 0; li < logicalLines.Length; li++)
                {
                    var logLine = logicalLines[li];
                    var wrapped = WrapTextLineForCaret(logLine, charOffset, contentWidth, fontPx);
                    float lineTop = y;
                    float lineBottom = y + lineHeight;
                    foreach (var seg in wrapped)
                    {
                        if (contentY >= lineTop && contentY < lineBottom)
                        {
                            float segX = contentX;
                            if (segX <= 0) return seg.Start;
                            float segW = _text.MeasureWidth(seg.Text.AsSpan()) * scale;
                            if (segX >= segW) return seg.End;
                            for (int i = 0; i < seg.Text.Length; i++)
                            {
                                float wCur = _text.MeasureWidth(seg.Text.AsSpan(0, i + 1)) * scale;
                                if (segX < wCur)
                                {
                                    float wPrev = i > 0 ? _text.MeasureWidth(seg.Text.AsSpan(0, i)) * scale : 0;
                                    return seg.Start + (segX < (wPrev + wCur) / 2f ? i : i + 1);
                                }
                            }
                            return seg.End;
                        }
                    }
                    y += lineHeight;
                    charOffset += logLine.Length + 1;
                }
                return text.Length;
            }

            if (contentX <= 0) return 0;
            float totalW = _text.MeasureWidth(text.AsSpan()) * scale;
            if (contentX >= totalW) return text.Length;
            for (int i = 0; i < text.Length; i++)
            {
                float wCur = _text.MeasureWidth(text.AsSpan(0, i + 1)) * scale;
                if (contentX < wCur)
                {
                    float wPrev = i > 0 ? _text.MeasureWidth(text.AsSpan(0, i)) * scale : 0;
                    return contentX < (wPrev + wCur) / 2f ? i : i + 1;
                }
            }
            return text.Length;
        }

        /// <summary>Word-wrap a single line of text for caret calculation. Returns sub-lines with char offsets.</summary>
        private List<(string Text, int Start, int End)> WrapTextLineForCaret(string line, int offset, float maxWidth, float fontPx = 16f)
        {
            var result = new List<(string, int, int)>();

            if (_fontSet == null || maxWidth <= 0 || line.Length == 0)
            {
                result.Add((line, offset, offset + line.Length));
                return result;
            }

            int start = 0;
            while (start < line.Length)
            {
                float width = 0f;
                int end = start;
                int lastSpace = -1;

                while (end < line.Length)
                {
                    char character = line[end];
                    float characterWidth = _fontSet.MeasureWidth(line.AsSpan(end, 1), fontPx);

                    if (width + characterWidth > maxWidth && end > start)
                        break;

                    if (character == ' ')
                        lastSpace = end;

                    width += characterWidth;
                    end++;
                }

                if (end == line.Length)
                {
                    result.Add((line[start..], offset + start, offset + line.Length));
                    break;
                }

                int wrapAt = lastSpace > start ? lastSpace + 1 : end;

                result.Add((line[start..wrapAt].TrimEnd(), offset + start, offset + wrapAt));
                start = wrapAt;
            }

            if (result.Count == 0)
                result.Add(("", offset, offset));

            return result;
        }
        
        private void OnMouseMove(IMouse mouse, Vector2 position)
        {
            if (_reconciler?.Root == null) return;

            // Handle scrollbar thumb drag
            if (_scrollState.ScrollbarDragPath != null && mouse.IsButtonPressed(MouseButton.Left))
            {
                if (_renderer != null && _renderer.RenderedScrollbars.TryGetValue(_scrollState.ScrollbarDragPath, out var sb))
                {
                    var (_, fby) = PaperUtility.ToLayoutCoords(position);
                    float delta = fby - _scrollState.ScrollbarDragAnchorY;
                    float trackUsable = sb.TrackH - sb.ThumbH;
                    // All values (delta, MaxScroll, _scrollState.ScrollOffsets) are in layout/fb space — no scale conversion needed
                    float scrollDelta = trackUsable > 0 ? delta * sb.MaxScroll / trackUsable : 0f;
                    float newScroll = Math.Max(0f, Math.Min(sb.MaxScroll, _scrollState.ScrollbarDragAnchorScroll + scrollDelta));
                    var (cx, _) = _scrollState.ScrollOffsets.TryGetValue(_scrollState.ScrollbarDragPath, out var cv) ? cv : (0f, 0f);
                    _scrollState.ScrollOffsets[_scrollState.ScrollbarDragPath] = (cx, newScroll);
                    _scrollState.ScrollbarLastActive[_scrollState.ScrollbarDragPath] = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
                    MarkDirty(animationSeconds: 2.0);
                }
                return;
            }

            var (layoutCoordsX, layoutCoordsY) = PaperUtility.ToLayoutCoords(position);
            var target = HitTestAll(layoutCoordsX, layoutCoordsY);

            // During an active drag don't process hover changes — style recalc on every cell hover
            // causes full layout passes each frame and makes dragging feel sluggish.
            if (!_uiState.DragActive && !ReferenceEquals(target, _uiState.Hovered))
            {
                // Leave old
                if (_uiState.Hovered != null)
                {
                    DispatchPointer(_uiState.Hovered, new PointerEvent { Type = PointerEventType.Leave, X = layoutCoordsX, Y = layoutCoordsY, Button = -1 });
                    _uiState.Hovered.Props.OnMouseLeave?.Invoke(); // back-compat
                }

                // Enter new
                if (target != null)
                {
                    DispatchPointer(target, new PointerEvent { Type = PointerEventType.Enter, X = layoutCoordsX, Y = layoutCoordsY, Button = -1 });
                    target.Props.OnMouseEnter?.Invoke(); // back-compat
                }

                _uiState.Hovered = target;
                _uiState.HoveredPath = target != null ? PaperUtility.GetPathString(target) : null;
                ApplyGlfwCursor(target?.ComputedStyle.Cursor ?? Paper.Core.Styles.Cursor.Default);
                MarkDirty(); // hover state affects :hover styles
            }
            else if (_uiState.DragActive)
            {
                // Still update cursor and mark dirty for ghost repaint, but skip hover dispatch.
                ApplyGlfwCursor(target?.ComputedStyle.Cursor ?? Paper.Core.Styles.Cursor.Default);
                MarkDirty();
            }

            // If a button is held, route moves to the originally-pressed fiber so elements
            // like sliders keep responding even when the cursor leaves their bounds.
            if (mouse.IsButtonPressed(MouseButton.Left) && _pointerDownFiber != null)
            {
                var layoutBox = _pointerDownFiber.Layout;

                var capturedEvent = new PointerEvent
                {
                    Type = PointerEventType.Move,
                    X = layoutCoordsX,
                    Y = layoutCoordsY,
                    Button = 0,
                    LocalX = layoutCoordsX - layoutBox.AbsoluteX,
                    LocalY = layoutCoordsY - layoutBox.AbsoluteY
                };

                DispatchPointer(_pointerDownFiber, capturedEvent);
            }
            else if (target != null)
            {
                DispatchPointer(target, new PointerEvent { Type = PointerEventType.Move, X = layoutCoordsX, Y = layoutCoordsY, Button = -1 });
            }

            // Drag-and-drop: start drag once threshold crossed, then fire Drag + DragEnter/Leave/Over
            if (_uiState.DragSource != null && mouse.IsButtonPressed(MouseButton.Left))
            {
                const float DragThreshold = 4f;
                float dragStartX = layoutCoordsX - _uiState.DragStartX;
                float dragStartY = layoutCoordsY - _uiState.DragStartY;

                if (!_uiState.DragActive && (dragStartX * dragStartX + dragStartY * dragStartY) >= DragThreshold * DragThreshold)
                {
                    _uiState.DragActive = true;

                    var startEvt = new DragEvent
                    {
                        Type = DragEventType.DragStart,
                        X = layoutCoordsX,
                        Y = layoutCoordsY
                    };

                    DispatchDrag(_uiState.DragSource, startEvt);
                    _uiState.DragData = startEvt.Data; // source handler may set Data via a wrapper — see note
                }

                if (_uiState.DragActive)
                {
                    _uiState.DragCursorX = layoutCoordsX;
                    _uiState.DragCursorY = layoutCoordsY;
                    // Only dispatch Drag event if source actually has an OnDrag handler (avoids PathToRoot walk every frame).
                    if (_uiState.DragSource!.Props.OnDrag != null)
                        DispatchDrag(_uiState.DragSource, new DragEvent { Type = DragEventType.Drag, X = layoutCoordsX, Y = layoutCoordsY, Data = _uiState.DragData });

                    if (!ReferenceEquals(target, _uiState.DragOver))
                    {
                        if (_uiState.DragOver != null)
                            DispatchDrag(_uiState.DragOver, new DragEvent { Type = DragEventType.DragLeave, X = layoutCoordsX, Y = layoutCoordsY, Data = _uiState.DragData });
                        if (target != null)
                            DispatchDrag(target, new DragEvent { Type = DragEventType.DragEnter, X = layoutCoordsX, Y = layoutCoordsY, Data = _uiState.DragData });
                        _uiState.DragOver = target;
                    }
                    else if (target != null)
                    {
                        DispatchDrag(target, new DragEvent { Type = DragEventType.DragOver, X = layoutCoordsX, Y = layoutCoordsY, Data = _uiState.DragData });
                    }
                }
            }

            // Mouse selection: extend selection while dragging (including when mouse leaves the input)
            if (_inputState.InputSelecting && _inputState.FocusedPath != null && mouse.IsButtonPressed(MouseButton.Left) &&
                _inputState.Focused != null && _inputState.Focused.Type is string fm && PaperUtility.IsTextInput(fm))
            {
                var (scrollX, scrollY) = GetTotalScrollForPath(_inputState.FocusedPath);
                Fiber? inputFiber = (target != null && PaperUtility.GetPathString(target) == _inputState.FocusedPath) ? target : _inputState.Focused;
                float layoutCoordsXClamp = layoutCoordsX;
                float layoutCoordsYClamp = layoutCoordsY;
                var layout = _inputState.Focused.Layout;
                float left = (layout.AbsoluteX - scrollX);
                float right = left + layout.Width;
                float top = layout.AbsoluteY - scrollY;
                float bottom = top + layout.Height;

                if (layoutCoordsXClamp < left)
                    layoutCoordsXClamp = left;

                if (layoutCoordsXClamp > right)
                    layoutCoordsXClamp = right;

                if (layoutCoordsYClamp < top)
                    layoutCoordsYClamp = top;

                if (layoutCoordsYClamp > bottom)
                    layoutCoordsYClamp = bottom;

                float inputScroll = PaperUtility.IsTextInput(fm) ? _inputState.InputScrollX : 0f;
                int idx = GetCaretIndexFromX(_inputState.Focused, layoutCoordsXClamp, layoutCoordsYClamp, scrollX, scrollY, inputScroll);

                _inputState.InputCaret = idx;
                _inputState.InputSelStart = Math.Min(_inputState.InputSelAnchor, idx);
                _inputState.InputSelEnd = Math.Max(_inputState.InputSelAnchor, idx);

                RequestRender();
            }
        }

        private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
        {
            if (_reconciler?.Root == null) return;

            var (lx, ly) = PaperUtility.ToLayoutCoords(mouse.Position);
            var target = HitTestAll(lx, ly);
            if (target == null) return;

            var evt = new PointerEvent
            {
                Type = PointerEventType.Wheel,
                X = lx,
                Y = ly,
                WheelDeltaX = wheel.X,
                WheelDeltaY = wheel.Y,
            };

            // Walk innermost→outermost. Fire onWheel prop if present (stops bubbling, like a
            // component-managed scroll e.g. VirtualList). Otherwise fall through to the first
            // overflow:scroll/auto container and update _scrollOffsets there.
            var pathToRoot = PaperUtility.PathToRoot(target);
            for (int i = pathToRoot.Count - 1; i >= 0; i--)
            {
                var node = pathToRoot[i];

                // Component-managed scroll (e.g. VirtualList sets onWheel on its container).
                // Firing it handles the scroll internally — don't also move the page.
                if (node.Props?.OnWheel != null)
                {
                    node.Props.OnWheel(evt);
                    return; // consumed
                }

                var style = node.ComputedStyle;
                if (style.OverflowY == Overflow.Scroll || style.OverflowY == Overflow.Auto ||
                    style.OverflowX == Overflow.Scroll || style.OverflowX == Overflow.Auto)
                {
                    string key = string.Join(".", pathToRoot.Take(i + 1).Select(f => f.Index));
                    var (sx, sy) = _scrollState.ScrollOffsets.TryGetValue(key, out var v) ? v : (0f, 0f);
                    const float step = 24f;
                    // Invert so scroll-down (negative Y) increases scroll offset (content moves up)
                    float newSx = Math.Max(0, sx - (float)wheel.X * step);
                    float newSy = Math.Max(0, sy - (float)wheel.Y * step);
                    // Clamp to content bounds (skip if renderer hasn't recorded geometry yet)
                    if (_renderer != null && _renderer.RenderedScrollbars.TryGetValue(key, out var sb))
                    {
                        newSy = Math.Min(newSy, sb.MaxScroll);
                        newSx = Math.Min(newSx, sb.MaxScrollX);
                    }
                    _scrollState.ScrollOffsets[key] = (newSx, newSy);
                    // Record activity for scrollbar fade-in/out (visible 1.2s + fade 0.4s = 1.6s)
                    _scrollState.ScrollbarLastActive[key] = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
                    MarkDirty(animationSeconds: 2.0); // keep rendering for scrollbar fade
                    break;
                }
            }
        }

        private void OnKeyDown(IKeyboard keyboard, Key key, int _)
        {
            string ks = key.ToString();
            bool ctrl = keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight);
            bool cmd = keyboard.IsKeyPressed(Key.SuperLeft) || keyboard.IsKeyPressed(Key.SuperRight); // Cmd on macOS
            bool alt = keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight); // Option on macOS
            bool shortcutMod = ctrl || cmd; // Copy/Paste/Select All: Ctrl on Windows/Linux, Cmd on macOS
            bool shift = keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight);

            // Dispatch to global keyboard shortcuts first
            if (KeyboardShortcutRegistry.TryDispatch(ks, ctrl, alt, shift, cmd, out bool shortcutHandled) && shortcutHandled)
                return;

            var target = _inputState.Focused;
            if (target == null) return;

            // Tab / Shift+Tab: cycle focus between all focusable elements in proper tab order.
            if (key == Key.Tab && _reconciler?.Root != null)
            {
                var all = new List<Fiber>();
                PaperUtility.CollectFocusable(_reconciler.Root, all);

                // Separate into groups: positive tabIndex (sorted), tabIndex=0, tabIndex=-1 (not in tab order)
                var positiveTabIndex = all.Where(f => (f.Props.TabIndex ?? 0) > 0).OrderBy(f => f.Props.TabIndex).ToList();
                var zeroTabIndex = all.Where(f => f.Props.TabIndex == 0 || f.Props.TabIndex == null).ToList();
                var negativeTabIndex = all.Where(f => f.Props.TabIndex == -1).ToList();

                // Combine in proper tab order: positive first, then zero, then skip -1
                var ordered = new List<Fiber>();
                ordered.AddRange(positiveTabIndex);
                ordered.AddRange(zeroTabIndex);

                if (ordered.Count > 0)
                {
                    int cur = all.FindIndex(f => ReferenceEquals(f, _inputState.Focused));
                    int next = shift
                        ? (cur <= 0 ? ordered.Count - 1 : cur - 1)
                        : (cur < 0 || cur >= ordered.Count - 1 ? 0 : cur + 1);
                    var nextFiber = ordered[next];
                    SetFocus(nextFiber);
                    // Select all text in the newly focused input
                    if (_inputState.InputText != null)
                    {
                        _inputState.InputSelStart = 0;
                        _inputState.InputSelEnd = _inputState.InputText.Length;
                        _inputState.InputCaret = _inputState.InputText.Length;
                    }
                }
                return;
            }

            // Input/Textarea: navigation, Delete, and clipboard shortcuts (use _inputText so rapid keys don't read stale Props)
            if (target.Type is string t && PaperUtility.IsTextInput(t))
            {
                // Allow navigation even in readOnly/disabled, but block modification
                bool canModify = !target.Props.ReadOnly && !target.Props.Disabled;
                _inputState.LastInputActivityTicks = Environment.TickCount64;
                var cur = _inputState.InputText ?? target.Props.Text ?? "";
                PaperUtility.ClampInputIndices(cur.Length, ref _inputState.InputCaret, ref _inputState.InputSelStart, ref _inputState.InputSelEnd);
                int len = cur.Length;
                bool handled = true;

                if (key == Key.Left)
                {
                    int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
                    int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
                    int dest;
                    if (cmd) dest = 0;                           // Cmd+Left = Home
                    else if (alt) dest = PaperUtility.WordStartBefore(cur, _inputState.InputCaret); // Option+Left = word left
                    else dest = _inputState.InputCaret > 0 ? _inputState.InputCaret - 1 : 0;
                    // Without shift: if selection exists, collapse to its start; otherwise move normally
                    if (!shift && selMin != selMax) dest = selMin;
                    _inputState.InputCaret = dest;
                    if (shift) _inputState.InputSelEnd = dest; else _inputState.InputSelStart = _inputState.InputSelEnd = dest;
                }
                else if (key == Key.Right)
                {
                    int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
                    int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
                    int dest;
                    if (cmd) dest = len;                          // Cmd+Right = End
                    else if (alt) dest = PaperUtility.WordEndAfter(cur, _inputState.InputCaret); // Option+Right = word right
                    else dest = _inputState.InputCaret < len ? _inputState.InputCaret + 1 : len;
                    // Without shift: if selection exists, collapse to its end; otherwise move normally
                    if (!shift && selMin != selMax) dest = selMax;
                    _inputState.InputCaret = dest;
                    if (shift) _inputState.InputSelEnd = dest; else _inputState.InputSelStart = _inputState.InputSelEnd = dest;
                }
                else if (key == Key.Up && PaperUtility.IsMultiLineInput(t) && !cmd)
                {
                    int dest = PaperUtility.CaretUpLine(cur, _inputState.InputCaret);
                    _inputState.InputCaret = dest;
                    if (shift) _inputState.InputSelEnd = dest; else _inputState.InputSelStart = _inputState.InputSelEnd = dest;
                }
                else if (key == Key.Down && PaperUtility.IsMultiLineInput(t) && !cmd)
                {
                    int dest = PaperUtility.CaretDownLine(cur, _inputState.InputCaret);
                    _inputState.InputCaret = dest;
                    if (shift) _inputState.InputSelEnd = dest; else _inputState.InputSelStart = _inputState.InputSelEnd = dest;
                }
                else if (key == Key.Enter && PaperUtility.IsMultiLineInput(t))
                {
                    if (canModify && target.Props.OnChange != null)
                    {
                        int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
                        int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
                        string nextText = cur[..selMin] + "\n" + cur[selMax..];
                        int nextCaret = selMin + 1;
                        _inputState.InputText = nextText;
                        target.Props.OnChange(nextText);
                        _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret;
                    }
                }
                else if (key == Key.Home || (cmd && key == Key.Up))
                {
                    if (shift) _inputState.InputSelEnd = 0; else _inputState.InputSelStart = _inputState.InputSelEnd = 0;
                    _inputState.InputCaret = 0;
                }
                else if (key == Key.End || (cmd && key == Key.Down))
                {
                    if (shift) _inputState.InputSelEnd = len; else _inputState.InputSelStart = _inputState.InputSelEnd = len;
                    _inputState.InputCaret = len;
                }
                else if (key == Key.Backspace || ks.Equals("Backspace", StringComparison.OrdinalIgnoreCase))
                {
                    if (canModify && target.Props.OnChange != null)
                    {
                        int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
                        int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
                        string nextText = cur;
                        int nextCaret = _inputState.InputCaret;
                        if (selMin != selMax)
                        {
                            nextText = cur[..selMin] + cur[selMax..];
                            nextCaret = selMin;
                        }
                        else if (alt && _inputState.InputCaret > 0)
                        {
                            // Option+Backspace = delete word before caret
                            int wordStart = PaperUtility.WordStartBefore(cur, _inputState.InputCaret);
                            nextText = cur[..wordStart] + cur[_inputState.InputCaret..];
                            nextCaret = wordStart;
                        }
                        else if (_inputState.InputCaret > 0)
                        {
                            nextText = cur[..(_inputState.InputCaret - 1)] + cur[_inputState.InputCaret..];
                            nextCaret = _inputState.InputCaret - 1;
                        }
                        if (nextText != cur) { _inputState.InputText = nextText; target.Props.OnChange(nextText); _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret; }
                    }
                    handled = true;
                }
                else if (key == Key.Delete || ks.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                {
                    if (canModify && target.Props.OnChange != null)
                    {
                        int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
                        int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
                        string nextText = cur;
                        int nextCaret = _inputState.InputCaret;
                        if (selMin != selMax)
                        {
                            nextText = cur[..selMin] + cur[selMax..];
                            nextCaret = selMin;
                        }
                        else if (_inputState.InputCaret < len)
                        {
                            nextText = cur[.._inputState.InputCaret] + cur[(_inputState.InputCaret + 1)..];
                            nextCaret = _inputState.InputCaret;
                        }
                        if (nextText != cur) { _inputState.InputText = nextText; target.Props.OnChange(nextText); _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret; }
                    }
                    handled = true;
                }
                else if (shortcutMod && key == Key.A)
                {
                    _inputState.InputSelStart = 0; _inputState.InputSelEnd = len; _inputState.InputCaret = len;
                }
                else if (shortcutMod && key == Key.C)
                {
                    int a = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd), b = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
                    if (len > 0 && a < b) { keyboard.ClipboardText = cur[a..b]; target.Props.OnCopy?.Invoke(cur[a..b]); }
                }
                else if (shortcutMod && key == Key.X)
                {
                    if (canModify && target.Props.OnChange != null)
                    {
                        int a = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd), b = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
                        if (a < b)
                        {
                            string cutText = cur[a..b];
                            keyboard.ClipboardText = cutText;
                            target.Props.OnCut?.Invoke(cutText);
                            var after = cur[..a] + cur[b..]; _inputState.InputText = after; target.Props.OnChange(after); _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = a;
                        }
                    }
                }
                else if (shortcutMod && key == Key.V)
                {
                    if (canModify && target.Props.OnChange != null)
                    {
                        var paste = keyboard.ClipboardText ?? "";
                        if (paste.Length > 0)
                        {
                            int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
                            int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
                            int maxLen = target.Props.MaxLength ?? int.MaxValue;
                            int insertLen = cur.Length - (selMax - selMin) + paste.Length;
                            if (insertLen > maxLen)
                            {
                                int available = maxLen - (cur.Length - (selMax - selMin));
                                if (available > 0) paste = paste[..available];
                                else paste = "";
                            }
                            if (paste.Length > 0)
                            {
                                string nextText = cur[..selMin] + paste + cur[selMax..];
                                int nextCaret = selMin + paste.Length;
                                _inputState.InputText = nextText;
                                target.Props.OnChange(nextText);
                                target.Props.OnPaste?.Invoke(paste);
                                _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret;
                            }
                        }
                    }
                }
                else
                    handled = false;

                // Per-component dirty tracking (from OnChange calls above) handles reconcile.
                // For pure navigation (no text change), mark dirty so the caret repaints this frame
                // rather than waiting up to 500ms for the blink timer to fire RequestRender().
                if (handled) { MarkDirty(); return; }
            }

            // Back-compat
            target.Props.OnKeyDown?.Invoke(ks);
            DispatchKey(target, new KeyEvent { Type = KeyEventType.Down, Key = ks });
        }

        private void OnKeyUp(IKeyboard keyboard, Key key, int _)
        {
            var target = _inputState.Focused;
            if (target == null) return;

            string ks = key.ToString();

            // Back-compat
            target.Props.OnKeyUp?.Invoke(ks);

            DispatchKey(target, new KeyEvent { Type = KeyEventType.Up, Key = ks });
        }

        private void OnKeyChar(IKeyboard keyboard, char c)
        {
            var target = _inputState.Focused;
            if (target == null) return;

            DispatchKey(target, new KeyEvent { Type = KeyEventType.Char, Key = c.ToString(), Char = c });

            if (target.Type is not string t || !PaperUtility.IsTextInput(t) || target.Props.OnChange == null)
                return;

            // Check readOnly and disabled states
            if (target.Props.ReadOnly || target.Props.Disabled)
                return;

            _inputState.LastInputActivityTicks = Environment.TickCount64;

            var cur = _inputState.InputText ?? target.Props.Text ?? "";
            PaperUtility.ClampInputIndices(cur.Length, ref _inputState.InputCaret, ref _inputState.InputSelStart, ref _inputState.InputSelEnd);
            int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
            int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
            int maxLen = target.Props.MaxLength ?? int.MaxValue;

            // Backspace is handled in OnKeyDown to avoid double-delete when both KeyDown and KeyChar fire
            if (c == '\b') return;

            // Validate input type
            string? inputType = target.Props.InputType;
            bool isNumberInput = inputType == "number";

            if (PaperUtility.IsMultiLineInput(t) && (c == '\n' || c == '\r'))
            {
                string insert = "\n";
                int insertLen = cur.Length - (selMax - selMin) + insert.Length;
                if (insertLen > maxLen) return;
                string nextText = cur[..selMin] + insert + cur[selMax..];
                int nextCaret = selMin + insert.Length;
                _inputState.InputText = nextText;
                target.Props.OnChange(nextText);
                _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret;
            }
            else if (!char.IsControl(c))
            {
                // For number inputs, only allow digits, minus sign, and decimal point
                if (isNumberInput && !char.IsDigit(c) && c != '-' && c != '.' && c != ',')
                    return;
                // For number inputs, only allow one minus sign at the start
                if (isNumberInput && c == '-' && selMin > 0)
                    return;
                // For number inputs, only allow one decimal point
                if ((c == '.' || c == ',') && cur.Contains('.') && cur.Contains(','))
                    return;
                string insert = c.ToString();
                int insertLen = cur.Length - (selMax - selMin) + insert.Length;
                if (insertLen > maxLen) return;
                string nextText = cur[..selMin] + insert + cur[selMax..];
                int nextCaret = selMin + insert.Length;
                _inputState.InputText = nextText;
                target.Props.OnChange(nextText);
                _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret;
            }
            // Per-component dirty tracking (from OnChange) handles reconcile.
            // No explicit RequestRender needed — 60fps loop handles visual refresh.
        }

        /// <summary>
        /// Hit-test the main tree then portal roots (portals render on top and win).
        /// </summary>
        private Fiber? HitTestAll(float x, float y)
        {
            Func<string, (float, float)> getScroll = p => _scrollState.ScrollOffsets.TryGetValue(p, out var v) ? v : (0f, 0f);
            var target = PaperUtility.HitTest(_reconciler?.Root, x, y, "", 0, 0f, 0f, getScroll);
            if (_reconciler?.PortalRoots is { Count: > 0 } portals)
            {
                foreach (var portal in portals)
                {
                    var hit = PaperUtility.HitTest(portal, x, y, "", 0, 0f, 0f, getScroll);
                    if (hit != null) target = hit;
                }
            }
            return target;
        }

        private void DispatchPointer(Fiber? target, PointerEvent e)
        {
            if (target == null || _reconciler?.Root == null) return;
            // Set element-local coordinates relative to the hit-test target.
            e.LocalX = e.X - target.Layout.AbsoluteX;
            e.LocalY = e.Y - target.Layout.AbsoluteY;
            e.TargetWidth = target.Layout.Width;
            e.TargetHeight = target.Layout.Height;
            var path = PaperUtility.PathToRoot(target);

            // Capture: root -> parent(target)
            e.Phase = EventPhase.Capturing;
            for (int i = 0; i < path.Count - 1 && !e.PropagationStopped; i++)
                PaperUtility.InvokePointerHandlers(path[i], e, capture: true);

            // Target
            if (!e.PropagationStopped)
            {
                e.Phase = EventPhase.AtTarget;
                PaperUtility.InvokePointerHandlers(target, e, capture: false);
                PaperUtility.InvokePointerHandlers(target, e, capture: true);
            }

            // Bubble: parent(target) -> root
            e.Phase = EventPhase.Bubbling;
            for (int i = path.Count - 2; i >= 0 && !e.PropagationStopped; i--)
                PaperUtility.InvokePointerHandlers(path[i], e, capture: false);
        }

        private void DispatchKey(Fiber target, KeyEvent e)
        {
            var path = PaperUtility.PathToRoot(target);

            // Capture
            e.Phase = EventPhase.Capturing;
            for (int i = 0; i < path.Count - 1 && !e.PropagationStopped; i++)
                PaperUtility.InvokeKeyHandlers(path[i], e, capture: true);

            // Target
            if (!e.PropagationStopped)
            {
                e.Phase = EventPhase.AtTarget;
                PaperUtility.InvokeKeyHandlers(target, e, capture: false);
                PaperUtility.InvokeKeyHandlers(target, e, capture: true);
            }

            // Bubble
            e.Phase = EventPhase.Bubbling;
            for (int i = path.Count - 2; i >= 0 && !e.PropagationStopped; i--)
                PaperUtility.InvokeKeyHandlers(path[i], e, capture: false);
        }

        private void SetFocus(Fiber? next)
        {
            if (ReferenceEquals(next, _inputState.Focused)) return;

            var prev = _inputState.Focused;
            _inputState.Focused = next;
            _inputState.FocusedPath = next != null ? PaperUtility.GetPathString(next) : null;

            if (next != null && next.Type is string nt && PaperUtility.IsTextInput(nt))
            {
                _inputState.InputText = next.Props.Text ?? "";
                var len = _inputState.InputText.Length;
                _inputState.InputCaret = len;
                _inputState.InputSelStart = len;
                _inputState.InputSelEnd = len;
                _inputState.LastInputActivityTicks = Environment.TickCount64;
                StartCaretBlinkTimer();
            }
            else
            {
                _inputState.InputText = null;
                StopCaretBlinkTimer();
            }

            prev?.Props.OnBlur?.Invoke();
            next?.Props.OnFocus?.Invoke();
        }

        private void StartCaretBlinkTimer()
        {
            _inputState.CaretBlinkTimer ??= new Timer(_ => RequestRender(), null, InputState.CARET_BLINK_ON_MS, InputState.CARET_BLINK_ON_MS);
            _inputState.CaretBlinkTimer.Change(InputState.CARET_BLINK_ON_MS, InputState.CARET_BLINK_ON_MS);
        }

        private void StopCaretBlinkTimer()
        {
            _inputState.CaretBlinkTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>True when the caret should be drawn: solid while recently active, else blinking (on phase).</summary>
        private bool ComputeCaretVisible()
        {
            if (_inputState.Focused == null || !PaperUtility.IsTextInput(_inputState.Focused.Type as string))
                return true;
            long elapsed = Environment.TickCount64 - _inputState.LastInputActivityTicks;
            if (elapsed < InputState.CARET_IDLE_MS) return true; // solid while typing
            return (Environment.TickCount64 % InputState.CARET_BLINK_PERIOD_MS) < InputState.CARET_BLINK_ON_MS; // blink
        }

        private void OnRender(double dt)
        {
            if (_gl == null || _rects == null || _viewports == null ||
                _reconciler == null || _layout == null) return;

            PreRender?.Invoke(dt);

            // Reconcile only when something requested it (setState/RequestRender or hot reload).
            bool requested = _renderState.ExternalRenderRequested;
            if (requested)
                _renderState.ExternalRenderRequested = false;
            if (requested || _reconciler.NeedsUpdate())
            {
                _reconciler.Update(_rootFactory!(), forceReconcile: requested);
                _renderState.LayoutDirty = true;
                _renderState.NeedsLayout = true;
            }
            var root = _reconciler.Root;
            if (root == null) return;
            LayoutAndDraw();
            _renderState.LayoutDirty = false; // reset after drawing — next dirty event will re-set it
            // If CSS transitions are still running, keep the animation deadline alive
            if (_renderer?.HasActiveTransitions == true)
                MarkDirty(animationSeconds: 0.1);
        }

        /// <summary>Applies styles, runs layout, and draws the current tree. Used by OnRender and after click-driven Update().</summary>
        private void LayoutAndDraw()
        {
            var root = _reconciler!.Root!;
            // Re-bind focus to the current tree so Input/Textarea get correct Props.Text after re-render
            if (_inputState.FocusedPath != null)
            {
                var live = PaperUtility.GetFiberByPath(root, _inputState.FocusedPath);
                if (live != null && PaperUtility.IsFocusable(live))
                {
                    _inputState.Focused = live;
                    if (live.Type is string lt && PaperUtility.IsTextInput(lt))
                    {
                        _inputState.InputText = live.Props.Text ?? "";
                        PaperUtility.ClampInputIndices(_inputState.InputText.Length, ref _inputState.InputCaret, ref _inputState.InputSelStart, ref _inputState.InputSelEnd);
                    }
                }
                else
                    _inputState.Focused = null;
            }
            // Re-bind _hovered to the current tree after reconcile (fiber objects are replaced each reconcile).
            if (_uiState.HoveredPath != null)
                _uiState.Hovered = PaperUtility.GetFiberByPath(root, _uiState.HoveredPath);

            // If the style registry changed (CSSS sheet loaded/reloaded, class registered),
            // mark all fibers dirty so stale cached ComputedStyles are recomputed.
            int registryVersion = Styles.Version;
            if (registryVersion != _renderState.LastStyleRegistryVersion)
            {
                PaperUtility.InvalidateStyleTree(root);
                _renderState.LastStyleRegistryVersion = registryVersion;
                _renderState.NeedsLayout = true;
            }
            ApplyComputedStyles(root);

            var fbSize = _window!.FramebufferSize;
            _framBufferState.LastFbWidth = fbSize.X;
            _framBufferState.LastFbHeight = fbSize.Y;
            int layoutWidth = _width;
            int layoutHeight = _height;

            if (_layout == null || _measurer == null) return;

            // Only re-run layout when the fiber tree structure or window size changed.
            // Pure scroll/animation frames skip layout — geometry doesn't change when scrolling.
            if (_renderState.NeedsLayout)
            {
                _layout.GetImageSize = path =>
                {
                    var resolved = PaperUtility.ResolveImagePath(path);
                    var dim = _imageLoader?.GetDimensions(resolved);
                    return dim.HasValue ? ((float)dim.Value.w, (float)dim.Value.h) : ((float, float)?)null;
                };
                _layout.Layout(root, layoutWidth, layoutHeight, _measurer);

                // Apply full-screen layout to portal roots so they can position themselves
                // using position:absolute/fixed relative to the window.
                if (_reconciler?.PortalRoots is { Count: > 0 } portals)
                {
                    foreach (var portal in portals)
                        ApplyStylesAndLayout(portal, layoutWidth, layoutHeight);
                }
                _renderState.NeedsLayout = false;
            }

            // Update horizontal scroll for single-line input so caret stays in view
            if (_text != null && _inputState.Focused != null && _inputState.Focused.Type is string fit && fit == ElementTypes.Input)
            {
                var style = _inputState.Focused.ComputedStyle;
                var lb = _inputState.Focused.Layout;
                var inputPad = style.Padding ?? Thickness.Zero;
                float padLeft = inputPad.Left.Resolve(lb.Width);
                float padRight = inputPad.Right.Resolve(lb.Width);
                float contentW = lb.Width - padLeft - padRight;
                string text = _inputState.InputText ?? _inputState.Focused.Props?.Text ?? "";
                var atlas = _text.Atlas;
                float baseSize = atlas.BaseSize > 0 ? atlas.BaseSize : 16f;
                float fontPx = style.FontSize is { } fs && !fs.IsAuto ? fs.Resolve(baseSize) : baseSize;
                float scale = baseSize > 0 ? fontPx / baseSize : 1f;
                float fullTextW = _text.MeasureWidth(text.AsSpan()) * scale;
                int caret = Math.Clamp(_inputState.InputCaret, 0, text.Length);
                float caretX = (caret <= 0 ? 0 : _text.MeasureWidth(text.AsSpan(0, caret)) * scale);
                float maxScroll = Math.Max(0, fullTextW - contentW);
                if (maxScroll <= 0)
                    _inputState.InputScrollX = 0;
                else
                {
                    const float caretMargin = 8f; // keep caret this many px from right edge when scrolling
                    _inputState.InputScrollX = Math.Clamp(caretX - contentW + caretMargin, 0, maxScroll);
                }
            }
            else
                _inputState.InputScrollX = 0;

            _gl!.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.Viewport(0, 0, (uint)fbSize.X, (uint)fbSize.Y);
            _gl.ClearColor(0.07f, 0.07f, 0.12f, 1f);
            _gl.ClearStencil(0);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.StencilBufferBit);

            // Update the long-lived renderer with per-frame state.
            var renderer = _renderer!;
            renderer.SetScreenSize(fbSize.X, fbSize.Y);
            renderer.DpiScale = _width > 0 ? fbSize.X / (float)_width : 1f;
            renderer.ScaleX = renderer.DpiScale;
            renderer.ScaleY = renderer.DpiScale;
            renderer.FocusedInputPath = _inputState.Focused != null && PaperUtility.IsTextInput(_inputState.Focused.Type as string) ?PaperUtility.GetPathString(_inputState.Focused) : _inputState.FocusedPath;
            renderer.FocusedInputText = _inputState.InputText;
            renderer.FocusedInputType = _inputState.Focused?.Props?.InputType;
            renderer.FocusedInputCaret = _inputState.InputCaret;
            renderer.FocusedInputSelStart = _inputState.InputSelStart;
            renderer.FocusedInputSelEnd = _inputState.InputSelEnd;
            renderer.FocusedInputCaretVisible = ComputeCaretVisible();
            renderer.FocusedInputScrollX = _inputState.InputScrollX;
            renderer.HoveredPath = _uiState.Hovered != null ? PaperUtility.GetPathString(_uiState.Hovered) : null;
            renderer.PortalRoots = _reconciler?.PortalRoots;
            renderer.Render(root);

            // Drag ghost: render the dragged fiber at the cursor with 50% opacity.
            if (_uiState.DragActive && _uiState.DragSource != null)
            {
                renderer.RenderGhost(_uiState.DragSource, _uiState.DragCursorX, _uiState.DragCursorY, 0.5f);
            }

            _rects!.Flush(fbSize.X, fbSize.Y);
            _text?.Flush(fbSize.X, fbSize.Y);
        }

        private unsafe void InitGlfwCursors()
        {
            try
            {
                _glfwState.GLFW = Glfw.GetApi();

                if (_glfwState.GLFW == null)
                    return;

                _glfwState.CursorArrow = (nint)_glfwState.GLFW.CreateStandardCursor(CursorShape.Arrow);
                _glfwState.CursorHand = (nint)_glfwState.GLFW.CreateStandardCursor(CursorShape.Hand);
                _glfwState.CursorIBeam = (nint)_glfwState.GLFW.CreateStandardCursor(CursorShape.IBeam);
                _glfwState.CursorCrosshair = (nint)_glfwState.GLFW.CreateStandardCursor(CursorShape.Crosshair);
                _glfwState.CursorEwResize = (nint)_glfwState.GLFW.CreateStandardCursor(CursorShape.HResize);
                // GLFW has no built-in wait/not-allowed — fall back to arrow for those
            }
            catch { }
        }

        private unsafe void DestroyGlfwCursors()
        {
            if (_glfwState.GLFW == null)
                return;
            try
            {
                if (_glfwState.CursorArrow != 0) 
                    _glfwState.GLFW.DestroyCursor((global::Silk.NET.GLFW.Cursor*)_glfwState.CursorArrow);

                if (_glfwState.CursorHand != 0) 
                    _glfwState.GLFW.DestroyCursor((global::Silk.NET.GLFW.Cursor*)_glfwState.CursorHand);

                if (_glfwState.CursorIBeam != 0) 
                    _glfwState.GLFW.DestroyCursor((global::Silk.NET.GLFW.Cursor*)_glfwState.CursorIBeam);

                if (_glfwState.CursorCrosshair != 0)
                    _glfwState.GLFW.DestroyCursor((global::Silk.NET.GLFW.Cursor*)_glfwState.CursorCrosshair);

                if (_glfwState.CursorEwResize != 0)
                    _glfwState.GLFW.DestroyCursor((global::Silk.NET.GLFW.Cursor*)_glfwState.CursorEwResize);

                _glfwState.CursorArrow = _glfwState.CursorHand = _glfwState.CursorIBeam = _glfwState.CursorCrosshair = _glfwState.CursorEwResize = 0;

                _glfwState.GLFW.Dispose();
                _glfwState.GLFW = null;
            }
            catch { }
        }

        private unsafe void ApplyGlfwCursor(Core.Styles.Cursor cursor)
        {
            if (_glfwState.GLFW == null)
                return;
            try
            {
                nint handle = _window!.Handle;
                if (handle == 0) return;
                nint pointer = cursor switch
                {
                    Core.Styles.Cursor.Pointer => _glfwState.CursorHand,
                    Core.Styles.Cursor.Text => _glfwState.CursorIBeam,
                    Core.Styles.Cursor.Crosshair => _glfwState.CursorCrosshair,
                    Core.Styles.Cursor.EwResize => _glfwState.CursorEwResize,
                    Core.Styles.Cursor.None => 0,
                    _ => _glfwState.CursorArrow,
                };
                _glfwState.GLFW.SetCursor((WindowHandle*)handle, (global::Silk.NET.GLFW.Cursor*)pointer);
            }
            catch { }
        }

        /// <summary>Applies minimum size limits via GLFW when available (Silk.NET backend is often GLFW). No-op if not set or not supported.</summary>
        private unsafe void ApplyMinimumWindowSizeLimits()
        {
            if (!MinimumWindowWidth.HasValue && !MinimumWindowHeight.HasValue) return;
            int minW = MinimumWindowWidth ?? 1;
            int minH = MinimumWindowHeight ?? 1;
            try
            {
                var glfw = Glfw.GetApi();
                if (glfw == null) return;
                nint h = _window!.Handle;
                if (h == 0) return;
                glfw.SetWindowSizeLimits((WindowHandle*)h, minW, minH, Glfw.DontCare, Glfw.DontCare);
            }
            catch
            {
                // Backend may not be GLFW or Handle may not be GLFW window; OnResize clamp will still apply.
            }
        }

        private void OnResize(Vector2D<int> size)
        {
            int w = size.X;
            int h = size.Y;
            // NOTE: do NOT set _externalRenderRequested here. Resize only changes layout
            // dimensions — the fiber tree is identical, so forceReconcile is wasteful.
            if (MinimumWindowWidth.HasValue && w < MinimumWindowWidth.Value)
                w = MinimumWindowWidth.Value;
            if (MinimumWindowHeight.HasValue && h < MinimumWindowHeight.Value)
                h = MinimumWindowHeight.Value;
            if (w != size.X || h != size.Y)
            {
                try { _window!.Size = new Vector2D<int>(w, h); } catch { /* fallback if setter not supported */ }
            }
            _width = w;
            _height = h;
            _renderState.LayoutDirty = true;
            _renderState.NeedsLayout = true;
            // Render immediately so the window content updates during drag resize.
            // Previously this caused VSync stalls (~300ms render × every pixel), but
            // now that rendering is ~1ms the VSync wait (~16ms at 60Hz) is acceptable.
            if (_gl != null && _reconciler?.Root != null)
                _window?.DoRender();
        }

        public void Shutdown() => _window?.Close();

        private void ApplyComputedStyles(Fiber fiber)
        {
            if (fiber == null) return;
            var state = new InteractionState(
                Hover: ReferenceEquals(fiber, _uiState.Hovered),
                Active: ReferenceEquals(fiber, _uiState.Pressed),
                Focus: ReferenceEquals(fiber, _inputState.Focused));

            if (fiber.StyleDirty || fiber.CachedInteractionState != state)
            {
                fiber.ComputedStyle = StyleResolver.Resolve(fiber.Type, fiber.Props, Styles, state, fiber);
                fiber.StyleDirty = false;
                fiber.CachedInteractionState = state;
            }

            var child = fiber.Child;
            while (child != null)
            {
                ApplyComputedStyles(child);
                child = child.Sibling;
            }
        }

        /// <summary>
        /// Applies computed styles and runs layout for a portal root fiber.
        /// Portals are laid out against the full window/framebuffer area so that
        /// <c>position:absolute/fixed</c> elements within them can be placed anywhere on screen.
        /// </summary>
        private void ApplyStylesAndLayout(Fiber fiber, int width, int height)
        {
            ApplyComputedStyles(fiber);
            _layout?.Layout(fiber, width, height, _measurer!);
        }
    }
}
