using Paper.Core.Hooks;
using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Paper.Layout;
using Paper.Rendering.Silk.NET.Text;
using Paper.Rendering.Silk.NET.Utilities;
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
    public sealed partial class PaperEmbeddedSurface : IDisposable
    {
        private readonly GL _gl;
        private int _logicalW;
        private int _logicalH;
        private readonly Func<string>? _getClipboard;
        private readonly Action<string>? _setClipboard;

        private readonly RectBatch _rects;
        private readonly TexturedQuadRenderer _viewports;
        private readonly ImageTextureLoader _imageLoader;
        private readonly Paper.Icons.IconTextureCache _iconTextureCache;
        private readonly SpriteTextureCache _spriteTextureCache;
        private readonly LayoutEngine _layout;
        private ILayoutMeasurer _measurer;
        private FontRegistry? _fontSet;
        private FiberRenderer? _renderer;
        private Reconciler? _reconciler;
        private Func<Props, UINode>? _rootFactory;

        private volatile bool _renderRequested = true;
        private readonly Action _renderRequestedListener;
        private bool _needsLayout = true;
        private int _lastStyleRegistryVersion = -1;

        // Interaction state (populated by input forwarding). Every one of these also has a
        // *Path string counterpart and gets re-bound to the live tree in Render() right after each
        // reconcile — Reconciler.Render always allocates a brand-new Fiber object for every node
        // on every Update (it only carries HookSlots/Instance over, not identity), so a raw Fiber
        // reference captured before a reconcile is guaranteed to no longer ReferenceEquals anything
        // in the tree after one. Canvas.Rendering.cs's LayoutAndDraw does the exact same re-bind
        // for its own Hovered/Focused/pointer-down/drag fibers — this mirrors that established
        // pattern rather than inventing a new one.
        private Fiber? _hovered;
        private string? _hoveredPath;
        private Fiber? _pressed;
        private string? _pressedPath;
        private readonly Dictionary<string, (float sx, float sy)> _scrollOffsets = new();
        private readonly Dictionary<string, double> _scrollbarLastActive = new();

        // Click-and-drag on a scrollbar thumb — ported from Canvas.Mouse.cs/Canvas.MouseMovement.cs,
        // which track this the same way (a path string, not a fiber reference, since the whole
        // point is surviving across the reconciles a drag's own scroll updates trigger).
        private string? _scrollbarDragPath;
        private float _scrollbarDragAnchorY;
        private float _scrollbarDragAnchorScroll;

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
        /// <param name="getClipboard">Read the host's clipboard text, for Ctrl/Cmd+C/X and paste
        /// in text inputs. Paper has no clipboard API of its own — omit to disable copy/cut/paste
        /// (typing, selection, and everything else still works).</param>
        /// <param name="setClipboard">Write to the host's clipboard, paired with <paramref name="getClipboard"/>.</param>
        public PaperEmbeddedSurface(
            GL gl, int logicalWidth, int logicalHeight, string? fontDir = null,
            Func<string>? getClipboard = null, Action<string>? setClipboard = null)
        {
            _gl = gl;
            _logicalW = logicalWidth;
            _logicalH = logicalHeight;
            _getClipboard = getClipboard;
            _setClipboard = setClipboard;

            _rects     = new RectBatch(gl);
            _viewports = new TexturedQuadRenderer(gl);
            _imageLoader = new ImageTextureLoader(gl);
            _iconTextureCache = new Paper.Icons.IconTextureCache(gl);
            _spriteTextureCache = new SpriteTextureCache(gl);
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
                // Without this the scrollbar track/thumb are never actually drawn — FiberRenderer
                // only records their hit geometry when opacity is 0, it never draws them (see
                // RenderFiberChildren's scrollClip branch). Canvas wires the identical fade
                // (visible while recently scrolled, fades out after) via its own _scrollState;
                // this ports that behaviour using _scrollbarLastActive instead.
                GetScrollbarOpacity = path =>
                {
                    if (!_scrollbarLastActive.TryGetValue(path, out double lastActive))
                        return 0f;
                    double elapsed = (DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond) - lastActive;
                    const double visibleSeconds = 1.2, fadeSeconds = 0.4;
                    if (elapsed < visibleSeconds) return 1f;
                    if (elapsed >= visibleSeconds + fadeSeconds) return 0f;
                    return 1f - (float)((elapsed - visibleSeconds) / fadeSeconds);
                },
                GetImageTexture  = path => _imageLoader.GetOrLoad(path).Handle,
                GetImageResult   = path =>
                {
                    var r = _imageLoader.GetOrLoad(path);
                    return r.Handle != 0 ? (r.Handle, r.Width, r.Height) : (0u, 0, 0);
                },
                // Without this, ElementTypes.Icon's render branch (`if (iconRef.Set != null &&
                // GetIconTexture != null)`) never rasterizes anything and every UI.Icon(...) is
                // silently a no-op — never wired here even though Canvas has always had it.
                GetIconTexture = (iconRef, sizePx, r, g, b, a) => _iconTextureCache.GetTexture(iconRef, sizePx, r, g, b, a),
                GetSpriteTexture = (path, frameIndex, frameW, frameH, sizePx) =>
                    _spriteTextureCache.GetTexture(path, frameIndex, frameW, frameH, sizePx),
            };

            _reconciler = new Reconciler();
            _renderRequestedListener = () => _renderRequested = true;
            RenderScheduler.AddListener(_renderRequestedListener);
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

            // Reset GL state — the host's own game render may leave non-standard state active
            // (depth/stencil test enabled, a clipped scissor rect from its own UI, a non-default
            // blend func), and unlike Canvas this surface doesn't own the frame's only render
            // pass, so it can't assume state starts clean.
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.Viewport(0, 0, (uint)framebufferWidth, (uint)framebufferHeight);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFuncSeparate(
                BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                BlendingFactor.Zero,     BlendingFactor.One);
            _gl.Disable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.StencilTest);
            _gl.Disable(EnableCap.ScissorTest);
            _gl.ColorMask(true, true, true, true);

            bool requested = _renderRequested;
            if (requested) _renderRequested = false;

            if (AlwaysRender || requested || _reconciler.NeedsUpdate())
            {
                _reconciler.Update(_rootFactory(Props.Empty), forceReconcile: requested);
                _needsLayout = true;

                // AutoFocus: if a newly-mounted input has autoFocus=true, focus it.
                var autoFocusFiber = FindAutoFocus(_reconciler.Root);
                if (autoFocusFiber != null)
                {
                    var autoFocusPath = GetPathString(_reconciler.Root, autoFocusFiber);
                    if (autoFocusPath != _inputState.FocusedPath)
                        SetFocus(autoFocusFiber);
                }

                // Re-bind every cross-render fiber reference to the tree that was just built —
                // see the field comments on _hovered/_pressed for why this is required after
                // every reconcile, not just a nice-to-have.
                RebindTrackedFibers();
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

            // Computed before layout (not just before render) so that if layout runs this frame,
            // it measures text against the same DPI-scaled font atlas DrawText will later render
            // with — see SilkTextMeasurer.DpiScale's remarks. A stale measurer DpiScale here would
            // mean layout allocates boxes sized for the *previous* frame's DPI.
            float dpi = _logicalW > 0 ? framebufferWidth / (float)_logicalW : 1f;
            if (_measurer is Text.SilkTextMeasurer stm) stm.DpiScale = dpi;

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
            _renderer.SetScreenSize(framebufferWidth, framebufferHeight);
            _renderer.DpiScale = dpi;
            _renderer.ScaleX   = dpi;
            _renderer.ScaleY   = dpi;
            _renderer.HoveredPath = _hovered != null ? GetPathString(_reconciler.Root, _hovered) : null;
            _renderer.PortalRoots = _reconciler.PortalRoots;
            _renderer.FocusedInputPath = _inputState.FocusedPath;
            _renderer.FocusedInputText = _inputState.InputText;
            _renderer.FocusedInputType = _inputState.Focused?.Props?.InputType;
            _renderer.FocusedInputCaret = _inputState.InputCaret;
            _renderer.FocusedInputSelStart = _inputState.InputSelStart;
            _renderer.FocusedInputSelEnd = _inputState.InputSelEnd;
            _renderer.FocusedInputCaretVisible = ComputeCaretVisible();
            // No horizontal scroll-into-view for single-line inputs wider than their box — unlike
            // Canvas, which computes this from measured caret position each frame. Text entry,
            // selection and editing all work regardless; the caret can just end up temporarily
            // out of view for a long value in a narrow field.
            _renderer.FocusedInputScrollX = 0f;
            _renderer.Render(root);

            // FiberRenderer batches draw calls into _rects/the font's TextBatch rather than
            // issuing them immediately — Canvas.Rendering.cs flushes both right after its own
            // equivalent Render(root) call; without this nothing actually reaches the GPU.
            // TODO: LineBatch is never constructed/passed to FiberRenderer in this class, so
            // FlushLines is skipped — wire one in if a future overlay uses line-drawing elements.
            _rects.Flush(framebufferWidth, framebufferHeight);
            _fontSet?.Default?.Flush(framebufferWidth, framebufferHeight);

            if (_renderer.HasActiveTransitions)
                _renderRequested = true;
        }

        /// <summary>Re-resolves every fiber reference this class holds across renders (hover,
        /// press, focus) against the tree that was just (re)built, by looking each one up via its
        /// stable path string. Without this, e.g. a press captured before a reconcile can never
        /// ReferenceEquals the fiber found by hit-testing after one, even for the exact same
        /// on-screen element — silently breaking every click, since a reconcile happens on
        /// essentially every mouse-down (it sets _renderRequested itself).</summary>
        private void RebindTrackedFibers()
        {
            var root = _reconciler?.Root;
            if (root == null) return;

            if (_hoveredPath != null)
                _hovered = FiberTreeUtility.GetFiberByPath(root, _hoveredPath);

            if (_pressedPath != null)
                _pressed = FiberTreeUtility.GetFiberByPath(root, _pressedPath);

            if (_inputState.FocusedPath != null)
            {
                var liveFocused = FiberTreeUtility.GetFiberByPath(root, _inputState.FocusedPath);
                _inputState.Focused = liveFocused != null && HitTestUtility.IsFocusable(liveFocused)
                    ? liveFocused
                    : null;
            }
        }

        // ── Input forwarding ──────────────────────────────────────────────────
        // The host engine calls these from its own input handlers.

        /// <summary>Forward a mouse move event (logical coordinates).</summary>
        public void HandleMouseMove(float x, float y)
        {
            if (_reconciler?.Root == null) return;

            // A scrollbar-thumb drag in progress owns mouse-move entirely until release (see
            // HandleMouseButton) — ported from Canvas.MouseMovement.cs's HandleScrollbarThumbDrag.
            // Unlike Canvas, this doesn't need to re-check "is the button still down": there's no
            // continuous polling here, only discrete Handle* calls, and HandleMouseButton(down:
            // false) is guaranteed to fire and clear _scrollbarDragPath on release (including a
            // fast tap — see PaperOverlay's edge-triggered JustPressed/JustReleased), so this flag
            // alone is a reliable "currently dragging" signal.
            if (_scrollbarDragPath != null)
            {
                if (_renderer != null && _renderer.RenderedScrollbars.TryGetValue(_scrollbarDragPath, out var scrollbar))
                {
                    float dragDelta = y - _scrollbarDragAnchorY;
                    float usableTrackHeight = scrollbar.TrackH - scrollbar.ThumbH;
                    float scrollDelta = usableTrackHeight > 0 ? dragDelta * scrollbar.MaxScroll / usableTrackHeight : 0f;
                    float newScrollY = Math.Clamp(_scrollbarDragAnchorScroll + scrollDelta, 0f, Math.Max(0f, scrollbar.MaxScroll));
                    var (currentScrollX, _) = _scrollOffsets.TryGetValue(_scrollbarDragPath, out var currentOffsets) ? currentOffsets : (0f, 0f);
                    _scrollOffsets[_scrollbarDragPath] = (currentScrollX, newScrollY);
                    _scrollbarLastActive[_scrollbarDragPath] = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
                    _renderRequested = true;
                }
                return;
            }

            var hit = HitTestAll(_reconciler.Root, x, y);
            if (!ReferenceEquals(hit, _hovered))
            {
                _hovered = hit;
                _hoveredPath = hit != null ? GetPathString(_reconciler.Root, hit) : null;
                _renderRequested = true;
            }
        }

        /// <summary>Forward a mouse button press or release (logical coordinates). button=0 is left.</summary>
        public void HandleMouseButton(float x, float y, int button, bool down)
        {
            if (_reconciler?.Root == null || button != 0) return;

            if (down)
            {
                // Scrollbar-thumb hit-test takes priority over the normal press/click path —
                // ported from Canvas.Mouse.cs's OnMouseButtonDown. Only the thumb itself (not the
                // whole track) starts a drag, matching the macOS-overlay-scrollbar convention
                // DrawScrollbar already draws to (a fixed 6px-wide thumb).
                if (_renderer != null)
                {
                    foreach (var (path, scrollbar) in _renderer.RenderedScrollbars)
                    {
                        if (x >= scrollbar.TrackX && x <= scrollbar.TrackX + 6f &&
                            y >= scrollbar.ThumbY && y <= scrollbar.ThumbY + scrollbar.ThumbH)
                        {
                            _scrollbarDragPath = path;
                            _scrollbarDragAnchorY = y;
                            _scrollbarDragAnchorScroll = _scrollOffsets.TryGetValue(path, out var savedScroll) ? savedScroll.sy : 0f;
                            return;
                        }
                    }
                }

                var hit = HitTestAll(_reconciler.Root, x, y);
                _pressed = hit;
                _pressedPath = hit != null ? GetPathString(_reconciler.Root, hit) : null;
                SetFocus(hit != null && HitTestUtility.IsFocusable(hit) ? hit : null);
                _renderRequested = true;
            }
            else
            {
                if (_scrollbarDragPath != null)
                {
                    _scrollbarDragPath = null;
                    return;
                }

                var hit = HitTestAll(_reconciler.Root, x, y);

                // Compare by path, not by fiber reference: a reconcile happens on essentially
                // every press (it flips _renderRequested itself), and Reconciler.Render allocates
                // a brand-new Fiber for every node on every Update, so a reference captured at
                // press time never survives to release time even for the same on-screen element —
                // RebindTrackedFibers() keeps _pressed pointing at the live tree, but the path is
                // the actually-stable identity here.
                bool samePress = _pressedPath != null && hit != null &&
                                  _pressedPath == GetPathString(_reconciler.Root, hit);
                if (samePress)
                {
                    // Fire click
                    var e = new Paper.Core.Events.PointerEvent { X = x, Y = y };
                    DispatchPointerEvent(_reconciler.Root, hit, e, fiber => fiber.Props?.OnPointerClick);
                    _reconciler.Update(_rootFactory!(Props.Empty), forceReconcile: false);
                    _needsLayout = true;
                }
                _pressed = null;
                _pressedPath = null;
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
                    float newSy = sy - deltaY * 24f;
                    // Clamp to the content's actual scroll range from the last render pass (one
                    // frame stale, same as Canvas's own wheel handler) — without this, the offset
                    // grows/shrinks without bound and content can scroll arbitrarily far past
                    // either end.
                    if (_renderer != null && _renderer.RenderedScrollbars.TryGetValue(path, out var scrollbar))
                        newSy = Math.Clamp(newSy, 0f, Math.Max(0f, scrollbar.MaxScroll));
                    else
                        newSy = Math.Max(0f, newSy);
                    _scrollOffsets[path] = (sx, newSy);
                    _scrollbarLastActive[path] = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
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
                Focus:  ReferenceEquals(fiber, _inputState.Focused));

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
            Func<string, (float, float)> getScroll = p =>
                _scrollOffsets.TryGetValue(p, out var v) ? v : (0f, 0f);

            // HitTestUtility.HitTest (not a locally-duplicated walk) — it clips hit-testing to a
            // scrollable/overflow-hidden container's own visible bounds, which the old local copy
            // here didn't: without that check, a child scrolled out of view (its layout position
            // still exists, just off-screen) stayed hit-testable at its stale on-screen rect and
            // could steal clicks meant for whatever's actually drawn there — e.g. sort/filter
            // buttons sitting above a scrolled grid.
            Fiber? hit = HitTestUtility.HitTest(root, x, y, "", 0, 0f, 0f, getScroll);

            if (_reconciler?.PortalRoots is { Count: > 0 } portals)
                foreach (var portal in portals)
                {
                    var h = HitTestUtility.HitTest(portal, x, y, "", 0, 0f, 0f, getScroll);
                    if (h != null) hit = h;
                }
            return hit;
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
            RenderScheduler.RemoveListener(_renderRequestedListener);
            _reconciler?.Dispose();
            _rects.Dispose();
            _iconTextureCache.Dispose();
            _spriteTextureCache.Dispose();
        }
    }
}
