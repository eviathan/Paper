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
    public sealed class PaperSurface : IDisposable
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
        private Fiber? _hovered;
        private string? _hoveredPath; // Stable path so we can re-bind _hovered after reconcile replaces the tree.
        private Fiber? _pressed;
        private string? _pressedPath; // Stable path (indices from root) so we match the same control after tree is replaced by a reconcile.
        private Fiber? _focused;
        private string? _focusedPath; // Path so we can re-bind _focused to the new tree after re-render (input/textarea need current Props).
        private int _inputCaret;      // Caret index in focused Input/Textarea
        private int _inputSelStart;   // Selection start (can be > end for backward selection)
        private int _inputSelEnd;     // Selection end
        private int _inputSelAnchor;  // Mouse selection anchor (index at mousedown)
        private bool _inputSelecting; // True while left-drag selecting in focused input
        private float _inputScrollX;  // Horizontal scroll offset for single-line input when text overflows
        private string? _inputText;   // Local buffer for focused input so rapid key events don't read stale Props.Text
        private long _lastInputActivityTicks; // Environment.TickCount64 when last key/focus in input (for caret solid vs blink)
        private System.Threading.Timer? _caretBlinkTimer;
        private DateTime _lastClickAtUtc = DateTime.MinValue;
        private bool _lastClickWasDoubleOnInput;
        private string? _lastDoubleClickInputPath;
        private DateTime _lastDoubleClickAtUtc = DateTime.MinValue;

        private const int CaretIdleMs = 500;   // After this many ms without input, caret starts blinking
        private const int CaretBlinkPeriodMs = 1000; // Match macOS default (~1s period)
        private const int CaretBlinkOnMs = 500;      // Half period = 50% duty
        private CSXHotReload? _csxHotReload;
        private volatile bool _externalRenderRequested;
        private readonly Dictionary<string, (float scrollX, float scrollY)> _scrollOffsets = new();
        private int _lastFbWidth;
        private int _lastFbHeight;
        private ImageTextureLoader? _imageLoader;
        private FiberRenderer? _renderer;
        private string? _scrollbarDragPath;
        private float _scrollbarDragAnchorY;
        private float _scrollbarDragAnchorScroll;

        // Pointer capture: fiber that received the last PointerDown. Receives Move events
        // regardless of cursor position while the button is held, so sliders/knobs work
        // correctly when the cursor leaves the element during drag.
        private Fiber? _pointerDownFiber;

        // Drag-and-drop state
        private Fiber? _dragSource;        // fiber where drag started
        private string? _dragSourcePath;   // stable path of drag source
        private object? _dragData;         // payload set by OnDragStart
        private bool _dragActive;          // true once drag threshold crossed and DragStart fired
        private float _dragStartX;         // pointer position when mouse button pressed
        private float _dragStartY;
        private float _dragCursorX;        // current cursor position during drag (layout coords)
        private float _dragCursorY;
        private Fiber? _dragOver;          // current fiber under pointer during drag
        // Scrollbar fade: maps path → time (seconds) at which scrolling last occurred
        private readonly Dictionary<string, double> _scrollbarLastActive = new();
        // Dirty-flag rendering: only run LayoutAndDraw when something changed.
        // _animationDeadline keeps rendering alive while scrollbar fade / CSS transitions run.
        private volatile bool _layoutDirty = true;
        /// <summary>
        /// True when fiber tree structure or window size changed — layout must re-run.
        /// False for pure scroll/animation frames where only rendering position changes.
        /// </summary>
        private bool _needsLayout = true;
        private double _animationDeadline; // UTC seconds — keep drawing until this time
        private int _lastStyleRegistryVersion = -1; // tracks StyleRegistry.Version; -1 forces first-frame invalidation

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

        public PaperSurface(string title = "Paper", int width = 1280, int height = 720)
        {
            _title = title;
            _width = width;
            _height = height;
        }

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
            scopeId ??= Path.GetFileNameWithoutExtension(csxFilePath);
            _csxHotReload?.Dispose();
            _csxHotReload = new CSXHotReload(this, csxFilePath, scopeId);
            _csxHotReload.Start();
            Mount(_csxHotReload.RootComponent);
        }

        public void RequestRender() { _externalRenderRequested = true; _layoutDirty = true; }

        /// <summary>
        /// Mark that a draw is needed and extend the animation deadline (keeps rendering alive
        /// for scrollbar fade / CSS transitions for up to <paramref name="animationSeconds"/> seconds).
        /// </summary>
        private void MarkDirty(double animationSeconds = 0)
        {
            _layoutDirty = true;
            if (animationSeconds > 0)
            {
                double deadline = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond + animationSeconds;
                if (deadline > _animationDeadline) _animationDeadline = deadline;
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
            options.VSync = true; // Lock SwapBuffers to the display refresh — natural 60/120fps pacing without sleep imprecision
            // Continuous render loop: PollEvents (non-blocking) so we get OnRender at FramesPerSecond even when idle.
            // When IsEventDriven is true, GLFW uses WaitEvents() and blocks until input — so hot reload and button updates only appear after a click.
            options.IsEventDriven = false;
            options.PreferredStencilBufferBits = 8; // Needed for rounded overflow:hidden clipping

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

        /// <summary>
        /// Releases all GPU and managed resources. Called automatically at the end of <see cref="Run"/>.
        /// Safe to call manually if <see cref="Run"/> was not used (e.g. in tests or headless scenarios).
        /// </summary>
        public void Dispose() => DisposeResources();

        private void DisposeResources()
        {
            _caretBlinkTimer?.Dispose();
            _caretBlinkTimer = null;

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
                if (_window.IsClosing) break;
                _window.DoUpdate();
                if (_window.IsClosing) break;

                double utcNow = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
                bool animating = utcNow < _animationDeadline;

                if (_layoutDirty || animating)
                    _window.DoRender(); // blocks at vblank — natural frame pacing
                else
                    System.Threading.Thread.Sleep(4); // idle: yield CPU, wake at ~250 Hz
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
            // Separate blend factors for RGB vs alpha channel.
            // RGB: standard straight-alpha blending (SrcAlpha / 1-SrcAlpha).
            // Alpha: Zero/One — preserve the framebuffer's alpha as-is (always keeps it at 1.0
            // after the clear). Without this, semi-transparent draws reduce the framebuffer alpha
            // below 1.0, and macOS Metal composites the window against the desktop background
            // (typically white), making semi-transparent colours appear lighter than intended.
            _gl.BlendFuncSeparate(
                BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                BlendingFactor.Zero, BlendingFactor.One);

            // Load fonts for text rendering (degrades gracefully if not found).
            // Discovers regular + bold variants by checking common naming conventions
            // next to the primary font file (e.g. roboto.ttf → roboto-bold.ttf).
            var fontRegistry = new FontRegistry();
            var fontDir  = Path.Combine(AppContext.BaseDirectory, "Assets", "fonts");
            if (Directory.Exists(fontDir))
            {
                foreach (var regularPath in Directory.GetFiles(fontDir, "*.ttf"))
                {
                    // Skip files that look like weight variants — they are registered via the primary file.
                    var fname = Path.GetFileNameWithoutExtension(regularPath);
                    if (fname.EndsWith("-bold",    StringComparison.OrdinalIgnoreCase)) continue;
                    if (fname.EndsWith("-italic",  StringComparison.OrdinalIgnoreCase)) continue;
                    if (fname.EndsWith("-bolditalic", StringComparison.OrdinalIgnoreCase)) continue;
                    if (fname.EndsWith("bold",     StringComparison.OrdinalIgnoreCase) && fname.Length > 4) continue;

                    // Icon fonts (file name contains "icon") use all available codepoints
                    // (typically Unicode PUA ranges) instead of just ASCII.
                    bool isIconFont = fname.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0;
                    var atlasLoader = isIconFont
                        ? (Func<GL, string, Dictionary<int, PaperFontAtlas>>)((g, p) => PaperFontLoader.LoadIconSet(g, p))
                        : (g, p) => PaperFontLoader.LoadSet(g, p);

                    var regular = new PaperFontSet(atlasLoader(_gl, regularPath), _gl);

                    // Look for bold, italic, bold-italic variants next to the regular file.
                    PaperFontSet? bold = null;
                    foreach (var suffix in new[] { "-bold", "-Bold", "bold", "Bold" })
                    {
                        var boldPath = Path.Combine(fontDir, fname + suffix + ".ttf");
                        if (File.Exists(boldPath))
                        {
                            bold = new PaperFontSet(PaperFontLoader.LoadSet(_gl, boldPath), _gl);
                            break;
                        }
                    }

                    PaperFontSet? italic = null;
                    foreach (var suffix in new[] { "-italic", "-Italic" })
                    {
                        var italicPath = Path.Combine(fontDir, fname + suffix + ".ttf");
                        if (File.Exists(italicPath))
                        {
                            italic = new PaperFontSet(PaperFontLoader.LoadSet(_gl, italicPath), _gl);
                            break;
                        }
                    }

                    PaperFontSet? boldItalic = null;
                    foreach (var suffix in new[] { "-bolditalic", "-BoldItalic", "-bold-italic", "-Bold-Italic" })
                    {
                        var biPath = Path.Combine(fontDir, fname + suffix + ".ttf");
                        if (File.Exists(biPath))
                        {
                            boldItalic = new PaperFontSet(PaperFontLoader.LoadSet(_gl, biPath), _gl);
                            break;
                        }
                    }

                    // Register under the filename (e.g. "roboto") and as "default" for the first font.
                    fontRegistry.Register(fname.ToLowerInvariant(), regular, bold, italic, boldItalic);
                }
            }

            if (fontRegistry.Default != null)
            {
                _fontSet  = fontRegistry;
                _measurer = new SilkTextMeasurer(fontRegistry);
            }

            // Create long-lived renderer (holds CSS transition animation state across frames).
            _renderer = new FiberRenderer(_rects!, _viewports!, _fontSet, _width, _height, _gl)
            {
                GetScrollOffset = path => _scrollOffsets.TryGetValue(path, out var v) ? v : (0f, 0f),
                GetScrollbarOpacity = path =>
                {
                    if (!_scrollbarLastActive.TryGetValue(path, out double t)) return 0f;
                    double elapsed = (DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond) - t;
                    // Visible for 1.2s then fade over 0.4s
                    const double visible = 1.2, fade = 0.4;
                    if (elapsed < visible) return 1f;
                    if (elapsed >= visible + fade) return 0f;
                    return 1f - (float)((elapsed - visible) / fade);
                },
                GetImageTexture = path => _imageLoader?.GetOrLoad(ResolveImagePath(path)).Handle ?? 0,
                GetImageResult  = path =>
                {
                    var r = _imageLoader != null ? _imageLoader.GetOrLoad(ResolveImagePath(path)) : default(ImageTextureResult);
                    return r.Handle != 0 ? (r.Handle, r.Width, r.Height) : (0u, 0, 0);
                },
            };

            _reconciler = new Reconciler();
            // When hooks (e.g. setState) request a re-render, also mark the surface so the next frame runs.
            var prevRequest = Paper.Core.Hooks.RenderScheduler.OnRenderRequested;
            Paper.Core.Hooks.RenderScheduler.OnRenderRequested = () =>
            {
                // prevRequest sets _reconciler._renderRequested = true (per-component dirty tracking).
                // Do NOT set _externalRenderRequested here — that forces a full tree re-reconcile.
                // Normal setState uses per-component reconciliation (forceReconcile: false).
                prevRequest?.Invoke();
                _layoutDirty = true; // wake the RunLoop so DoRender() is called next frame
            };
            _reconciler.Mount(_rootFactory!());

            // Seed layout size from window so first frame and layout use actual size (OnResize then keeps _width/_height updated during drag).
            _width = _window!.Size.X;
            _height = _window.Size.Y;

            // Enforce minimum window size via GLFW so the window cannot be resized smaller (when backend is GLFW).
            ApplyMinimumWindowSizeLimits();
            InitGlfwCursors();

            // Create input and wire up Paper UI click handling
            var fbSize = _window!.FramebufferSize;
            var ic = _window.CreateInput();
            _inputContext = ic;

            foreach (var mouse in ic.Mice)
            {
                mouse.MouseDown += OnMouseButtonDown;
                mouse.MouseUp += OnMouseButtonUp;
                mouse.MouseMove += OnMouseMove;
                mouse.Scroll += OnMouseScroll;
            }

            foreach (var kb in ic.Keyboards)
            {
                kb.KeyDown += OnKeyDown;
                kb.KeyUp += OnKeyUp;
                kb.KeyChar += OnKeyChar;
            }

            OnLoad?.Invoke(_gl, ic, fbSize.X, fbSize.Y);
        }

        private void OnMouseButtonDown(IMouse mouse, MouseButton button)
        {
            if (_reconciler?.Root == null || _window == null) return;

            // Check if mouse down hit a scrollbar thumb
            if (_renderer != null)
            {
                var (fbx, fby) = ToLayoutCoords(mouse.Position);
                foreach (var kvp in _renderer.RenderedScrollbars)
                {
                    var sb = kvp.Value;
                    if (fbx >= sb.TrackX && fbx <= sb.TrackX + 6f &&
                        fby >= sb.ThumbY && fby <= sb.ThumbY + sb.ThumbH)
                    {
                        _scrollbarDragPath = kvp.Key;
                        _scrollbarDragAnchorY = fby;
                        _scrollbarDragAnchorScroll = _scrollOffsets.TryGetValue(kvp.Key, out var sv) ? sv.scrollY : 0f;
                        return; // consume event
                    }
                }
            }

            var (lx, ly) = ToLayoutCoords(mouse.Position);
            var target = HitTestAll(lx, ly);
            _pressed = target;
            _pressedPath = target != null ? GetPathString(target) : null;
            if (button == MouseButton.Left) _pointerDownFiber = target;

            // Track potential drag source — walk up from target to find element with OnDragStart
            // (clicks often land on a child text node, not the draggable container itself).
            Fiber? dragCandidate = target;
            while (dragCandidate != null && dragCandidate.Props.OnDragStart == null)
                dragCandidate = dragCandidate.Parent;

            if (button == MouseButton.Left && dragCandidate != null)
            {
                _dragSource = dragCandidate;
                _dragSourcePath = GetPathString(dragCandidate);
                _dragData = null;
                _dragActive = false;
                _dragStartX = lx;
                _dragStartY = ly;
                _dragOver = null;
            }
            else
            {
                _dragSource = null;
                _dragSourcePath = null;
                _dragActive = false;
            }

            DispatchPointer(target, new PointerEvent
            {
                Type = PointerEventType.Down,
                X = lx,
                Y = ly,
                Button = button == MouseButton.Left ? 0 : button == MouseButton.Right ? 1 : 2,
            });

            // Focus on pointer down if the element is focusable. Resolve to Input/Textarea when clicking a child.
            Fiber? focusTarget = GetInputAncestorOrSelf(target) ?? target;
            if (IsFocusable(focusTarget))
            {
                SetFocus(focusTarget);
                if (focusTarget != null && focusTarget.Type is string ft && IsTextInput(ft))
                {
                    _lastInputActivityTicks = Environment.TickCount64;
                    StartCaretBlinkTimer();
                    var (scrollX, scrollY) = GetTotalScrollForPath(_focusedPath ?? "");
                    float inputScroll = (focusTarget == _focused && focusTarget.Type is string it && IsTextInput(it)) ? _inputScrollX : 0f;
                    int idx = GetCaretIndexFromX(focusTarget, lx, ly, scrollX, scrollY, inputScroll);
                    _inputCaret = _inputSelStart = _inputSelEnd = _inputSelAnchor = idx;
                    _inputSelecting = true;
                    RequestRender();
                }
            }
        }

        private void DispatchDrag(Fiber? target, DragEvent e)
        {
            if (target == null) return;
            var path = PathToRoot(target);
            for (int i = path.Count - 1; i >= 0; i--)
            {
                var fiber = path[i];
                var handler = e.Type switch
                {
                    DragEventType.DragStart => fiber.Props.OnDragStart,
                    DragEventType.Drag      => fiber.Props.OnDrag,
                    DragEventType.DragEnd   => fiber.Props.OnDragEnd,
                    DragEventType.DragEnter => fiber.Props.OnDragEnter,
                    DragEventType.DragOver  => fiber.Props.OnDragOver,
                    DragEventType.DragLeave => fiber.Props.OnDragLeave,
                    DragEventType.Drop      => fiber.Props.OnDrop,
                    _                       => null,
                };
                handler?.Invoke(e);
            }
        }

        private void OnMouseButtonUp(IMouse mouse, MouseButton button)
        {
            if (_reconciler?.Root == null || _window == null) return;

            if (_scrollbarDragPath != null) { _scrollbarDragPath = null; return; }

            var (lx, ly) = ToLayoutCoords(mouse.Position);
            var target = HitTestAll(lx, ly);

            // Finish drag-and-drop
            if (button == MouseButton.Left && _dragActive && _dragSource != null)
            {
                var dropEvt = new DragEvent { Type = DragEventType.Drop, X = lx, Y = ly, Data = _dragData };
                DispatchDrag(target, dropEvt);

                var endEvt = new DragEvent { Type = DragEventType.DragEnd, X = lx, Y = ly, Data = _dragData };
                DispatchDrag(_dragSource, endEvt);

                if (_dragOver != null)
                {
                    DispatchDrag(_dragOver, new DragEvent { Type = DragEventType.DragLeave, X = lx, Y = ly, Data = _dragData });
                    _dragOver = null;
                }
                _dragSource = null;
                _dragSourcePath = null;
                _dragActive = false;
                _dragData = null;
            }
            else if (button == MouseButton.Left)
            {
                _dragSource = null;
                _dragSourcePath = null;
                _dragActive = false;
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
            bool sameControl = target != null && _pressedPath != null && GetPathString(target) == _pressedPath;
            if (button == MouseButton.Left && target != null && sameControl)
            {
                var now = DateTime.UtcNow;
                bool isDouble = (now - _lastClickAtUtc).TotalMilliseconds < 350;
                _lastClickAtUtc = now;
                if (!isDouble)
                    _lastClickWasDoubleOnInput = false;

                DispatchPointer(target, new PointerEvent
                {
                    Type = isDouble ? PointerEventType.DoubleClick : PointerEventType.Click,
                    X = lx,
                    Y = ly,
                    Button = 0,
                });

                // Double-click in Input/Textarea: macOS-style — first double-click selects word, second (triple-click) selects all.
                if (isDouble && _focused != null && _focusedPath != null)
                {
                    Fiber? inputFiber = GetInputAncestorOrSelf(target);
                    if (inputFiber != null && GetPathString(inputFiber) == _focusedPath &&
                        inputFiber.Type is string fit && IsTextInput(fit))
                    {
                        var cur = _inputText ?? inputFiber.Props?.Text ?? "";
                        int len = cur.Length;
                        bool sameInputAsLastDouble = _lastClickWasDoubleOnInput && _lastDoubleClickInputPath == _focusedPath &&
                            (now - _lastDoubleClickAtUtc).TotalMilliseconds < 350;
                        if (sameInputAsLastDouble)
                        {
                            // Triple-click: select all
                            _inputSelStart = 0;
                            _inputSelEnd = len;
                            _inputCaret = len;
                        }
                        else
                        {
                            // Double-click: select word at caret (set by mousedown of second click)
                            var (wStart, wEnd) = GetWordBounds(cur, _inputCaret);
                            _inputSelStart = wStart;
                            _inputSelEnd = wEnd;
                            _inputCaret = wEnd;
                        }
                        _lastInputActivityTicks = Environment.TickCount64;
                        _lastClickWasDoubleOnInput = true;
                        _lastDoubleClickInputPath = _focusedPath;
                        _lastDoubleClickAtUtc = now;
                    }
                    else
                    {
                        _lastClickWasDoubleOnInput = false;
                    }
                }
                else
                {
                    _lastClickWasDoubleOnInput = false;
                }

                // Use target from current tree (has correct Props/OnClick); _pressed may be from old tree.
                Fiber? clickTarget = GetClickTarget(target);
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

            _pressed = null;
            _pressedPath = null;
            _inputSelecting = false;
        }

        /// <summary>Stable path string (indices from root) so we can match the same control after the tree is replaced.</summary>
        private static string GetPathString(Fiber? fiber)
        {
            if (fiber == null) return "";
            var path = PathToRoot(fiber);
            return string.Join(".", path.Select(f => f.Index));
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
                if (_scrollOffsets.TryGetValue(prefix, out var v)) { sx += v.scrollX; sy += v.scrollY; }
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
                float w = 0f;
                int end = start;
                int lastSpace = -1;
                while (end < line.Length)
                {
                    char c = line[end];
                    float cw = _fontSet.MeasureWidth(line.AsSpan(end, 1), fontPx);
                    if (w + cw > maxWidth && end > start) break;
                    if (c == ' ') lastSpace = end;
                    w += cw;
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
            if (result.Count == 0) result.Add(("", offset, offset));
            return result;
        }

        /// <summary>Word boundaries for macOS-style double-click: (start, end) of the word containing index, or (idx, idx) if none.</summary>
        private static (int start, int end) GetWordBounds(string text, int idx)
        {
            if (string.IsNullOrEmpty(text) || idx < 0 || idx > text.Length) return (0, 0);
            idx = Math.Clamp(idx, 0, text.Length);
            bool isWordChar(int i)
            {
                if (i < 0 || i >= text.Length) return false;
                char c = text[i];
                return char.IsLetterOrDigit(c) || c == '_';
            }
            if (idx == text.Length && idx > 0) idx--;
            int start = idx;
            int end = idx;
            if (isWordChar(idx))
            {
                while (start > 0 && isWordChar(start - 1)) start--;
                while (end < text.Length && isWordChar(end)) end++;
                return (start, end);
            }
            // On whitespace/punctuation: select the previous word (macOS-style)
            int p = idx;
            while (p > 0 && !isWordChar(p - 1)) p--;
            while (p > 0 && isWordChar(p - 1)) p--;
            start = p;
            while (p < text.Length && isWordChar(p)) p++;
            end = p;
            return (start, end);
        }

        /// <summary>True if a is the same as b or one is a descendant of the other (same "control" for click).</summary>
        private static bool IsSameControl(Fiber? a, Fiber? b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            return IsDescendantOf(a, b) || IsDescendantOf(b, a);
        }

        private static bool IsDescendantOf(Fiber? node, Fiber? ancestor)
        {
            for (var p = node?.Parent; p != null; p = p.Parent)
                if (ReferenceEquals(p, ancestor)) return true;
            return false;
        }

        private static bool IsTextInput(string? type) =>
            type == ElementTypes.Input || type == ElementTypes.Textarea || type == ElementTypes.MarkdownEditor;

        private static bool IsMultiLineInput(string? type) =>
            type == ElementTypes.Textarea || type == ElementTypes.MarkdownEditor;

        /// <summary>Returns the Input, Textarea, or MarkdownEditor that contains this fiber (self or ancestor), or null.</summary>
        private static Fiber? GetInputAncestorOrSelf(Fiber? target)
        {
            for (var f = target; f != null; f = f.Parent)
                if (IsTextInput(f.Type as string))
                    return f;
            return null;
        }

        /// <summary>Returns the fiber that should receive the click: the first (deepest) that has an OnClick, OnDoubleClick, or OnCheckedChange handler.</summary>
        private static Fiber? GetClickTarget(Fiber? target)
        {
            for (var f = target; f != null; f = f.Parent)
                if (f.Props.OnClick != null || f.Props.OnDoubleClick != null || f.Props.OnCheckedChange != null)
                    return f;
            return null;
        }

        private void OnMouseMove(IMouse mouse, Vector2 position)
        {
            if (_reconciler?.Root == null) return;

            // Handle scrollbar thumb drag
            if (_scrollbarDragPath != null && mouse.IsButtonPressed(MouseButton.Left))
            {
                if (_renderer != null && _renderer.RenderedScrollbars.TryGetValue(_scrollbarDragPath, out var sb))
                {
                    var (_, fby) = ToLayoutCoords(position);
                    float delta = fby - _scrollbarDragAnchorY;
                    float trackUsable = sb.TrackH - sb.ThumbH;
                    // All values (delta, MaxScroll, _scrollOffsets) are in layout/fb space — no scale conversion needed
                    float scrollDelta = trackUsable > 0 ? delta * sb.MaxScroll / trackUsable : 0f;
                    float newScroll = Math.Max(0f, Math.Min(sb.MaxScroll, _scrollbarDragAnchorScroll + scrollDelta));
                    var (cx, _) = _scrollOffsets.TryGetValue(_scrollbarDragPath, out var cv) ? cv : (0f, 0f);
                    _scrollOffsets[_scrollbarDragPath] = (cx, newScroll);
                    _scrollbarLastActive[_scrollbarDragPath] = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
                    MarkDirty(animationSeconds: 2.0);
                }
                return;
            }

            var (lx, ly) = ToLayoutCoords(position);
            var target = HitTestAll(lx, ly);

            // During an active drag don't process hover changes — style recalc on every cell hover
            // causes full layout passes each frame and makes dragging feel sluggish.
            if (!_dragActive && !ReferenceEquals(target, _hovered))
            {
                // Leave old
                if (_hovered != null)
                {
                    DispatchPointer(_hovered, new PointerEvent { Type = PointerEventType.Leave, X = lx, Y = ly, Button = -1 });
                    _hovered.Props.OnMouseLeave?.Invoke(); // back-compat
                }

                // Enter new
                if (target != null)
                {
                    DispatchPointer(target, new PointerEvent { Type = PointerEventType.Enter, X = lx, Y = ly, Button = -1 });
                    target.Props.OnMouseEnter?.Invoke(); // back-compat
                }

                _hovered = target;
                _hoveredPath = target != null ? GetPathString(target) : null;
                ApplyGlfwCursor(target?.ComputedStyle.Cursor ?? Paper.Core.Styles.Cursor.Default);
                MarkDirty(); // hover state affects :hover styles
            }
            else if (_dragActive)
            {
                // Still update cursor and mark dirty for ghost repaint, but skip hover dispatch.
                ApplyGlfwCursor(target?.ComputedStyle.Cursor ?? Paper.Core.Styles.Cursor.Default);
                MarkDirty();
            }

            // If a button is held, route moves to the originally-pressed fiber so elements
            // like sliders keep responding even when the cursor leaves their bounds.
            if (mouse.IsButtonPressed(MouseButton.Left) && _pointerDownFiber != null)
            {
                var lb = _pointerDownFiber.Layout;
                var capturedEvt = new PointerEvent { Type = PointerEventType.Move, X = lx, Y = ly, Button = 0 };
                capturedEvt.LocalX = lx - lb.AbsoluteX;
                capturedEvt.LocalY = ly - lb.AbsoluteY;
                DispatchPointer(_pointerDownFiber, capturedEvt);
            }
            else if (target != null)
            {
                DispatchPointer(target, new PointerEvent { Type = PointerEventType.Move, X = lx, Y = ly, Button = -1 });
            }

            // Drag-and-drop: start drag once threshold crossed, then fire Drag + DragEnter/Leave/Over
            if (_dragSource != null && mouse.IsButtonPressed(MouseButton.Left))
            {
                const float DragThreshold = 4f;
                float dx = lx - _dragStartX;
                float dy = ly - _dragStartY;

                if (!_dragActive && (dx * dx + dy * dy) >= DragThreshold * DragThreshold)
                {
                    _dragActive = true;
                    var startEvt = new DragEvent { Type = DragEventType.DragStart, X = lx, Y = ly };
                    DispatchDrag(_dragSource, startEvt);
                    _dragData = startEvt.Data; // source handler may set Data via a wrapper — see note
                }

                if (_dragActive)
                {
                    _dragCursorX = lx;
                    _dragCursorY = ly;
                    // Only dispatch Drag event if source actually has an OnDrag handler (avoids PathToRoot walk every frame).
                    if (_dragSource!.Props.OnDrag != null)
                        DispatchDrag(_dragSource, new DragEvent { Type = DragEventType.Drag, X = lx, Y = ly, Data = _dragData });

                    if (!ReferenceEquals(target, _dragOver))
                    {
                        if (_dragOver != null)
                            DispatchDrag(_dragOver, new DragEvent { Type = DragEventType.DragLeave, X = lx, Y = ly, Data = _dragData });
                        if (target != null)
                            DispatchDrag(target, new DragEvent { Type = DragEventType.DragEnter, X = lx, Y = ly, Data = _dragData });
                        _dragOver = target;
                    }
                    else if (target != null)
                    {
                        DispatchDrag(target, new DragEvent { Type = DragEventType.DragOver, X = lx, Y = ly, Data = _dragData });
                    }
                }
            }

            // Mouse selection: extend selection while dragging (including when mouse leaves the input)
            if (_inputSelecting && _focusedPath != null && mouse.IsButtonPressed(MouseButton.Left) &&
                _focused != null && _focused.Type is string fm && IsTextInput(fm))
            {
                var (scrollX, scrollY) = GetTotalScrollForPath(_focusedPath);
                Fiber? inputFiber = (target != null && GetPathString(target) == _focusedPath) ? target : _focused;
                float lxClamp = lx;
                float lyClamp = ly;
                var lb = _focused.Layout;
                float left = (lb.AbsoluteX - scrollX);
                float right = left + lb.Width;
                float top = lb.AbsoluteY - scrollY;
                float bottom = top + lb.Height;
                if (lxClamp < left) lxClamp = left;
                if (lxClamp > right) lxClamp = right;
                if (lyClamp < top) lyClamp = top;
                if (lyClamp > bottom) lyClamp = bottom;
                float inputScroll = IsTextInput(fm) ? _inputScrollX : 0f;
                int idx = GetCaretIndexFromX(_focused, lxClamp, lyClamp, scrollX, scrollY, inputScroll);
                _inputCaret = idx;
                _inputSelStart = Math.Min(_inputSelAnchor, idx);
                _inputSelEnd = Math.Max(_inputSelAnchor, idx);
                RequestRender();
            }
        }

        private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
        {
            if (_reconciler?.Root == null) return;

            var (lx, ly) = ToLayoutCoords(mouse.Position);
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
            var pathToRoot = PathToRoot(target);
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
                    var (sx, sy) = _scrollOffsets.TryGetValue(key, out var v) ? v : (0f, 0f);
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
                    _scrollOffsets[key] = (newSx, newSy);
                    // Record activity for scrollbar fade-in/out (visible 1.2s + fade 0.4s = 1.6s)
                    _scrollbarLastActive[key] = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
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

            var target = _focused;
            if (target == null) return;

            // Tab / Shift+Tab: cycle focus between all focusable elements in proper tab order.
            if (key == Key.Tab && _reconciler?.Root != null)
            {
                var all = new List<Fiber>();
                CollectFocusable(_reconciler.Root, all);
                
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
                    int cur = all.FindIndex(f => ReferenceEquals(f, _focused));
                    int next = shift
                        ? (cur <= 0 ? ordered.Count - 1 : cur - 1)
                        : (cur < 0 || cur >= ordered.Count - 1 ? 0 : cur + 1);
                    var nextFiber = ordered[next];
                    SetFocus(nextFiber);
                    // Select all text in the newly focused input
                    if (_inputText != null)
                    {
                        _inputSelStart = 0;
                        _inputSelEnd = _inputText.Length;
                        _inputCaret = _inputText.Length;
                    }
                }
                return;
            }

            // Input/Textarea: navigation, Delete, and clipboard shortcuts (use _inputText so rapid keys don't read stale Props)
            if (target.Type is string t && IsTextInput(t))
            {
                // Allow navigation even in readOnly/disabled, but block modification
                bool canModify = !target.Props.ReadOnly && !target.Props.Disabled;
                _lastInputActivityTicks = Environment.TickCount64;
                var cur = _inputText ?? target.Props.Text ?? "";
                ClampInputIndices(cur.Length, ref _inputCaret, ref _inputSelStart, ref _inputSelEnd);
                int len = cur.Length;
                bool handled = true;

                if (key == Key.Left)
                {
                    int selMin = Math.Min(_inputSelStart, _inputSelEnd);
                    int selMax = Math.Max(_inputSelStart, _inputSelEnd);
                    int dest;
                    if (cmd)       dest = 0;                           // Cmd+Left = Home
                    else if (alt)  dest = WordStartBefore(cur, _inputCaret); // Option+Left = word left
                    else           dest = _inputCaret > 0 ? _inputCaret - 1 : 0;
                    // Without shift: if selection exists, collapse to its start; otherwise move normally
                    if (!shift && selMin != selMax) dest = selMin;
                    _inputCaret = dest;
                    if (shift) _inputSelEnd = dest; else _inputSelStart = _inputSelEnd = dest;
                }
                else if (key == Key.Right)
                {
                    int selMin = Math.Min(_inputSelStart, _inputSelEnd);
                    int selMax = Math.Max(_inputSelStart, _inputSelEnd);
                    int dest;
                    if (cmd)       dest = len;                          // Cmd+Right = End
                    else if (alt)  dest = WordEndAfter(cur, _inputCaret); // Option+Right = word right
                    else           dest = _inputCaret < len ? _inputCaret + 1 : len;
                    // Without shift: if selection exists, collapse to its end; otherwise move normally
                    if (!shift && selMin != selMax) dest = selMax;
                    _inputCaret = dest;
                    if (shift) _inputSelEnd = dest; else _inputSelStart = _inputSelEnd = dest;
                }
                else if (key == Key.Up && IsMultiLineInput(t) && !cmd)
                {
                    int dest = CaretUpLine(cur, _inputCaret);
                    _inputCaret = dest;
                    if (shift) _inputSelEnd = dest; else _inputSelStart = _inputSelEnd = dest;
                }
                else if (key == Key.Down && IsMultiLineInput(t) && !cmd)
                {
                    int dest = CaretDownLine(cur, _inputCaret);
                    _inputCaret = dest;
                    if (shift) _inputSelEnd = dest; else _inputSelStart = _inputSelEnd = dest;
                }
                else if (key == Key.Enter && IsMultiLineInput(t))
                {
                    if (canModify && target.Props.OnChange != null)
                    {
                        int selMin = Math.Min(_inputSelStart, _inputSelEnd);
                        int selMax = Math.Max(_inputSelStart, _inputSelEnd);
                        string nextText = cur[..selMin] + "\n" + cur[selMax..];
                        int nextCaret = selMin + 1;
                        _inputText = nextText;
                        target.Props.OnChange(nextText);
                        _inputCaret = _inputSelStart = _inputSelEnd = nextCaret;
                    }
                }
                else if (key == Key.Home || (cmd && key == Key.Up))
                {
                    if (shift) _inputSelEnd = 0; else _inputSelStart = _inputSelEnd = 0;
                    _inputCaret = 0;
                }
                else if (key == Key.End || (cmd && key == Key.Down))
                {
                    if (shift) _inputSelEnd = len; else _inputSelStart = _inputSelEnd = len;
                    _inputCaret = len;
                }
                else if (key == Key.Backspace || ks.Equals("Backspace", StringComparison.OrdinalIgnoreCase))
                {
                    if (canModify && target.Props.OnChange != null)
                    {
                        int selMin = Math.Min(_inputSelStart, _inputSelEnd);
                        int selMax = Math.Max(_inputSelStart, _inputSelEnd);
                        string nextText = cur;
                        int nextCaret = _inputCaret;
                        if (selMin != selMax)
                        {
                            nextText = cur[..selMin] + cur[selMax..];
                            nextCaret = selMin;
                        }
                        else if (alt && _inputCaret > 0)
                        {
                            // Option+Backspace = delete word before caret
                            int wordStart = WordStartBefore(cur, _inputCaret);
                            nextText = cur[..wordStart] + cur[_inputCaret..];
                            nextCaret = wordStart;
                        }
                        else if (_inputCaret > 0)
                        {
                            nextText = cur[..(_inputCaret - 1)] + cur[_inputCaret..];
                            nextCaret = _inputCaret - 1;
                        }
                        if (nextText != cur) { _inputText = nextText; target.Props.OnChange(nextText); _inputCaret = _inputSelStart = _inputSelEnd = nextCaret; }
                    }
                    handled = true;
                }
                else if (key == Key.Delete || ks.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                {
                    if (canModify && target.Props.OnChange != null)
                    {
                        int selMin = Math.Min(_inputSelStart, _inputSelEnd);
                        int selMax = Math.Max(_inputSelStart, _inputSelEnd);
                        string nextText = cur;
                        int nextCaret = _inputCaret;
                        if (selMin != selMax)
                        {
                            nextText = cur[..selMin] + cur[selMax..];
                            nextCaret = selMin;
                        }
                        else if (_inputCaret < len)
                        {
                            nextText = cur[.._inputCaret] + cur[(_inputCaret + 1)..];
                            nextCaret = _inputCaret;
                        }
                        if (nextText != cur) { _inputText = nextText; target.Props.OnChange(nextText); _inputCaret = _inputSelStart = _inputSelEnd = nextCaret; }
                    }
                    handled = true;
                }
                else if (shortcutMod && key == Key.A)
                {
                    _inputSelStart = 0; _inputSelEnd = len; _inputCaret = len;
                }
                else if (shortcutMod && key == Key.C)
                {
                    int a = Math.Min(_inputSelStart, _inputSelEnd), b = Math.Max(_inputSelStart, _inputSelEnd);
                    if (len > 0 && a < b) { keyboard.ClipboardText = cur[a..b]; target.Props.OnCopy?.Invoke(cur[a..b]); }
                }
                else if (shortcutMod && key == Key.X)
                {
                    if (canModify && target.Props.OnChange != null)
                    {
                        int a = Math.Min(_inputSelStart, _inputSelEnd), b = Math.Max(_inputSelStart, _inputSelEnd);
                        if (a < b) { 
                            string cutText = cur[a..b];
                            keyboard.ClipboardText = cutText; 
                            target.Props.OnCut?.Invoke(cutText);
                            var after = cur[..a] + cur[b..]; _inputText = after; target.Props.OnChange(after); _inputCaret = _inputSelStart = _inputSelEnd = a; 
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
                            int selMin = Math.Min(_inputSelStart, _inputSelEnd);
                            int selMax = Math.Max(_inputSelStart, _inputSelEnd);
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
                                _inputText = nextText;
                                target.Props.OnChange(nextText);
                                target.Props.OnPaste?.Invoke(paste);
                                _inputCaret = _inputSelStart = _inputSelEnd = nextCaret;
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
            var target = _focused;
            if (target == null) return;

            string ks = key.ToString();

            // Back-compat
            target.Props.OnKeyUp?.Invoke(ks);

            DispatchKey(target, new KeyEvent { Type = KeyEventType.Up, Key = ks });
        }

        private void OnKeyChar(IKeyboard keyboard, char c)
        {
            var target = _focused;
            if (target == null) return;

            DispatchKey(target, new KeyEvent { Type = KeyEventType.Char, Key = c.ToString(), Char = c });

            if (target.Type is not string t || !IsTextInput(t) || target.Props.OnChange == null)
                return;

            // Check readOnly and disabled states
            if (target.Props.ReadOnly || target.Props.Disabled)
                return;

            _lastInputActivityTicks = Environment.TickCount64;

            var cur = _inputText ?? target.Props.Text ?? "";
            ClampInputIndices(cur.Length, ref _inputCaret, ref _inputSelStart, ref _inputSelEnd);
            int selMin = Math.Min(_inputSelStart, _inputSelEnd);
            int selMax = Math.Max(_inputSelStart, _inputSelEnd);
            int maxLen = target.Props.MaxLength ?? int.MaxValue;

            // Backspace is handled in OnKeyDown to avoid double-delete when both KeyDown and KeyChar fire
            if (c == '\b') return;

            // Validate input type
            string? inputType = target.Props.InputType;
            bool isNumberInput = inputType == "number";

            if (IsMultiLineInput(t) && (c == '\n' || c == '\r'))
            {
                string insert = "\n";
                int insertLen = cur.Length - (selMax - selMin) + insert.Length;
                if (insertLen > maxLen) return;
                string nextText = cur[..selMin] + insert + cur[selMax..];
                int nextCaret = selMin + insert.Length;
                _inputText = nextText;
                target.Props.OnChange(nextText);
                _inputCaret = _inputSelStart = _inputSelEnd = nextCaret;
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
                _inputText = nextText;
                target.Props.OnChange(nextText);
                _inputCaret = _inputSelStart = _inputSelEnd = nextCaret;
            }
            // Per-component dirty tracking (from OnChange) handles reconcile.
            // No explicit RequestRender needed — 60fps loop handles visual refresh.
        }

        /// <summary>Moves caret up one line in a textarea, preserving column position.</summary>
        private static int CaretUpLine(string text, int caret)
        {
            // Find start of current line
            int lineStart = caret > 0 ? text.LastIndexOf('\n', caret - 1) + 1 : 0;
            int col = caret - lineStart;
            if (lineStart == 0) return 0; // Already on first line
            // Find start of previous line
            int prevLineEnd = lineStart - 1; // The '\n' before current line
            int prevLineStart = prevLineEnd > 0 ? text.LastIndexOf('\n', prevLineEnd - 1) + 1 : 0;
            int prevLineLen = prevLineEnd - prevLineStart;
            return prevLineStart + Math.Min(col, prevLineLen);
        }

        /// <summary>Moves caret down one line in a textarea, preserving column position.</summary>
        private static int CaretDownLine(string text, int caret)
        {
            int lineStart = caret > 0 ? text.LastIndexOf('\n', caret - 1) + 1 : 0;
            int col = caret - lineStart;
            int nextNewline = text.IndexOf('\n', caret);
            if (nextNewline < 0) return text.Length; // Already on last line
            int nextLineStart = nextNewline + 1;
            int nextNewline2 = text.IndexOf('\n', nextLineStart);
            int nextLineLen = nextNewline2 < 0 ? text.Length - nextLineStart : nextNewline2 - nextLineStart;
            return nextLineStart + Math.Min(col, nextLineLen);
        }

        /// <summary>Returns the index of the start of the word before <paramref name="pos"/> (Option+Left on macOS).</summary>
        private static int WordStartBefore(string text, int pos)
        {
            if (pos <= 0) return 0;
            int i = pos - 1;
            // Skip trailing spaces
            while (i > 0 && text[i] == ' ') i--;
            // Skip word characters
            while (i > 0 && text[i - 1] != ' ') i--;
            return i;
        }

        /// <summary>Returns the index just after the end of the word after <paramref name="pos"/> (Option+Right on macOS).</summary>
        private static int WordEndAfter(string text, int pos)
        {
            int len = text.Length;
            if (pos >= len) return len;
            int i = pos;
            // Skip leading spaces
            while (i < len && text[i] == ' ') i++;
            // Skip word characters
            while (i < len && text[i] != ' ') i++;
            return i;
        }

        private static void ClampInputIndices(int length, ref int caret, ref int selStart, ref int selEnd)
        {
            length = Math.Max(0, length);
            caret = Math.Clamp(caret, 0, length);
            selStart = Math.Clamp(selStart, 0, length);
            selEnd = Math.Clamp(selEnd, 0, length);
        }

        /// <summary>
        /// Hit-test the main tree then portal roots (portals render on top and win).
        /// </summary>
        private Fiber? HitTestAll(float x, float y)
        {
            Func<string, (float, float)> getScroll = p => _scrollOffsets.TryGetValue(p, out var v) ? v : (0f, 0f);
            var target = HitTest(_reconciler?.Root, x, y, "", 0, 0f, 0f, getScroll);
            if (_reconciler?.PortalRoots is { Count: > 0 } portals)
            {
                foreach (var portal in portals)
                {
                    var hit = HitTest(portal, x, y, "", 0, 0f, 0f, getScroll);
                    if (hit != null) target = hit;
                }
            }
            return target;
        }

        /// <summary>
        /// Walk the fiber tree depth-first and return the deepest fiber at (x,y).
        /// Uses path and scroll offset so hit-testing matches visible (scrolled) positions.
        /// </summary>
        private static Fiber? HitTest(Fiber? fiber, float x, float y, string parentPath, int indexInParent,
            float scrollX, float scrollY, Func<string, (float sx, float sy)> getScrollOffset)
        {
            if (fiber == null) return null;

            string path = string.IsNullOrEmpty(parentPath) ? indexInParent.ToString() : parentPath + "." + indexInParent;
            var (ox, oy) = getScrollOffset(path);
            bool isScrollable = fiber.ComputedStyle.OverflowY == Overflow.Scroll || fiber.ComputedStyle.OverflowY == Overflow.Auto
                || fiber.ComputedStyle.OverflowX == Overflow.Scroll || fiber.ComputedStyle.OverflowX == Overflow.Auto;
            float childScrollX = scrollX + (isScrollable ? ox : 0);
            float childScrollY = scrollY + (isScrollable ? oy : 0);

            // position:fixed elements are in viewport space — zero out accumulated scroll so their
            // AbsoluteX/Y are compared directly against the viewport-space pointer position.
            var fiberPos = fiber.ComputedStyle.Position ?? Position.Static;
            if (fiberPos == Position.Fixed) { scrollX = 0f; scrollY = 0f; childScrollX = 0f; childScrollY = 0f; }

            // Recurse into children — last child wins (later siblings paint on top in painter's order,
            // so fixed/high-ZIndex elements added later in the tree correctly win over earlier content).
            Fiber? childHit = null;
            int i = 0;
            for (var c = fiber.Child; c != null; c = c.Sibling, i++)
            {
                var hit = HitTest(c, x, y, path, i, childScrollX, childScrollY, getScrollOffset);
                if (hit != null) childHit = hit;
            }
            if (childHit != null) return childHit;

            // Check this fiber (in visible coords: layout minus scroll)
            var lb = fiber.Layout;
            float vx = lb.AbsoluteX - scrollX;
            float vy = lb.AbsoluteY - scrollY;
            bool contains = x >= vx && x < vx + lb.Width && y >= vy && y < vy + lb.Height;

            if (contains && fiber.ComputedStyle.PointerEvents != PointerEvents.None) return fiber;

            // Siblings are handled by the parent's children loop above — no tail call needed.
            return null;
        }

        /// <summary>Convert input position (window coords) to layout space (logical pixels).</summary>
        private static (float x, float y) ToLayoutCoords(Vector2 position)
            => (position.X, position.Y);

        private static List<Fiber> PathToRoot(Fiber target)
        {
            var path = new List<Fiber>();
            Fiber? cur = target;
            while (cur != null)
            {
                path.Add(cur);
                cur = cur.Parent;
            }
            path.Reverse();
            return path;
        }

        private void DispatchPointer(Fiber? target, PointerEvent e)
        {
            if (target == null || _reconciler?.Root == null) return;
            // Set element-local coordinates relative to the hit-test target.
            e.LocalX = e.X - target.Layout.AbsoluteX;
            e.LocalY = e.Y - target.Layout.AbsoluteY;
            e.TargetWidth  = target.Layout.Width;
            e.TargetHeight = target.Layout.Height;
            var path = PathToRoot(target);

            // Capture: root -> parent(target)
            e.Phase = EventPhase.Capturing;
            for (int i = 0; i < path.Count - 1 && !e.PropagationStopped; i++)
                InvokePointerHandlers(path[i], e, capture: true);

            // Target
            if (!e.PropagationStopped)
            {
                e.Phase = EventPhase.AtTarget;
                InvokePointerHandlers(target, e, capture: false);
                InvokePointerHandlers(target, e, capture: true);
            }

            // Bubble: parent(target) -> root
            e.Phase = EventPhase.Bubbling;
            for (int i = path.Count - 2; i >= 0 && !e.PropagationStopped; i--)
                InvokePointerHandlers(path[i], e, capture: false);
        }

        private static void InvokePointerHandlers(Fiber fiber, PointerEvent e, bool capture)
        {
            var p = fiber.Props;
            switch (e.Type)
            {
                case PointerEventType.Move:
                    (capture ? p.OnPointerMoveCapture : p.OnPointerMove)?.Invoke(e);
                    break;
                case PointerEventType.Down:
                    (capture ? p.OnPointerDownCapture : p.OnPointerDown)?.Invoke(e);
                    break;
                case PointerEventType.Up:
                    (capture ? p.OnPointerUpCapture : p.OnPointerUp)?.Invoke(e);
                    break;
                case PointerEventType.Click:
                    (capture ? p.OnPointerClickCapture : p.OnPointerClick)?.Invoke(e);
                    break;
                case PointerEventType.DoubleClick:
                    (capture ? p.OnPointerClickCapture : p.OnPointerClick)?.Invoke(e);
                    break;
                case PointerEventType.Enter:
                    p.OnPointerEnter?.Invoke(e);
                    break;
                case PointerEventType.Leave:
                    p.OnPointerLeave?.Invoke(e);
                    break;
                case PointerEventType.Wheel:
                    p.OnWheel?.Invoke(e);
                    break;
            }
        }

        private void DispatchKey(Fiber target, KeyEvent e)
        {
            var path = PathToRoot(target);

            // Capture
            e.Phase = EventPhase.Capturing;
            for (int i = 0; i < path.Count - 1 && !e.PropagationStopped; i++)
                InvokeKeyHandlers(path[i], e, capture: true);

            // Target
            if (!e.PropagationStopped)
            {
                e.Phase = EventPhase.AtTarget;
                InvokeKeyHandlers(target, e, capture: false);
                InvokeKeyHandlers(target, e, capture: true);
            }

            // Bubble
            e.Phase = EventPhase.Bubbling;
            for (int i = path.Count - 2; i >= 0 && !e.PropagationStopped; i--)
                InvokeKeyHandlers(path[i], e, capture: false);
        }

        private static void InvokeKeyHandlers(Fiber fiber, KeyEvent e, bool capture)
        {
            var p = fiber.Props;
            switch (e.Type)
            {
                case KeyEventType.Down:
                    (capture ? p.OnKeyDownEventCapture : p.OnKeyDownEvent)?.Invoke(e);
                    break;
                case KeyEventType.Up:
                    (capture ? p.OnKeyUpEventCapture : p.OnKeyUpEvent)?.Invoke(e);
                    break;
                case KeyEventType.Char:
                    (capture ? p.OnKeyCharCapture : p.OnKeyChar)?.Invoke(e);
                    break;
            }
        }

        private static bool IsFocusable(Fiber? f)
        {
            if (f == null) return false;
            
            var tabIndex = f.Props.TabIndex;
            
            // tabIndex = -1 means focusable but not in tab order
            if (tabIndex == -1) return true;
            
            // tabIndex = 0 is in tab order (default)
            // positive tabIndex means explicit tab order
            // null or not set means use default behavior
            
            if (IsTextInput(f.Type as string))
            {
                // Disabled inputs are not focusable
                if (f.Props.Disabled) return false;
                return true;
            }
            var p = f.Props;
            return p.OnKeyDownEvent != null || p.OnKeyUpEvent != null || p.OnKeyChar != null || p.OnChange != null || tabIndex != null;
        }

        /// <summary>Collects all focusable fibers from the tree in depth-first order.
        /// The caller is responsible for sorting by tabIndex if needed.</summary>
        private static void CollectFocusable(Fiber? fiber, List<Fiber> results)
        {
            if (fiber == null) return;
            if (IsFocusable(fiber)) results.Add(fiber);
            CollectFocusable(fiber.Child, results);
            CollectFocusable(fiber.Sibling, results);
        }

        private void SetFocus(Fiber? next)
        {
            if (ReferenceEquals(next, _focused)) return;

            var prev = _focused;
            _focused = next;
            _focusedPath = next != null ? GetPathString(next) : null;

            if (next != null && next.Type is string nt && IsTextInput(nt))
            {
                _inputText = next.Props.Text ?? "";
                var len = _inputText.Length;
                _inputCaret = len;
                _inputSelStart = len;
                _inputSelEnd = len;
                _lastInputActivityTicks = Environment.TickCount64;
                StartCaretBlinkTimer();
            }
            else
            {
                _inputText = null;
                StopCaretBlinkTimer();
            }

            prev?.Props.OnBlur?.Invoke();
            next?.Props.OnFocus?.Invoke();
        }

        private void StartCaretBlinkTimer()
        {
            _caretBlinkTimer ??= new System.Threading.Timer(_ => RequestRender(), null, CaretBlinkOnMs, CaretBlinkOnMs);
            _caretBlinkTimer.Change(CaretBlinkOnMs, CaretBlinkOnMs);
        }

        private void StopCaretBlinkTimer()
        {
            _caretBlinkTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        }

        /// <summary>True when the caret should be drawn: solid while recently active, else blinking (on phase).</summary>
        private bool ComputeCaretVisible()
        {
            if (_focused == null || !IsTextInput(_focused.Type as string))
                return true;
            long elapsed = Environment.TickCount64 - _lastInputActivityTicks;
            if (elapsed < CaretIdleMs) return true; // solid while typing
            return (Environment.TickCount64 % CaretBlinkPeriodMs) < CaretBlinkOnMs; // blink
        }

        /// <summary>Find the fiber at the given path (e.g. "0.2.0.1") in the current tree so focus points at the live fiber after re-render. Path is from PathToRoot (root first, then child indices).</summary>
        private static Fiber? GetFiberByPath(Fiber? root, string? path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            var parts = path.Split('.');
            Fiber? cur = root;
            // First part is root's index; descend using the rest (child index from root, then grandchild index, ...).
            for (int p = 1; p < parts.Length; p++)
            {
                if (cur == null || !int.TryParse(parts[p], out int index)) return null;
                var child = cur.Child;
                for (int i = 0; child != null && i < index; i++)
                    child = child.Sibling;
                cur = child;
            }
            return cur;
        }

        private void OnRender(double dt)
        {
            if (_gl == null || _rects == null || _viewports == null ||
                _reconciler == null || _layout == null) return;

            PreRender?.Invoke(dt);

            // Reconcile only when something requested it (setState/RequestRender or hot reload).
            bool requested = _externalRenderRequested;
            if (requested)
                _externalRenderRequested = false;
            if (requested || _reconciler.NeedsUpdate())
            {
                _reconciler.Update(_rootFactory!(), forceReconcile: requested);
                _layoutDirty = true;
                _needsLayout = true;
            }
            var root = _reconciler.Root;
            if (root == null) return;
            LayoutAndDraw();
            _layoutDirty = false; // reset after drawing — next dirty event will re-set it
            // If CSS transitions are still running, keep the animation deadline alive
            if (_renderer?.HasActiveTransitions == true)
                MarkDirty(animationSeconds: 0.1);
        }

        /// <summary>Applies styles, runs layout, and draws the current tree. Used by OnRender and after click-driven Update().</summary>
        private void LayoutAndDraw()
        {
            var root = _reconciler!.Root!;
            // Re-bind focus to the current tree so Input/Textarea get correct Props.Text after re-render
            if (_focusedPath != null)
            {
                var live = GetFiberByPath(root, _focusedPath);
                if (live != null && IsFocusable(live))
                {
                    _focused = live;
                    if (live.Type is string lt && IsTextInput(lt))
                    {
                        _inputText = live.Props.Text ?? "";
                        ClampInputIndices(_inputText.Length, ref _inputCaret, ref _inputSelStart, ref _inputSelEnd);
                    }
                }
                else
                    _focused = null;
            }
            // Re-bind _hovered to the current tree after reconcile (fiber objects are replaced each reconcile).
            if (_hoveredPath != null)
                _hovered = GetFiberByPath(root, _hoveredPath);

            // If the style registry changed (CSSS sheet loaded/reloaded, class registered),
            // mark all fibers dirty so stale cached ComputedStyles are recomputed.
            int registryVersion = Styles.Version;
            if (registryVersion != _lastStyleRegistryVersion)
            {
                InvalidateStyleTree(root);
                _lastStyleRegistryVersion = registryVersion;
                _needsLayout = true;
            }
            ApplyComputedStyles(root);

            var fbSize = _window!.FramebufferSize;
            _lastFbWidth = fbSize.X;
            _lastFbHeight = fbSize.Y;
            int layoutWidth = _width;
            int layoutHeight = _height;

            if (_layout == null || _measurer == null) return;

            // Only re-run layout when the fiber tree structure or window size changed.
            // Pure scroll/animation frames skip layout — geometry doesn't change when scrolling.
            if (_needsLayout)
            {
                _layout.GetImageSize = path =>
                {
                    var resolved = ResolveImagePath(path);
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
                _needsLayout = false;
            }

            // Update horizontal scroll for single-line input so caret stays in view
            if (_text != null && _focused != null && _focused.Type is string fit && fit == ElementTypes.Input)
            {
                var style = _focused.ComputedStyle;
                var lb = _focused.Layout;
                var inputPad = style.Padding ?? Thickness.Zero;
                float padLeft = inputPad.Left.Resolve(lb.Width);
                float padRight = inputPad.Right.Resolve(lb.Width);
                float contentW = lb.Width - padLeft - padRight;
                string text = _inputText ?? _focused.Props?.Text ?? "";
                var atlas = _text.Atlas;
                float baseSize = atlas.BaseSize > 0 ? atlas.BaseSize : 16f;
                float fontPx = style.FontSize is { } fs && !fs.IsAuto ? fs.Resolve(baseSize) : baseSize;
                float scale = baseSize > 0 ? fontPx / baseSize : 1f;
                float fullTextW = _text.MeasureWidth(text.AsSpan()) * scale;
                int caret = Math.Clamp(_inputCaret, 0, text.Length);
                float caretX = (caret <= 0 ? 0 : _text.MeasureWidth(text.AsSpan(0, caret)) * scale);
                float maxScroll = Math.Max(0, fullTextW - contentW);
                if (maxScroll <= 0)
                    _inputScrollX = 0;
                else
                {
                    const float caretMargin = 8f; // keep caret this many px from right edge when scrolling
                    _inputScrollX = Math.Clamp(caretX - contentW + caretMargin, 0, maxScroll);
                }
            }
            else
                _inputScrollX = 0;

            _gl!.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.Viewport(0, 0, (uint)fbSize.X, (uint)fbSize.Y);
            _gl.ClearColor(0.07f, 0.07f, 0.12f, 1f);
            _gl.ClearStencil(0);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.StencilBufferBit);

            // Update the long-lived renderer with per-frame state.
            var renderer = _renderer!;
            renderer.SetScreenSize(fbSize.X, fbSize.Y);
            renderer.DpiScale = _width > 0 ? fbSize.X / (float)_width : 1f;
            renderer.ScaleX   = renderer.DpiScale;
            renderer.ScaleY   = renderer.DpiScale;
            renderer.FocusedInputPath = _focused != null && IsTextInput(_focused.Type as string) ? GetPathString(_focused) : _focusedPath;
            renderer.FocusedInputText = _inputText;
            renderer.FocusedInputType = _focused?.Props?.InputType;
            renderer.FocusedInputCaret = _inputCaret;
            renderer.FocusedInputSelStart = _inputSelStart;
            renderer.FocusedInputSelEnd = _inputSelEnd;
            renderer.FocusedInputCaretVisible = ComputeCaretVisible();
            renderer.FocusedInputScrollX = _inputScrollX;
            renderer.HoveredPath = _hovered != null ? GetPathString(_hovered) : null;
            renderer.PortalRoots = _reconciler?.PortalRoots;
            renderer.Render(root);

            // Drag ghost: render the dragged fiber at the cursor with 50% opacity.
            if (_dragActive && _dragSource != null)
            {
                renderer.RenderGhost(_dragSource, _dragCursorX, _dragCursorY, 0.5f);
            }

            _rects!.Flush(fbSize.X, fbSize.Y);
            _text?.Flush(fbSize.X, fbSize.Y);
        }

        // ── GLFW cursor handles ───────────────────────────────────────────────

        private nint _glfwCursorArrow;
        private nint _glfwCursorHand;
        private nint _glfwCursorIBeam;
        private nint _glfwCursorCrosshair;
        private nint _glfwCursorEwResize;
        private Glfw? _glfw;

        private unsafe void InitGlfwCursors()
        {
            try
            {
                _glfw = Glfw.GetApi();
                if (_glfw == null) return;
                _glfwCursorArrow      = (nint)_glfw.CreateStandardCursor(CursorShape.Arrow);
                _glfwCursorHand       = (nint)_glfw.CreateStandardCursor(CursorShape.Hand);
                _glfwCursorIBeam      = (nint)_glfw.CreateStandardCursor(CursorShape.IBeam);
                _glfwCursorCrosshair  = (nint)_glfw.CreateStandardCursor(CursorShape.Crosshair);
                _glfwCursorEwResize   = (nint)_glfw.CreateStandardCursor(CursorShape.HResize);
                // GLFW has no built-in wait/not-allowed — fall back to arrow for those
            }
            catch { }
        }

        private unsafe void DestroyGlfwCursors()
        {
            if (_glfw == null) return;
            try
            {
                if (_glfwCursorArrow     != 0) _glfw.DestroyCursor((global::Silk.NET.GLFW.Cursor*)_glfwCursorArrow);
                if (_glfwCursorHand      != 0) _glfw.DestroyCursor((global::Silk.NET.GLFW.Cursor*)_glfwCursorHand);
                if (_glfwCursorIBeam     != 0) _glfw.DestroyCursor((global::Silk.NET.GLFW.Cursor*)_glfwCursorIBeam);
                if (_glfwCursorCrosshair != 0) _glfw.DestroyCursor((global::Silk.NET.GLFW.Cursor*)_glfwCursorCrosshair);
                if (_glfwCursorEwResize  != 0) _glfw.DestroyCursor((global::Silk.NET.GLFW.Cursor*)_glfwCursorEwResize);
                _glfwCursorArrow = _glfwCursorHand = _glfwCursorIBeam = _glfwCursorCrosshair = _glfwCursorEwResize = 0;
                _glfw.Dispose();
                _glfw = null;
            }
            catch { }
        }

        private unsafe void ApplyGlfwCursor(Paper.Core.Styles.Cursor cursor)
        {
            if (_glfw == null) return;
            try
            {
                nint h = _window!.Handle;
                if (h == 0) return;
                nint ptr = cursor switch
                {
                    Paper.Core.Styles.Cursor.Pointer    => _glfwCursorHand,
                    Paper.Core.Styles.Cursor.Text       => _glfwCursorIBeam,
                    Paper.Core.Styles.Cursor.Crosshair  => _glfwCursorCrosshair,
                    Paper.Core.Styles.Cursor.EwResize   => _glfwCursorEwResize,
                    Paper.Core.Styles.Cursor.None       => 0,
                    _                                   => _glfwCursorArrow,
                };
                _glfw.SetCursor((WindowHandle*)h, (global::Silk.NET.GLFW.Cursor*)ptr);
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
            _layoutDirty = true;
            _needsLayout = true;
            // Render immediately so the window content updates during drag resize.
            // Previously this caused VSync stalls (~300ms render × every pixel), but
            // now that rendering is ~1ms the VSync wait (~16ms at 60Hz) is acceptable.
            if (_gl != null && _reconciler?.Root != null)
                _window?.DoRender();
        }

        public void Shutdown() => _window?.Close();

        /// <summary>Resolve relative image paths (e.g. "Assets/test.png") relative to the app base directory so they load from output.</summary>
        private static string? ResolveImagePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(AppContext.BaseDirectory, path);
        }

        private static void InvalidateStyleTree(Fiber? fiber)
        {
            while (fiber != null)
            {
                fiber.StyleDirty = true;
                InvalidateStyleTree(fiber.Child);
                fiber = fiber.Sibling;
            }
        }

        private void ApplyComputedStyles(Fiber fiber)
        {
            if (fiber == null) return;
            var state = new InteractionState(
                Hover: ReferenceEquals(fiber, _hovered),
                Active: ReferenceEquals(fiber, _pressed),
                Focus: ReferenceEquals(fiber, _focused));

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
