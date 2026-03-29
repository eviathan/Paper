using Paper.Core.Hooks;
using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Paper.Layout;
using Paper.Rendering.Silk.NET.Text;
using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// A Paper UI surface that renders into an existing OpenGL context without creating
    /// its own window. Use this to embed Paper UI inside a game engine that owns the window.
    ///
    /// Usage:
    /// <code>
    ///   // During engine initialisation (after GL context is ready):
    ///   var ui = new PaperEmbeddedSurface(gl, logicalWidth, logicalHeight);
    ///   ui.Mount(props => MyHUD(props));
    ///
    ///   // Each frame, after the game has rendered its scene:
    ///   ui.Render(dt, framebufferWidth, framebufferHeight);
    ///
    ///   // Forward engine input events:
    ///   ui.HandleMouseMove(x, y);
    ///   ui.HandleMouseButton(x, y, button: 0, down: true);
    ///   ui.HandleWheel(x, y, deltaY: -3f);
    /// </code>
    /// </summary>
    public sealed class PaperEmbeddedSurface : IDisposable
    {
        private readonly GL _gl;
        private int _logicalW;
        private int _logicalH;

        private readonly RectBatch _rects;
        private readonly TexturedQuadRenderer _viewports;
        private readonly ImageTextureLoader _imageLoader;
        private readonly LayoutEngine _layout;
        private ILayoutMeasurer _measurer;
        private FontRegistry? _fontSet;
        private FiberRenderer? _renderer;
        private Reconciler? _reconciler;
        private Func<Props, UINode>? _rootFactory;

        private volatile bool _renderRequested = true;
        private bool _needsLayout = true;
        private int _lastStyleRegistryVersion = -1;

        // Interaction state (populated by input forwarding)
        private Fiber? _hovered;
        private Fiber? _pressed;
        private readonly Dictionary<string, (float sx, float sy)> _scrollOffsets = new();

        /// <summary>Global style registry for this surface.</summary>
        public StyleRegistry Styles { get; } = new();

        /// <summary>When true, reconciles and redraws every frame regardless of dirty state.</summary>
        public bool AlwaysRender { get; set; } = false;

        /// <param name="gl">The active OpenGL context owned by the host.</param>
        /// <param name="logicalWidth">Logical (CSS-pixel) width of the UI canvas.</param>
        /// <param name="logicalHeight">Logical (CSS-pixel) height of the UI canvas.</param>
        /// <param name="fontDir">
        /// Directory containing .ttf font files. If null, falls back to
        /// <c>Assets/fonts/</c> relative to <see cref="AppContext.BaseDirectory"/>.
        /// </param>
        public PaperEmbeddedSurface(GL gl, int logicalWidth, int logicalHeight, string? fontDir = null)
        {
            _gl = gl;
            _logicalW = logicalWidth;
            _logicalH = logicalHeight;

            _rects     = new RectBatch(gl);
            _viewports = new TexturedQuadRenderer(gl);
            _imageLoader = new ImageTextureLoader(gl);
            _layout    = new LayoutEngine();
            _measurer  = new FallbackLayoutMeasurer();

            gl.Enable(EnableCap.Blend);
            gl.BlendFuncSeparate(
                BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                BlendingFactor.Zero,     BlendingFactor.One);

            LoadFonts(fontDir ?? Path.Combine(AppContext.BaseDirectory, "Assets", "fonts"));

            _renderer = new FiberRenderer(_rects, _viewports, _fontSet, logicalWidth, logicalHeight, gl)
            {
                GetScrollOffset  = path => _scrollOffsets.TryGetValue(path, out var v) ? v : (0f, 0f),
                GetImageTexture  = path => _imageLoader.GetOrLoad(path).Handle,
                GetImageResult   = path =>
                {
                    var r = _imageLoader.GetOrLoad(path);
                    return r.Handle != 0 ? (r.Handle, r.Width, r.Height) : (0u, 0, 0);
                },
            };

            _reconciler = new Reconciler();

            var prevRequest = RenderScheduler.OnRenderRequested;
            RenderScheduler.OnRenderRequested = () =>
            {
                prevRequest?.Invoke();
                _renderRequested = true;
            };
        }

        /// <summary>Set the root component (replaces any existing mount).</summary>
        public void Mount(Func<Props, UINode> rootComponent)
        {
            _rootFactory = rootComponent;
            _renderRequested = true;
            _needsLayout = true;
            if (_reconciler != null && _rootFactory != null)
                _reconciler.Mount(_rootFactory(Props.Empty));
        }

        /// <summary>Force a re-render on the next <see cref="Render"/> call.</summary>
        public void RequestRender() => _renderRequested = true;

        /// <summary>
        /// Update the logical canvas size (e.g. after window resize).
        /// <paramref name="framebufferWidth"/> and <paramref name="framebufferHeight"/> are
        /// the pixel dimensions of the framebuffer (may differ on HiDPI displays).
        /// </summary>
        public void Resize(int logicalWidth, int logicalHeight)
        {
            _logicalW    = logicalWidth;
            _logicalH    = logicalHeight;
            _needsLayout = true;
            _renderRequested = true;
        }

        /// <summary>
        /// Reconcile, lay out and render the Paper UI tree onto the currently bound framebuffer.
        /// Call this each frame AFTER the game has already rendered its scene geometry.
        /// The surface does NOT clear the framebuffer — it composites the UI on top.
        /// </summary>
        /// <param name="dt">Delta time in seconds (used by CSS transitions).</param>
        /// <param name="framebufferWidth">Physical framebuffer width in pixels.</param>
        /// <param name="framebufferHeight">Physical framebuffer height in pixels.</param>
        public void Render(double dt, int framebufferWidth, int framebufferHeight)
        {
            if (_reconciler == null || _renderer == null || _rootFactory == null) return;

            bool requested = _renderRequested;
            if (requested) _renderRequested = false;

            if (AlwaysRender || requested || _reconciler.NeedsUpdate())
            {
                _reconciler.Update(_rootFactory(Props.Empty), forceReconcile: requested);
                _needsLayout = true;
            }

            var root = _reconciler.Root;
            if (root == null) return;

            // Style resolution
            int regVer = Styles.Version;
            if (regVer != _lastStyleRegistryVersion)
            {
                InvalidateStyleTree(root);
                _lastStyleRegistryVersion = regVer;
                _needsLayout = true;
            }
            ApplyComputedStyles(root);

            // Layout
            if (_needsLayout)
            {
                _layout.Layout(root, _logicalW, _logicalH, _measurer);
                if (_reconciler.PortalRoots is { Count: > 0 } portals)
                    foreach (var p in portals)
                    {
                        ApplyComputedStyles(p);
                        _layout.Layout(p, _logicalW, _logicalH, _measurer);
                    }
                _needsLayout = false;
            }

            // Render UI over whatever is currently in the framebuffer (no clear)
            float dpi = _logicalW > 0 ? framebufferWidth / (float)_logicalW : 1f;
            _renderer.SetScreenSize(framebufferWidth, framebufferHeight);
            _renderer.DpiScale = dpi;
            _renderer.ScaleX   = dpi;
            _renderer.ScaleY   = dpi;
            _renderer.HoveredPath = _hovered != null ? GetPathString(_reconciler.Root, _hovered) : null;
            _renderer.PortalRoots = _reconciler.PortalRoots;
            _renderer.Render(root);

            if (_renderer.HasActiveTransitions)
                _renderRequested = true;
        }

        // ── Input forwarding ──────────────────────────────────────────────────
        // The host engine calls these from its own input handlers.

        /// <summary>Forward a mouse move event (logical coordinates).</summary>
        public void HandleMouseMove(float x, float y)
        {
            if (_reconciler?.Root == null) return;
            var hit = HitTestAll(_reconciler.Root, x, y);
            if (!ReferenceEquals(hit, _hovered))
            {
                _hovered = hit;
                _renderRequested = true;
            }
        }

        /// <summary>Forward a mouse button press or release (logical coordinates). button=0 is left.</summary>
        public void HandleMouseButton(float x, float y, int button, bool down)
        {
            if (_reconciler?.Root == null || button != 0) return;

            var hit = HitTestAll(_reconciler.Root, x, y);

            if (down)
            {
                _pressed = hit;
                _renderRequested = true;
            }
            else
            {
                if (_pressed != null && ReferenceEquals(_pressed, hit))
                {
                    // Fire click
                    var e = new Paper.Core.Events.PointerEvent { X = x, Y = y };
                    DispatchPointerEvent(_reconciler.Root, hit, e, fiber => fiber.Props?.OnPointerClick);
                    _reconciler.Update(_rootFactory!(Props.Empty), forceReconcile: false);
                    _needsLayout = true;
                }
                _pressed = null;
                _renderRequested = true;
            }
        }

        /// <summary>Forward a mouse wheel scroll (logical coordinates, delta in notches).</summary>
        public void HandleWheel(float x, float y, float deltaY)
        {
            if (_reconciler?.Root == null) return;
            var hit = HitTestAll(_reconciler.Root, x, y);
            if (hit == null) return;

            var e = new Paper.Core.Events.PointerEvent { X = x, Y = y, WheelDeltaY = deltaY };

            // Walk up the fiber tree looking for an onWheel handler or a scrollable container
            var fiber = hit;
            while (fiber != null)
            {
                if (fiber.Props?.Get<Action<Paper.Core.Events.PointerEvent>>("onWheel") is { } onWheel)
                {
                    onWheel(e);
                    _reconciler.Update(_rootFactory!(Props.Empty), forceReconcile: false);
                    _needsLayout = true;
                    _renderRequested = true;
                    return;
                }
                var ovfY = fiber.ComputedStyle.OverflowY ?? Overflow.Visible;
                if (ovfY == Overflow.Scroll || ovfY == Overflow.Auto)
                {
                    var path = GetPathString(_reconciler.Root, fiber) ?? "";
                    var (sx, sy) = _scrollOffsets.TryGetValue(path, out var prev) ? prev : (0f, 0f);
                    _scrollOffsets[path] = (sx, sy - deltaY * 24f);
                    _renderRequested = true;
                    return;
                }
                fiber = fiber.Parent;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ApplyComputedStyles(Fiber? fiber)
        {
            if (fiber == null) return;
            var state = new InteractionState(
                Hover:  ReferenceEquals(fiber, _hovered),
                Active: ReferenceEquals(fiber, _pressed),
                Focus:  false);

            if (fiber.StyleDirty || fiber.CachedInteractionState != state)
            {
                fiber.ComputedStyle = StyleResolver.Resolve(fiber.Type, fiber.Props, Styles, state, fiber);
                fiber.StyleDirty = false;
                fiber.CachedInteractionState = state;
            }

            var child = fiber.Child;
            while (child != null) { ApplyComputedStyles(child); child = child.Sibling; }
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

        private Fiber? HitTestAll(Fiber? root, float x, float y)
        {
            Fiber? hit = null;
            Func<string, (float, float)> getScroll = p =>
                _scrollOffsets.TryGetValue(p, out var v) ? v : (0f, 0f);

            hit = HitTest(root, x, y, "", 0, 0f, 0f, getScroll);

            if (_reconciler?.PortalRoots is { Count: > 0 } portals)
                foreach (var portal in portals)
                {
                    var h = HitTest(portal, x, y, "", 0, 0f, 0f, getScroll);
                    if (h != null) hit = h;
                }
            return hit;
        }

        private static Fiber? HitTest(Fiber? fiber, float x, float y, string parentPath, int idx,
            float scrollX, float scrollY, Func<string, (float, float)> getScroll)
        {
            if (fiber == null) return null;

            string path = string.IsNullOrEmpty(parentPath) ? idx.ToString() : parentPath + "." + idx;
            var (ox, oy) = getScroll(path);
            bool isScrollable = fiber.ComputedStyle.OverflowY is Overflow.Scroll or Overflow.Auto
                             || fiber.ComputedStyle.OverflowX is Overflow.Scroll or Overflow.Auto;
            float csX = scrollX + (isScrollable ? ox : 0);
            float csY = scrollY + (isScrollable ? oy : 0);

            var pos = fiber.ComputedStyle.Position ?? Position.Static;
            if (pos == Position.Fixed) { scrollX = 0f; scrollY = 0f; csX = 0f; csY = 0f; }

            Fiber? childHit = null;
            int i = 0;
            for (var c = fiber.Child; c != null; c = c.Sibling, i++)
            {
                var h = HitTest(c, x, y, path, i, csX, csY, getScroll);
                if (h != null) childHit = h;
            }
            if (childHit != null) return childHit;

            var lb = fiber.Layout;
            float vx = lb.AbsoluteX - scrollX;
            float vy = lb.AbsoluteY - scrollY;
            bool contains = x >= vx && x < vx + lb.Width && y >= vy && y < vy + lb.Height;
            if (contains && fiber.ComputedStyle.PointerEvents != PointerEvents.None) return fiber;
            return null;
        }

        private static void DispatchPointerEvent(
            Fiber root, Fiber target,
            Paper.Core.Events.PointerEvent e,
            Func<Fiber, Action<Paper.Core.Events.PointerEvent>?> getHandler)
        {
            // Walk ancestors and fire the event bubbling up
            var chain = new List<Fiber>();
            var cur = target;
            while (cur != null) { chain.Add(cur); cur = cur.Parent; }
            chain.Reverse(); // root-first for capture phase (simple: just bubble for now)
            chain.Reverse();
            foreach (var f in chain)
            {
                var handler = getHandler(f);
                handler?.Invoke(e);
            }
        }

        /// <summary>Returns the stable dot-separated path string for a fiber, or null if not found.</summary>
        private static string? GetPathString(Fiber? root, Fiber? target)
        {
            if (root == null || target == null) return null;
            return TryGetPath(root, target, "", 0);
        }

        private static string? TryGetPath(Fiber? fiber, Fiber target, string parentPath, int idx)
        {
            if (fiber == null) return null;
            string path = string.IsNullOrEmpty(parentPath) ? idx.ToString() : parentPath + "." + idx;
            if (ReferenceEquals(fiber, target)) return path;
            var r = TryGetPath(fiber.Child, target, path, 0);
            if (r != null) return r;
            return TryGetPath(fiber.Sibling, target, parentPath, idx + 1);
        }

        private void LoadFonts(string fontDir)
        {
            var registry = new FontRegistry();
            if (!Directory.Exists(fontDir)) return;

            foreach (var regularPath in Directory.GetFiles(fontDir, "*.ttf"))
            {
                var fname = Path.GetFileNameWithoutExtension(regularPath);
                if (fname.EndsWith("-bold",      StringComparison.OrdinalIgnoreCase)) continue;
                if (fname.EndsWith("-italic",    StringComparison.OrdinalIgnoreCase)) continue;
                if (fname.EndsWith("-bolditalic",StringComparison.OrdinalIgnoreCase)) continue;
                if (fname.EndsWith("bold",       StringComparison.OrdinalIgnoreCase) && fname.Length > 4) continue;

                bool isIcon = fname.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0;
                var atlasLoader = isIcon
                    ? (Func<GL, string, Dictionary<int, PaperFontAtlas>>)((g, p) => PaperFontLoader.LoadIconSet(g, p))
                    : (g, p) => PaperFontLoader.LoadSet(g, p);

                var regular = new PaperFontSet(atlasLoader(_gl, regularPath), _gl);

                PaperFontSet? bold = null;
                foreach (var s in new[] { "-bold", "-Bold", "bold", "Bold" })
                {
                    var p = Path.Combine(fontDir, fname + s + ".ttf");
                    if (File.Exists(p)) { bold = new PaperFontSet(PaperFontLoader.LoadSet(_gl, p), _gl); break; }
                }
                PaperFontSet? italic = null;
                foreach (var s in new[] { "-italic", "-Italic" })
                {
                    var p = Path.Combine(fontDir, fname + s + ".ttf");
                    if (File.Exists(p)) { italic = new PaperFontSet(PaperFontLoader.LoadSet(_gl, p), _gl); break; }
                }
                PaperFontSet? boldItalic = null;
                foreach (var s in new[] { "-bolditalic", "-BoldItalic", "-bold-italic" })
                {
                    var p = Path.Combine(fontDir, fname + s + ".ttf");
                    if (File.Exists(p)) { boldItalic = new PaperFontSet(PaperFontLoader.LoadSet(_gl, p), _gl); break; }
                }

                registry.Register(fname.ToLowerInvariant(), regular, bold, italic, boldItalic);
            }

            if (registry.Default != null)
            {
                _fontSet  = registry;
                _measurer = new SilkTextMeasurer(registry);
            }
        }

        public void Dispose()
        {
            _rects.Dispose();
        }
    }
}
