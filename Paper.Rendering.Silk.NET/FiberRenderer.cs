using System.Collections.Generic;
using System.Text.Json;
using Paper.Core.Markdown;
using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Paper.Layout;
using Paper.Rendering.Silk.NET.Text;
using Silk.NET.OpenGL;
using Direction = Paper.Core.Styles.Direction;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// Walks a committed fiber tree (after layout) and issues draw calls.
    /// Handles: background, borders, border-radius, opacity, text, viewport elements, overflow clip/scroll.
    /// </summary>
    internal sealed class FiberRenderer
    {
        private readonly RectBatch _rects;
        private readonly TexturedQuadRenderer _viewports;
        private readonly FontRegistry? _fonts;
        // Convenience accessor for places that only need the default (16px) atlas metrics.
        private TextBatch? _text => _fonts?.Default;
        private readonly GL? _gl;
        private float _screenW;
        private float _screenH;
        private int _stencilDepth; // current stencil nesting depth for rounded overflow:hidden clips

        // Ghost rendering offset: layout-space offset applied to all positions during a ghost pass.
        private float _ghostOffsetX;
        private float _ghostOffsetY;

        // ── Viewport culling ──────────────────────────────────────────────────
        // Current visible clip rect in screen pixels. Narrowed when entering scroll/clip containers
        // so off-screen children can be skipped without GPU draw calls.
        private (float X, float Y, float W, float H) _cullRect;

        public record struct ScrollbarHit(float TrackX, float TrackY, float TrackH, float ThumbY, float ThumbH, float MaxScroll, float MaxScrollX);
        public readonly Dictionary<string, ScrollbarHit> RenderedScrollbars = new();

        // ── Markdown editor render cache ──────────────────────────────────────
        // Pre-computes token segments with x-offsets so the hot render loop does
        // zero MeasureWidth calls — just batch.Add per visible segment per row.
        private readonly record struct MdSegment(string Text, float XOffset, float R, float G, float B, float A);
        private sealed class MdCache
        {
            public int TextHash; public int TextLen;
            public int WidthInt; public int FontPxInt;
            // Rows: needed for selection/caret hit-testing (char offsets).
            public (string Text, int Start, int End)[] Rows = Array.Empty<(string, int, int)>();
            // RowSegments: pre-computed (text, xOffset, color) — used for rendering, no MeasureWidth needed.
            public MdSegment[][] RowSegments = Array.Empty<MdSegment[]>();
        }
        // One entry per MarkdownEditor path.
        private readonly Dictionary<string, MdCache> _mdCache = new();


        /// <summary>Optional: returns current scrollbar thumb opacity [0,1] for a given scroll container path (used for fade-out).</summary>
        public Func<string, float>? GetScrollbarOpacity { get; set; }

        /// <summary>Scale from layout space to framebuffer (e.g. when layout uses window size but we draw to framebuffer). Default 1,1.</summary>
        public float ScaleX { get; set; } = 1f;
        public float ScaleY { get; set; } = 1f;

        /// <summary>Physical pixel density scale (framebuffer / logical window). Used to size UI chrome (scrollbars) in logical pixels.</summary>
        public float DpiScale { get; set; } = 1f;

        /// <summary>Optional: returns (scrollX, scrollY) for a node path (e.g. "0.1.2") for overflow scroll.</summary>
        public Func<string, (float scrollX, float scrollY)>? GetScrollOffset { get; set; }

        /// <summary>Optional: returns texture handle for an image path (used when GetImageResult is not set).</summary>
        public Func<string?, uint>? GetImageTexture { get; set; }

        /// <summary>Optional: returns (handle, width, height) for object-fit and background-image (cover/contain).</summary>
        public Func<string?, (uint handle, int w, int h)>? GetImageResult { get; set; }

        /// <summary>Path of the currently hovered fiber (for hover highlight on interactive elements).</summary>
        public string? HoveredPath { get; set; }

        /// <summary>When set, the Input/Textarea at this path gets selection highlight and caret drawn.</summary>
        public string? FocusedInputPath { get; set; }
        /// <summary>When set, this text is drawn for the focused input instead of Props.Text (so rapid typing shows immediately).</summary>
        public string? FocusedInputText { get; set; }
        public int FocusedInputCaret { get; set; }
        public int FocusedInputSelStart { get; set; }
        public int FocusedInputSelEnd { get; set; }
        /// <summary>When false, caret is not drawn (blink off phase or no focus).</summary>
        public bool FocusedInputCaretVisible { get; set; } = true;
        /// <summary>Horizontal scroll offset for single-line input when text overflows.</summary>
        public float FocusedInputScrollX { get; set; }
        /// <summary>Input type for the focused input (e.g., "password" for masking).</summary>
        public string? FocusedInputType { get; set; }

        public FiberRenderer(RectBatch rects, TexturedQuadRenderer viewports,
                             FontRegistry? fonts, float screenW, float screenH,
                             GL? gl = null)
        {
            _rects = rects;
            _viewports = viewports;
            _fonts = fonts;
            _gl = gl;
            _screenW = screenW;
            _screenH = screenH;
        }

        /// <summary>Update the screen dimensions (call each frame if the window has been resized).</summary>
        public void SetScreenSize(float w, float h) { _screenW = w; _screenH = h; }

        // ── CSS Transitions ───────────────────────────────────────────────────

        private sealed class TransitionState
        {
            public PaperColour BgCurrent;
            public PaperColour BgTarget;
            public float OpacityCurrent = 1f;
            public float OpacityTarget  = 1f;
            public bool Initialized;
        }

        private readonly Dictionary<string, TransitionState> _transitions = new();
        private double _lastFrameTime = -1.0;
        private float  _frameDt;

        /// <summary>True if any CSS transition is still animating (not yet converged to its target).</summary>
        public bool HasActiveTransitions
        {
            get
            {
                const float epsilon = 0.002f;
                foreach (var ts in _transitions.Values)
                {
                    if (!ts.Initialized) continue;
                    if (Math.Abs(ts.BgCurrent.R - ts.BgTarget.R) > epsilon ||
                        Math.Abs(ts.BgCurrent.G - ts.BgTarget.G) > epsilon ||
                        Math.Abs(ts.BgCurrent.B - ts.BgTarget.B) > epsilon ||
                        Math.Abs(ts.BgCurrent.A - ts.BgTarget.A) > epsilon ||
                        Math.Abs(ts.OpacityCurrent - ts.OpacityTarget) > epsilon)
                        return true;
                }
                return false;
            }
        }

        private static readonly Dictionary<string, Dictionary<string, float>> _transitionSpecCache = new();

        /// <summary>Parse a CSS transition spec into a (property → duration-seconds) map.
        /// Handles: "all 0.2s", "background 0.15s", "background 0.2s, opacity 0.3s".</summary>
        private static Dictionary<string, float> ParseTransitionDurations(string spec)
        {
            if (_transitionSpecCache.TryGetValue(spec, out var cached)) return cached;
            var d = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var tokens = part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;
                string raw = tokens[1];
                bool isMs = raw.EndsWith("ms", StringComparison.OrdinalIgnoreCase);
                string numStr = isMs ? raw[..^2] : (raw.EndsWith('s') ? raw[..^1] : raw);
                if (float.TryParse(numStr, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float dur))
                {
                    if (isMs) dur /= 1000f;
                    d[tokens[0]] = Math.Max(0.001f, dur);
                }
            }
            _transitionSpecCache[spec] = d;
            return d;
        }

        private static float GetTransitionDuration(Dictionary<string, float> spec, string prop)
        {
            if (spec.TryGetValue(prop, out float d)) return d;
            if (spec.TryGetValue("all", out float all)) return all;
            return 0f;
        }

        // ── Z-index deferred rendering ────────────────────────────────────────
        // Fibers with z-index > 0 are collected during the main pass and rendered
        // after in ascending z-index order so they draw on top.

        private record struct ZIndexedFiber(Fiber Fiber, float Opacity, string Path, float ScrollX, float ScrollY);
        // Reused across frames to avoid per-frame allocation; null sentinel means "we are in the z-index pass".
        private readonly List<ZIndexedFiber> _zIndexedList = new();
        private List<ZIndexedFiber>? _zIndexed;

        /// <summary>
        /// Portal fibers from <see cref="Paper.Core.Reconciler.Reconciler.PortalRoots"/>.
        /// Set by the host each frame. Portals are rendered last — on top of everything including z-indexed elements.
        /// </summary>
        public List<Paper.Core.Reconciler.Fiber>? PortalRoots { get; set; }

        /// <summary>
        /// Renders <paramref name="fiber"/> and its subtree at the cursor position as a translucent
        /// drag ghost. Call this after the main <see cref="Render"/> pass; flush batches afterwards.
        /// <paramref name="cursorX"/>/<paramref name="cursorY"/> are in layout (window) pixel space.
        /// </summary>
        public void RenderGhost(Fiber? fiber, float cursorX, float cursorY, float opacity = 0.5f)
        {
            if (fiber == null) return;
            var lb = fiber.Layout;
            _ghostOffsetX = cursorX - lb.AbsoluteX - lb.Width  / 2f;
            _ghostOffsetY = cursorY - lb.AbsoluteY - lb.Height / 2f;

            // Temporarily sever the sibling link so Render() doesn't continue
            // past this fiber into its siblings (which would ghost the whole list).
            var savedSibling = fiber.Sibling;
            fiber.Sibling = null;

            var saved = _zIndexed;
            _zIndexed = null;  // render ghost subtree immediately, no further deferral
            Render(fiber, opacity, "", 0, 0f, 0f);
            _zIndexed = saved;

            fiber.Sibling = savedSibling;  // restore
            _ghostOffsetX = 0f;
            _ghostOffsetY = 0f;
        }

        public void Render(Fiber? fiber, float inheritedOpacity = 1f)
        {
            double now = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
            _frameDt = _lastFrameTime < 0.0 ? 0f : (float)(now - _lastFrameTime);
            _lastFrameTime = now;

            _cullRect = (0, 0, _screenW, _screenH);

            RenderedScrollbars.Clear();
            _zIndexedList.Clear();
            _zIndexed = _zIndexedList;
            Render(fiber, inheritedOpacity, "", 0, 0f, 0f);
            if (_zIndexedList.Count > 0)
            {
                _zIndexedList.Sort(static (a, b) => (a.Fiber.ComputedStyle.ZIndex ?? 0).CompareTo(b.Fiber.ComputedStyle.ZIndex ?? 0));
                foreach (var z in _zIndexedList)
                    RenderFiber(z.Fiber, z.Opacity, z.Path, z.ScrollX, z.ScrollY);
            }
            _zIndexed = null;

            // Portals render last — always on top of the entire main tree.
            if (PortalRoots is { Count: > 0 })
            {
                for (int pi = 0; pi < PortalRoots.Count; pi++)
                    Render(PortalRoots[pi], inheritedOpacity, "portal", pi, 0f, 0f);
            }
        }

        private void Render(Fiber? fiber, float inheritedOpacity, string parentPath, int indexInParent, float scrollX, float scrollY)
        {
            if (fiber == null) return;

            var style = fiber.ComputedStyle;

            if ((style.Visibility ?? Visibility.Visible) == Visibility.Hidden ||
                (style.Display ?? Display.Block) == Display.None)
            {
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            string path = string.IsNullOrEmpty(parentPath) ? indexInParent.ToString() : parentPath + "." + indexInParent;

            // Defer z-indexed fibers: collect and render after normal tree traversal
            if (_zIndexed != null && (style.ZIndex ?? 0) > 0)
            {
                _zIndexed.Add(new ZIndexedFiber(fiber, inheritedOpacity, path, scrollX, scrollY));
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            // ── CSS Transitions: animate background + opacity ─────────────────
            float ownOpacity = style.Opacity ?? 1f;
            PaperColour? animBg = null;
            if (style.Transition != null)
            {
                var tSpec = ParseTransitionDurations(style.Transition);
                if (!_transitions.TryGetValue(path, out var ts))
                    _transitions[path] = ts = new TransitionState();

                var bgTarget = style.Background ?? default;
                if (!ts.Initialized)
                {
                    ts.BgCurrent      = bgTarget;
                    ts.BgTarget       = bgTarget;
                    ts.OpacityCurrent = ownOpacity;
                    ts.OpacityTarget  = ownOpacity;
                    ts.Initialized    = true;
                }
                else if (_frameDt > 0f)
                {
                    float bgDur = GetTransitionDuration(tSpec, "background");
                    if (bgDur > 0f)
                    {
                        ts.BgTarget = bgTarget;
                        float f = 1f - MathF.Exp(-_frameDt * (4f / bgDur));
                        ts.BgCurrent = ts.BgCurrent.Lerp(ts.BgTarget, f);
                        if (style.Background.HasValue || ts.BgCurrent.A > 0.004f)
                            animBg = ts.BgCurrent;
                    }
                    float opDur = GetTransitionDuration(tSpec, "opacity");
                    if (opDur > 0f)
                    {
                        ts.OpacityTarget = ownOpacity;
                        float f = 1f - MathF.Exp(-_frameDt * (4f / opDur));
                        ts.OpacityCurrent += (ts.OpacityTarget - ts.OpacityCurrent) * f;
                        ownOpacity = ts.OpacityCurrent;
                    }
                }
            }

            float opacity = inheritedOpacity * ownOpacity;
            var (rTL, rTR, rBR, rBL) = ResolveCornerRadii(style, ScaleX);
            var lb = fiber.Layout;
            // position:fixed elements live in viewport space — don't offset by accumulated scroll.
            var pos = style.Position ?? Position.Static;
            if (pos == Position.Fixed) { scrollX = 0f; scrollY = 0f; }
            float dx = (lb.AbsoluteX + _ghostOffsetX - scrollX) * ScaleX;
            float dy = (lb.AbsoluteY + _ghostOffsetY - scrollY) * ScaleY;
            float dw = lb.Width * ScaleX;
            float dh = lb.Height * ScaleY;

            // ── CSS transforms ────────────────────────────────────────────────
            if (style.TranslateX is { } tx) dx += tx * ScaleX;
            if (style.TranslateY is { } ty) dy += ty * ScaleY;
            if (style.ScaleX is { } sx && sx != 1f)
            {
                float cx = dx + dw * 0.5f;
                dw *= sx;
                dx = cx - dw * 0.5f;
            }
            if (style.ScaleY is { } sy && sy != 1f)
            {
                float cy = dy + dh * 0.5f;
                dh *= sy;
                dy = cy - dh * 0.5f;
            }
            float rotation = style.Rotate ?? 0f;

            // Empty Box: use Props.Style or ComputedStyle dimensions for draw size when layout gave 0 so colored boxes render.
            if (fiber.Type is string bt && bt == ElementTypes.Box && fiber.Child == null && (dw <= 0 || dh <= 0))
            {
                var ps = fiber.Props.Style;
                if (dw <= 0)
                {
                    if (ps?.Width != null && !ps.Width.Value.IsAuto)
                        dw = Math.Max(0, ps.Width.Value.Resolve(_screenW) * ScaleX);
                    else if (ps?.MinWidth != null && !ps.MinWidth.Value.IsAuto)
                        dw = Math.Max(0, ps.MinWidth.Value.Resolve(_screenW) * ScaleX);
                    else if (style.Width != null && !style.Width.Value.IsAuto)
                        dw = Math.Max(0, style.Width.Value.Resolve(_screenW) * ScaleX);
                    else if (style.MinWidth != null && !style.MinWidth.Value.IsAuto)
                        dw = Math.Max(0, style.MinWidth.Value.Resolve(_screenW) * ScaleX);
                }
                if (dh <= 0)
                {
                    if (ps?.Height != null && !ps.Height.Value.IsAuto)
                        dh = Math.Max(0, ps.Height.Value.Resolve(_screenH) * ScaleY);
                    else if (ps?.MinHeight != null && !ps.MinHeight.Value.IsAuto)
                        dh = Math.Max(0, ps.MinHeight.Value.Resolve(_screenH) * ScaleY);
                    else if (style.Height != null && !style.Height.Value.IsAuto)
                        dh = Math.Max(0, style.Height.Value.Resolve(_screenH) * ScaleY);
                    else if (style.MinHeight != null && !style.MinHeight.Value.IsAuto)
                        dh = Math.Max(0, style.MinHeight.Value.Resolve(_screenH) * ScaleY);
                }
            }

            // ── Viewport culling ──────────────────────────────────────────────
            // Skip draw calls (and optionally the whole subtree) for fibers that are
            // completely outside the current visible clip rect.
            {
                // position:fixed elements are always viewport-relative, not container-relative.
                // Check them against the full screen regardless of any narrowed _cullRect.
                bool inView = pos == Position.Fixed
                    ? dx + dw > 0 && dx < _screenW && dy + dh > 0 && dy < _screenH
                    : dx + dw > _cullRect.X && dx < _cullRect.X + _cullRect.W &&
                      dy + dh > _cullRect.Y && dy < _cullRect.Y + _cullRect.H;
                if (!inView)
                {
                    // Clip containers physically bound their children: if the container is off-screen,
                    // children cannot escape its bounds → safe to skip the entire subtree.
                    // Non-clip containers MUST still recurse: position:fixed/absolute children can
                    // escape and be visible (e.g. a component wrapper whose fixed child is in view).
                    var ovfXc = style.OverflowX ?? Overflow.Visible;
                    var ovfYc = style.OverflowY ?? Overflow.Visible;
                    bool isClipContainer =
                        ovfXc is Overflow.Scroll or Overflow.Auto or Overflow.Hidden ||
                        ovfYc is Overflow.Scroll or Overflow.Auto or Overflow.Hidden;
                    if (isClipContainer)
                    {
                        Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                        return;
                    }
                    else
                    {
                        // Non-clip: skip drawing but still recurse — fixed/absolute children may be in view
                        RenderFiberChildren(fiber, style, dx, dy, dw, dh, lb, opacity, path, scrollX, scrollY, rTL, rTR, rBR, rBL);
                        Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                        return;
                    }
                }
            }

            // ── Image element ─────────────────────────────────────────────────
            if (fiber.Type is string typeImg && typeImg == ElementTypes.Image)
            {
                _rects.Flush(_screenW, _screenH);
                var objFit = style.ObjectFit ?? ObjectFit.Fill;
                (uint texHandle, int iw, int ih) = GetImageResult != null
                    ? GetImageResult(fiber.Props.Src)
                    : (GetImageTexture != null ? GetImageTexture(fiber.Props.Src) : 0u, 0, 0);
                if (texHandle != 0)
                {
                    if (iw > 0 && ih > 0 && objFit != ObjectFit.Fill)
                        DrawImageWithFit(dx, dy, dw, dh, texHandle, iw, ih, objFit);
                    else
                        _viewports.Draw(dx, dy, dw, dh, texHandle, _screenW, _screenH);
                }
                else
                {
                    DrawRect(dx, dy, dw, dh, 0.35f, 0.35f, 0.4f, 1f * opacity, 0, 0, 0, 0, 0, 0);
                }
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            // ── Checkbox element ──────────────────────────────────────────────
            if (fiber.Type is string typeCb && typeCb == ElementTypes.Checkbox)
            {
                bool cbHover = path == HoveredPath;
                const float boxSize = 16f;
                float cw = boxSize * ScaleX;
                float ch = boxSize * ScaleY;
                // Centre the visual box within the element's layout height
                float cbBoxOffY = (dh - ch) / 2f;
                float cbBg = cbHover ? 0.22f : 0.15f;
                DrawRect(dx, dy + cbBoxOffY, cw, ch, cbBg, cbBg, cbBg + 0.05f, 1f * opacity, 0.5f, 0.5f, 0.6f, 1f * opacity, 1f * ScaleX, 2f * ScaleX);
                if (fiber.Props.Checked)
                {
                    float inset = 4f * ScaleX;
                    DrawRect(dx + inset, dy + cbBoxOffY + inset, cw - 2 * inset, ch - 2 * inset, 0.4f, 0.6f, 1f, 1f * opacity, 0, 0, 0, 0, 0, 0);
                }
                if (_fonts != null && fiber.Props.Text is { Length: > 0 } labelText)
                {
                    float cbFontPx  = SilkTextMeasurer.ResolveFontPx(style);
                    float cbLineH   = _fonts.LineHeight(cbFontPx);
                    var (cbPadTop, _, _, _) = BoxModel.PaddingPixels(style, lb.Width, lb.Height);
                    float cbCenterOffY = lb.Height / 2f - cbPadTop - cbLineH * 0.5f;
                    var labelBox = lb;
                    labelBox.AbsoluteX += 20;
                    labelBox.X += 20;
                    labelBox.Width = Math.Max(0, labelBox.Width - 20);
                    labelBox.AbsoluteY += cbCenterOffY;
                    labelBox.Y += cbCenterOffY;
                    var col = style.Color ?? new PaperColour(1f, 1f, 1f, 1f);
                    DrawText(labelText, labelBox, style, col, opacity, scrollX, scrollY);
                }
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            // ── Radio option element ──────────────────────────────────────────
            if (fiber.Type is string typeRo && typeRo == ElementTypes.RadioOption)
            {
                bool roHover = path == HoveredPath;
                const float circleSize = 14f;
                float cw = circleSize * ScaleX;
                float ch = circleSize * ScaleY;
                float circleRadius = (circleSize / 2f) * ScaleX;
                // Centre the visual circle within the element's layout height
                float roBoxOffY = (dh - ch) / 2f;
                float roBg = roHover ? 0.22f : 0.15f;
                DrawRect(dx, dy + roBoxOffY, cw, ch, roBg, roBg, roBg + 0.05f, 1f * opacity, 0.5f, 0.5f, 0.6f, 1f * opacity, 1f * ScaleX, circleRadius);
                if (fiber.Props.RadioChecked)
                {
                    float inset = 3f * ScaleX;
                    DrawRect(dx + inset, dy + roBoxOffY + inset, cw - 2 * inset, ch - 2 * inset, 0.4f, 0.6f, 1f, 1f * opacity, 0, 0, 0, 0, 0, circleRadius - inset);
                }
                if (_fonts != null && fiber.Props.Text is { Length: > 0 } roLabel)
                {
                    float roFontPx  = SilkTextMeasurer.ResolveFontPx(style);
                    float roLineH   = _fonts.LineHeight(roFontPx);
                    var (cbPadTop, _, _, _) = BoxModel.PaddingPixels(style, lb.Width, lb.Height);
                    float cbCenterOffY = lb.Height / 2f - cbPadTop - roLineH * 0.5f;
                    var labelBox = lb;
                    labelBox.AbsoluteX += 20;
                    labelBox.X += 20;
                    labelBox.Width = Math.Max(0, labelBox.Width - 20);
                    labelBox.AbsoluteY += cbCenterOffY;
                    labelBox.Y += cbCenterOffY;
                    var col = style.Color ?? new PaperColour(1f, 1f, 1f, 1f);
                    DrawText(roLabel, labelBox, style, col, opacity, scrollX, scrollY);
                }
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            // ── Viewport element ──────────────────────────────────────────────
            if (fiber.Type is string typeStr && typeStr == ElementTypes.Viewport)
            {
                _rects.Flush(_screenW, _screenH);

                uint texHandle = fiber.Props.TextureHandle;
                if (texHandle != 0)
                {
                    _viewports.Draw(dx, dy, dw, dh,
                                    texHandle, _screenW, _screenH);
                }
                else if (style.Background.HasValue && style.Background.Value.A > 0)
                {
                    var viewportFill = style.Background.Value;
                    DrawRect(dx, dy, dw, dh,
                        viewportFill.R, viewportFill.G, viewportFill.B, viewportFill.A * opacity,
                        0, 0, 0, 0, 0, (style.BorderRadius ?? 0f) * ScaleX);
                }

                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            // ── Box shadow ────────────────────────────────────────────────────
            if (style.BoxShadow is { Length: > 0 } shadows)
            {
                float br = (style.BorderRadius ?? 0f) * ScaleX;
                foreach (var shadow in shadows)
                {
                    if (shadow.Inset) continue;
                    float spread = shadow.SpreadRadius * ScaleX;
                    float blur   = shadow.BlurRadius   * ScaleX;
                    float shX = dx + shadow.OffsetX * ScaleX - spread;
                    float shY = dy + shadow.OffsetY * ScaleY - spread;
                    float sw = dw + spread * 2;
                    float sh = dh + spread * 2;
                    float shadowRadius = br + spread;
                    float baseAlpha = shadow.Colour.A * opacity;

                    if (blur <= 0)
                    {
                        DrawRect(shX, shY, sw, sh,
                            shadow.Colour.R, shadow.Colour.G, shadow.Colour.B, baseAlpha,
                            0, 0, 0, 0, 0, shadowRadius);
                    }
                    else
                    {
                        // Approximate blur: draw several expanding rects with decreasing alpha
                        int passes = Math.Max(1, Math.Min(6, (int)(blur / 6) + 2));
                        for (int p = 0; p < passes; p++)
                        {
                            float t      = (p + 0.5f) / passes;
                            float expand = blur * t;
                            float passAlpha = baseAlpha * (1f - t * 0.6f) / passes;
                            DrawRect(shX - expand * 0.5f, shY - expand * 0.5f,
                                sw + expand, sh + expand,
                                shadow.Colour.R, shadow.Colour.G, shadow.Colour.B, passAlpha,
                                0, 0, 0, 0, 0, shadowRadius + expand * 0.5f);
                        }
                        // Core shadow at half alpha for depth
                        DrawRect(shX, shY, sw, sh,
                            shadow.Colour.R, shadow.Colour.G, shadow.Colour.B, baseAlpha * 0.5f,
                            0, 0, 0, 0, 0, shadowRadius);
                    }
                }
            }

            // ── Background image (Box) ────────────────────────────────────────
            if (style.BackgroundImage is { } bgImgPath && GetImageResult != null)
            {
                var (bgHandle, bgW, bgH) = GetImageResult(bgImgPath);
                if (bgHandle != 0 && bgW > 0 && bgH > 0)
                {
                    _rects.Flush(_screenW, _screenH);
                    _fonts?.Flush(_screenW, _screenH);
                    DrawImageWithFit(dx, dy, dw, dh, bgHandle, bgW, bgH, style.BackgroundSize ?? ObjectFit.Cover);
                }
            }

            // ── Background ────────────────────────────────────────────────────
            // Use animated background when a transition is active, otherwise the computed style value.
            var background = animBg.HasValue ? animBg : style.Background;
            if (background.HasValue && background.Value.A > 0)
            {
                var colour = background.Value;
                DrawRectCorners(dx, dy, dw, dh,
                    colour.R, colour.G, colour.B, colour.A * opacity,
                    0, 0, 0, 0, 0, rTL, rTR, rBR, rBL, rotation);
            }

            // ── Border ────────────────────────────────────────────────────────
            if (style.Border != null)
            {
                var edge = style.Border.Top;
                if (edge.Style != BorderStyle.None && edge.Width > 0)
                {
                    if (background.HasValue)
                        DrawRectCorners(dx, dy, dw, dh,
                            background.Value.R, background.Value.G, background.Value.B, background.Value.A * opacity,
                            edge.Colour.R, edge.Colour.G, edge.Colour.B, edge.Colour.A * opacity,
                            edge.Width * ScaleX, rTL, rTR, rBR, rBL, rotation);
                    else
                        DrawRectCorners(dx, dy, dw, dh,
                            0, 0, 0, 0,
                            edge.Colour.R, edge.Colour.G, edge.Colour.B, edge.Colour.A * opacity,
                            edge.Width * ScaleX, rTL, rTR, rBR, rBL, rotation);
                }
            }
            // ── Individual border sides ────────────────────────────────────────
            if (style.BorderTop is { } btop && btop.Style != BorderStyle.None && btop.Width > 0)
            {
                float bw = btop.Width * ScaleY;
                DrawRect(dx, dy, dw, bw, btop.Colour.R, btop.Colour.G, btop.Colour.B, btop.Colour.A * opacity, 0, 0, 0, 0, 0, 0);
            }
            if (style.BorderBottom is { } bbot && bbot.Style != BorderStyle.None && bbot.Width > 0)
            {
                float bw = bbot.Width * ScaleY;
                DrawRect(dx, dy + dh - bw, dw, bw, bbot.Colour.R, bbot.Colour.G, bbot.Colour.B, bbot.Colour.A * opacity, 0, 0, 0, 0, 0, 0);
            }
            if (style.BorderLeft is { } bleft && bleft.Style != BorderStyle.None && bleft.Width > 0)
            {
                float bw = bleft.Width * ScaleX;
                DrawRect(dx, dy, bw, dh, bleft.Colour.R, bleft.Colour.G, bleft.Colour.B, bleft.Colour.A * opacity, 0, 0, 0, 0, 0, 0);
            }
            if (style.BorderRight is { } bright && bright.Style != BorderStyle.None && bright.Width > 0)
            {
                float bw = bright.Width * ScaleX;
                DrawRect(dx + dw - bw, dy, bw, dh, bright.Colour.R, bright.Colour.G, bright.Colour.B, bright.Colour.A * opacity, 0, 0, 0, 0, 0, 0);
            }

            // ── Text label (text + button elements have Props.Text) ───────────
            if (_text != null && fiber.Props?.Text is { } labelNotNull)
            {
                bool isFocusedInput = path == FocusedInputPath && !string.IsNullOrEmpty(FocusedInputPath) &&
                    (fiber.Type is string tIn && (tIn == ElementTypes.Input || tIn == ElementTypes.Textarea || tIn == ElementTypes.MarkdownEditor));
                string label = (path == FocusedInputPath && FocusedInputText != null) ? FocusedInputText : labelNotNull;
                // Mask password input
                if (isFocusedInput && FocusedInputType == "password")
                    label = new string('●', label.Length);
                var col = style.Color ?? new PaperColour(1f, 1f, 1f, 1f);

                if (fiber.Type is string tta && tta == ElementTypes.Textarea)
                {
                    if (_gl != null)
                    {
                        _rects.Flush(_screenW, _screenH);
                        _fonts?.Flush(_screenW, _screenH);
                        _gl.Enable(EnableCap.ScissorTest);
                        _gl.Scissor((int)dx, (int)(_screenH - (dy + dh)), (uint)Math.Max(0, (int)dw), (uint)Math.Max(0, (int)dh));
                    }
                    float fontPx   = SilkTextMeasurer.ResolveFontPx(style);
                    float atlasLineH = _fonts!.LineHeight(fontPx);
                    float textH    = atlasLineH * Math.Max(0.5f, style.LineHeight ?? 1.4f);
                    var logicalLines = label.Split('\n');
                    var (padTop, padRight, _, padLeft) = BoxModel.PaddingPixels(style, lb.Width, lb.Height);
                    float contentWidth = lb.Width - padLeft - padRight;
                    int idx = 0;
                    int row = 0;
                    for (int li = 0; li < logicalLines.Length; li++)
                    {
                        var logLine = logicalLines[li];
                        var wrappedSegments = WrapTextLine(logLine, idx, contentWidth, fontPx);
                        foreach (var seg in wrappedSegments)
                        {
                            int lineStart = seg.Start;
                            int lineEnd = seg.End;
                            var lineBox = lb;
                            lineBox.AbsoluteY = lb.AbsoluteY + padTop + row * textH;
                            lineBox.Y = lb.Y + padTop + row * textH;
                            lineBox.Height = textH;
                            if (isFocusedInput)
                                DrawSelectionForLine(seg.Text, lineStart, lineEnd, lb, lineBox, style, scrollX, scrollY);
                            if (seg.Text.Length > 0)
                                DrawText(seg.Text, lineBox, style, col, opacity, scrollX, scrollY);
                            if (isFocusedInput)
                                DrawCaretForLine(seg.Text, lineStart, lineEnd, lb, lineBox, style, col, opacity, scrollX, scrollY);
                            row++;
                        }
                        idx += logLine.Length + 1; // +1 for the newline
                    }
                    if (_gl != null)
                    {
                        _rects.Flush(_screenW, _screenH);
                        _fonts?.Flush(_screenW, _screenH);
                        _gl.Disable(EnableCap.ScissorTest);
                    }
                }
                else if (fiber.Type is string mde && mde == ElementTypes.MarkdownEditor)
                {
                    if (_gl != null)
                    {
                        _rects.Flush(_screenW, _screenH);
                        _fonts?.Flush(_screenW, _screenH);
                        _gl.Enable(EnableCap.ScissorTest);
                        _gl.Scissor((int)dx, (int)(_screenH - (dy + dh)), (uint)Math.Max(0, (int)dw), (uint)Math.Max(0, (int)dh));
                    }
                    var mdTheme      = MarkdownTheme.Dark;
                    float fontPx     = SilkTextMeasurer.ResolveFontPx(style);
                    float atlasLineH = _fonts!.LineHeight(fontPx);
                    float textH      = atlasLineH * Math.Max(0.5f, style.LineHeight ?? 1.4f);
                    var (padTop, padRight, _, padLeft) = BoxModel.PaddingPixels(style, lb.Width, lb.Height);
                    float contentWidth = lb.Width - padLeft - padRight;
                    string? fam   = style.FontFamily;
                    var wt        = style.FontWeight;
                    var fs        = style.FontStyle;

                    // Rebuild token list and wrapped rows only when text or layout width changes.
                    int textHash = label.GetHashCode();
                    int widthInt = (int)contentWidth;
                    int fontPxInt = (int)(fontPx * 10);
                    if (!_mdCache.TryGetValue(path, out var mdCached) ||
                        mdCached.TextHash != textHash || mdCached.TextLen != label.Length ||
                        mdCached.WidthInt != widthInt || mdCached.FontPxInt != fontPxInt)
                    {
                        var tokens = MarkdownTokenizer.Tokenize(label);
                        var logicalLines = label.Split('\n');
                        var rows = new System.Collections.Generic.List<(string, int, int)>();
                        int idx = 0;
                        for (int li = 0; li < logicalLines.Length; li++)
                        {
                            var logLine = logicalLines[li];
                            foreach (var seg in WrapTextLine(logLine, idx, contentWidth, fontPx))
                                rows.Add(seg);
                            idx += logicalLines[li].Length + 1;
                        }
                        // Pre-compute per-row render segments with x-offsets so the render loop
                        // needs zero MeasureWidth calls on cache hits.
                        var rowArr = rows.ToArray();
                        var rowSegs = new MdSegment[rowArr.Length][];
                        for (int ri = 0; ri < rowArr.Length; ri++)
                        {
                            var (lineText, lineStart, lineEnd) = rowArr[ri];
                            rowSegs[ri] = BuildRowSegments(lineText, lineStart, lineEnd, tokens, mdTheme, col, fontPx, fam, wt, fs);
                        }
                        mdCached = new MdCache
                        {
                            TextHash = textHash, TextLen = label.Length,
                            WidthInt = widthInt, FontPxInt = fontPxInt,
                            Rows = rowArr, RowSegments = rowSegs,
                        };
                        _mdCache[path] = mdCached;
                    }

                    var (mdBatch, mdScale) = _fonts!.Get(fontPx * DpiScale, fam, wt, fs);
                    var (padTop2, _, padBottom2, _) = BoxModel.PaddingPixels(style, lb.Width, lb.Height);
                    float contentH2 = textH - padTop2 - padBottom2;
                    float baselineBase = contentH2 >= atlasLineH * 1.4f
                        ? padTop2 + (contentH2 - atlasLineH) / 2f + atlasLineH * 0.8f
                        : padTop2 + atlasLineH * 0.8f;
                    float xOrigin = lb.AbsoluteX + padLeft;

                    for (int row = 0; row < mdCached.Rows.Length; row++)
                    {
                        var seg = mdCached.Rows[row];
                        int lineStart = seg.Start;
                        int lineEnd   = seg.End;
                        var lineBox   = lb;
                        lineBox.AbsoluteY = lb.AbsoluteY + padTop + row * textH;
                        lineBox.Y         = lb.Y + padTop + row * textH;
                        lineBox.Height    = textH;
                        if (isFocusedInput)
                            DrawSelectionForLine(seg.Text, lineStart, lineEnd, lb, lineBox, style, scrollX, scrollY);
                        // Render pre-computed segments — no MeasureWidth in hot path.
                        float rowBaseline = lineBox.AbsoluteY + baselineBase;
                        float ry = (rowBaseline - scrollY) * ScaleY;
                        foreach (var ms in mdCached.RowSegments[row])
                            mdBatch.Add(ms.Text.AsSpan(), (xOrigin + ms.XOffset - scrollX) * ScaleX, ry,
                                ms.R, ms.G, ms.B, ms.A * opacity, mdScale);
                        if (isFocusedInput)
                            DrawCaretForLine(seg.Text, lineStart, lineEnd, lb, lineBox, style, col, opacity, scrollX, scrollY);
                    }
                    if (_gl != null)
                    {
                        _rects.Flush(_screenW, _screenH);
                        _fonts?.Flush(_screenW, _screenH);
                        _gl.Disable(EnableCap.ScissorTest);
                    }
                }
                else
                {
                    var singleLine = label ?? "";
                    float inputScrollX = (isFocusedInput && fiber.Type is string inp && inp == ElementTypes.Input) ? FocusedInputScrollX : 0f;
                    if (_gl != null)
                    {
                        _rects.Flush(_screenW, _screenH);
                        _fonts?.Flush(_screenW, _screenH);
                        int scX = (int)dx;
                        int scY = (int)(_screenH - (dy + dh));
                        int scW = Math.Max(0, (int)dw);
                        int scH = Math.Max(0, (int)dh);
                        _gl.Enable(EnableCap.ScissorTest);
                        _gl.Scissor(scX, scY, (uint)scW, (uint)scH);
                    }
                    if (isFocusedInput)
                        DrawSelectionForLine(singleLine, 0, singleLine.Length, lb, lb, style, scrollX, scrollY, inputScrollX);
                    if (singleLine.Length > 0)
                        DrawText(singleLine, lb, style, col, opacity, scrollX, scrollY, inputScrollX);
                    else if (!isFocusedInput && fiber.Props.Placeholder is string placeholder && placeholder.Length > 0)
                    {
                        // Render placeholder when empty and not focused
                        var placeholderCol = new PaperColour(0.6f, 0.6f, 0.6f, col.A * opacity);
                        DrawText(placeholder, lb, style, placeholderCol, opacity, scrollX, scrollY, inputScrollX);
                    }
                    if (isFocusedInput)
                        DrawCaretForLine(singleLine, 0, singleLine.Length, lb, lb, style, col, opacity, scrollX, scrollY, inputScrollX);
                    if (_gl != null)
                    {
                        _rects.Flush(_screenW, _screenH);
                        _fonts?.Flush(_screenW, _screenH);
                        _gl.Disable(EnableCap.ScissorTest);
                    }
                }
            }

            RenderFiberChildren(fiber, style, dx, dy, dw, dh, lb, opacity, path, scrollX, scrollY, rTL, rTR, rBR, rBL);
            Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
        }

        private void RenderFiberChildren(
            Fiber fiber, StyleSheet style,
            float dx, float dy, float dw, float dh, LayoutBox lb,
            float opacity, string path, float scrollX, float scrollY,
            float rTL, float rTR, float rBR, float rBL)
        {
            var ovfX = style.OverflowX ?? Overflow.Visible;
            var ovfY = style.OverflowY ?? Overflow.Visible;
            float radius = Math.Max(Math.Max(rTL, rTR), Math.Max(rBR, rBL));
            // Scissor clip for scroll/auto containers (rect clip is sufficient and cheap).
            bool scrollClip = _gl != null && (ovfY == Overflow.Scroll || ovfY == Overflow.Auto ||
                                              ovfX == Overflow.Scroll || ovfX == Overflow.Auto);
            bool hiddenClip = _gl != null && !scrollClip && fiber.Child != null && radius == 0f &&
                               (ovfX == Overflow.Hidden || ovfY == Overflow.Hidden);
            // Stencil clip for overflow:hidden with border-radius (rounded corner clip).
            // Skip when there are no children — nothing to clip, so the stencil ops would be wasted.
            bool roundedClip = _gl != null && !scrollClip && !hiddenClip && fiber.Child != null &&
                                (ovfX == Overflow.Hidden || ovfY == Overflow.Hidden) && radius > 0f;

            float childScrollX = scrollX;
            float childScrollY = scrollY;
            if ((scrollClip || roundedClip) && GetScrollOffset != null)
            {
                var (ox, oy) = GetScrollOffset(path);
                childScrollX += ox;
                childScrollY += oy;
            }

            if (scrollClip)
            {
                _rects.Flush(_screenW, _screenH);
                _fonts?.Flush(_screenW, _screenH);
                int x = (int)dx;
                int y = (int)(_screenH - (dy + dh)); // OpenGL scissor: origin bottom-left
                int w = Math.Max(0, (int)dw);
                int h = Math.Max(0, (int)dh);
                _gl!.Enable(EnableCap.ScissorTest);
                _gl.Scissor(x, y, (uint)w, (uint)h);
                // Narrow the cull rect to this container's visible screen area so children
                // outside it skip their draw calls (and entire subtrees) without GPU overhead.
                var prevCullRect = _cullRect;
                float cx0 = Math.Max(_cullRect.X, dx);
                float cy0 = Math.Max(_cullRect.Y, dy);
                float cx1 = Math.Min(_cullRect.X + _cullRect.W, dx + dw);
                float cy1 = Math.Min(_cullRect.Y + _cullRect.H, dy + dh);
                _cullRect = (cx0, cy0, Math.Max(0, cx1 - cx0), Math.Max(0, cy1 - cy0));
                RenderChildren(fiber.Child, opacity, path, childScrollX, childScrollY);
                _cullRect = prevCullRect;
                _rects.Flush(_screenW, _screenH);
                _fonts?.Flush(_screenW, _screenH);
                _gl.Disable(EnableCap.ScissorTest);
                // Draw scrollbar outside scissor so thumb rounded corners are never clipped
                float rawScrollY = GetScrollOffset != null ? GetScrollOffset(path).scrollY : 0f;
                float rawScrollX = GetScrollOffset != null ? GetScrollOffset(path).scrollX : 0f;
                // Include container's trailing padding so max scroll reveals the full padded area
                var (_, padRightPx, padBottomPx, _) = BoxModel.PaddingPixels(style, lb.Width, lb.Height);
                float contentH = (ComputeChildrenContentHeight(fiber.Child) + padBottomPx) * ScaleY;
                float contentW = (ComputeChildrenContentWidth(fiber.Child) + padRightPx) * ScaleX;
                float sbOpacity = GetScrollbarOpacity != null ? GetScrollbarOpacity(path) : 0f;
                if (sbOpacity > 0f)
                {
                    _rects.Flush(_screenW, _screenH);
                    DrawScrollbar(path, dx, dy, dw, dh, rawScrollY * ScaleY, contentH, rawScrollX * ScaleX, contentW, sbOpacity * opacity);
                }
                else
                {
                    // Still record geometry (with no visible scrollbar) so clamping works
                    RecordScrollbarGeometry(path, dx, dy, dw, dh, rawScrollY * ScaleY, contentH, rawScrollX * ScaleX, contentW);
                }
            }
            else if (hiddenClip)
            {
                _rects.Flush(_screenW, _screenH);
                _fonts?.Flush(_screenW, _screenH);
                int hx = (int)dx;
                int hy = (int)(_screenH - (dy + dh));
                int hw = Math.Max(0, (int)dw);
                int hh = Math.Max(0, (int)dh);
                _gl!.Enable(EnableCap.ScissorTest);
                _gl.Scissor(hx, hy, (uint)hw, (uint)hh);
                // Do NOT narrow _cullRect here: overflow:hidden may use PaddingTop-based virtual
                // scrolling where children's AbsoluteY can exceed the container bounds in layout space.
                // The GL scissor already handles all clipping correctly.
                RenderChildren(fiber.Child, opacity, path, childScrollX, childScrollY);
                _rects.Flush(_screenW, _screenH);
                _fonts?.Flush(_screenW, _screenH);
                _gl.Disable(EnableCap.ScissorTest);
            }
            else if (roundedClip)
            {
                PushRoundedClip(dx, dy, dw, dh, radius);
                var prevCullRectR = _cullRect;
                float rx0 = Math.Max(_cullRect.X, dx); float ry0 = Math.Max(_cullRect.Y, dy);
                float rx1 = Math.Min(_cullRect.X + _cullRect.W, dx + dw); float ry1 = Math.Min(_cullRect.Y + _cullRect.H, dy + dh);
                _cullRect = (rx0, ry0, Math.Max(0, rx1 - rx0), Math.Max(0, ry1 - ry0));
                RenderChildren(fiber.Child, opacity, path, childScrollX, childScrollY);
                _cullRect = prevCullRectR;
                PopRoundedClip(dx, dy, dw, dh, radius);
            }
            else
            {
                RenderChildren(fiber.Child, opacity, path, childScrollX, childScrollY);
            }
        }

        private void RenderChildren(Fiber? child, float opacity, string parentPath, float scrollX, float scrollY)
        {
            Render(child, opacity, parentPath, 0, scrollX, scrollY);
        }

        /// <summary>Render a single fiber (and its subtree) used for z-indexed deferred pass.</summary>
        private void RenderFiber(Fiber fiber, float opacity, string path, float scrollX, float scrollY)
        {
            // Temporarily clear _zIndexed so z-indexed children within this subtree
            // are rendered immediately (within their stacking context) rather than deferred again.
            var saved = _zIndexed;
            _zIndexed = null;
            int lastDot = path.LastIndexOf('.');
            string parentPath = lastDot >= 0 ? path[..lastDot] : "";
            int index = int.TryParse(lastDot >= 0 ? path[(lastDot + 1)..] : path, out int idx) ? idx : 0;
            Render(fiber, opacity, parentPath, index, scrollX, scrollY);
            _zIndexed = saved;
        }

        // ── Stencil rounded clipping ──────────────────────────────────────────

        private void PushRoundedClip(float dx, float dy, float dw, float dh, float radius)
        {
            if (_gl == null) return;
            _rects.Flush(_screenW, _screenH);
            _fonts?.Flush(_screenW, _screenH);
            _stencilDepth++;
            _gl.Enable(EnableCap.StencilTest);
            _gl.StencilMask(0xFF);
            _gl.StencilFunc(StencilFunction.Always, 0, 0xFF);
            _gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Incr);
            _gl.ColorMask(false, false, false, false);
            // Draw rounded rect shape into stencil only (colour writes off)
            _rects.Add(dx, dy, dw, dh, 1, 1, 1, 1, 0, 0, 0, 0, 0, radius, radius, radius, radius);
            _rects.Flush(_screenW, _screenH);
            _gl.ColorMask(true, true, true, true);
            // Only pass where stencil == _stencilDepth (inside all nested clips)
            _gl.StencilFunc(StencilFunction.Equal, _stencilDepth, 0xFF);
            _gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
        }

        private void PopRoundedClip(float dx, float dy, float dw, float dh, float radius)
        {
            if (_gl == null) return;
            _rects.Flush(_screenW, _screenH);
            _fonts?.Flush(_screenW, _screenH);
            // Decrement stencil values inside the shape
            _gl.StencilMask(0xFF);
            _gl.StencilFunc(StencilFunction.Always, 0, 0xFF);
            _gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Decr);
            _gl.ColorMask(false, false, false, false);
            _rects.Add(dx, dy, dw, dh, 1, 1, 1, 1, 0, 0, 0, 0, 0, radius, radius, radius, radius);
            _rects.Flush(_screenW, _screenH);
            _gl.ColorMask(true, true, true, true);
            _stencilDepth--;
            if (_stencilDepth <= 0)
            {
                _stencilDepth = 0;
                _gl.Disable(EnableCap.StencilTest);
            }
            else
            {
                _gl.StencilFunc(StencilFunction.Equal, _stencilDepth, 0xFF);
                _gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void DrawImageWithFit(float boxX, float boxY, float boxW, float boxH,
            uint textureHandle, int imgW, int imgH, ObjectFit fit)
        {
            if (textureHandle == 0 || imgW <= 0 || imgH <= 0) return;
            float iw = imgW;
            float ih = imgH;
            float scale;
            float drawX, drawY, drawW, drawH;
            float u0, v0, u1, v1;
            switch (fit)
            {
                case ObjectFit.Contain:
                    scale = Math.Min(boxW / iw, boxH / ih);
                    drawW = iw * scale;
                    drawH = ih * scale;
                    drawX = boxX + (boxW - drawW) / 2f;
                    drawY = boxY + (boxH - drawH) / 2f;
                    u0 = 0f; v0 = 0f; u1 = 1f; v1 = 1f;
                    break;
                case ObjectFit.Cover:
                    scale = Math.Max(boxW / iw, boxH / ih);
                    float visibleW = boxW / scale;
                    float visibleH = boxH / scale;
                    u0 = (float)(0.5 - visibleW / (2 * iw));
                    v0 = (float)(0.5 - visibleH / (2 * ih));
                    u1 = (float)(0.5 + visibleW / (2 * iw));
                    v1 = (float)(0.5 + visibleH / (2 * ih));
                    drawX = boxX; drawY = boxY; drawW = boxW; drawH = boxH;
                    break;
                default: // Fill
                    drawX = boxX; drawY = boxY; drawW = boxW; drawH = boxH;
                    u0 = 0f; v0 = 0f; u1 = 1f; v1 = 1f;
                    break;
            }
            _viewports.DrawWithUV(drawX, drawY, drawW, drawH, u0, v0, u1, v1, textureHandle, _screenW, _screenH);
        }

        private static (float tl, float tr, float br, float bl) ResolveCornerRadii(StyleSheet style, float scale)
        {
            float tl = (style.BorderTopLeftRadius     ?? style.BorderRadius ?? 0f) * scale;
            float tr = (style.BorderTopRightRadius    ?? style.BorderRadius ?? 0f) * scale;
            float br = (style.BorderBottomRightRadius ?? style.BorderRadius ?? 0f) * scale;
            float bl = (style.BorderBottomLeftRadius  ?? style.BorderRadius ?? 0f) * scale;
            return (tl, tr, br, bl);
        }

        private void DrawRect(
            float x, float y, float w, float h,
            float r, float g, float b, float a,
            float br, float bg, float bb, float ba,
            float borderWidth, float radius, float rotation = 0f)
        {
            _rects.Add(x, y, w, h, r, g, b, a, br, bg, bb, ba, borderWidth, radius, radius, radius, radius, rotation);
        }

        private void DrawRectCorners(
            float x, float y, float w, float h,
            float r, float g, float b, float a,
            float br, float bg, float bb, float ba,
            float borderWidth,
            float radiusTL, float radiusTR, float radiusBR, float radiusBL,
            float rotation = 0f)
        {
            _rects.Add(x, y, w, h, r, g, b, a, br, bg, bb, ba, borderWidth, radiusTL, radiusTR, radiusBR, radiusBL, rotation);
        }

        /// <summary>macOS overlay-style scrollbar: semi-transparent thumb only, no track background.</summary>
        private void DrawScrollbar(string path, float dx, float dy, float dw, float dh,
                                   float scrollY, float contentH, float scrollX, float contentW, float opacity)
        {
            RecordScrollbarGeometry(path, dx, dy, dw, dh, scrollY, contentH, scrollX, contentW);
            // Scale logical sizes (6px wide, 3px margin) to physical framebuffer pixels
            float thumbW = 6f * DpiScale;
            float margin  = 3f * DpiScale;
            // Vertical thumb — top margin only so thumb can reach the very bottom
            if (contentH > dh)
            {
                float trackX = dx + dw - thumbW - margin;
                float trackY = dy + margin;
                float trackH = dh - margin;   // only top margin; bottom is flush
                if (trackH > 0f)
                {
                    float maxScroll = contentH - dh;
                    float thumbRatio = Math.Clamp(dh / contentH, 0.05f, 1f);
                    float thumbH = Math.Max(thumbW * 2f, trackH * thumbRatio);
                    float scrollRatio = maxScroll > 0f ? Math.Clamp(scrollY / maxScroll, 0f, 1f) : 0f;
                    float thumbY = trackY + scrollRatio * (trackH - thumbH);
                    DrawRect(trackX, thumbY, thumbW, thumbH, 0.9f, 0.9f, 0.9f, 0.55f * opacity, 0, 0, 0, 0, 0, thumbW * 0.5f);
                }
            }
            // Horizontal thumb — left margin only so thumb can reach the very right edge
            if (contentW > dw)
            {
                float trackY2 = dy + dh - thumbW - margin;
                float trackX2 = dx + margin;
                float trackW = dw - margin;   // only left margin; right is flush
                if (trackW > 0f)
                {
                    float maxScroll = contentW - dw;
                    float thumbRatio = Math.Clamp(dw / contentW, 0.05f, 1f);
                    float thumbW2 = Math.Max(thumbW * 2f, trackW * thumbRatio);
                    float scrollRatio = maxScroll > 0f ? Math.Clamp(scrollX / maxScroll, 0f, 1f) : 0f;
                    float thumbX = trackX2 + scrollRatio * (trackW - thumbW2);
                    DrawRect(thumbX, trackY2, thumbW2, thumbW, 0.9f, 0.9f, 0.9f, 0.55f * opacity, 0, 0, 0, 0, 0, thumbW * 0.5f);
                }
            }
        }

        private void RecordScrollbarGeometry(string path, float dx, float dy, float dw, float dh,
                                             float scrollY, float contentH, float scrollX, float contentW)
        {
            float thumbW = 6f * DpiScale;
            float margin  = 3f * DpiScale;
            float trackX = dx + dw - thumbW - margin;
            float trackY = dy + margin;
            float trackH = dh - margin;   // only top margin; bottom is flush
            float maxScroll = Math.Max(0f, contentH - dh);
            float maxScrollX = Math.Max(0f, contentW - dw);
            float thumbRatio = contentH > dh ? Math.Clamp(dh / contentH, 0.05f, 1f) : 1f;
            float thumbH = Math.Max(thumbW * 2f, trackH * thumbRatio);
            float scrollRatio = maxScroll > 0f ? Math.Clamp(scrollY / maxScroll, 0f, 1f) : 0f;
            float thumbY = trackY + scrollRatio * (trackH - thumbH);
            // Store geometry in logical pixels so PaperSurface hit-testing and scroll clamping
            // (which operate in logical/CSS pixel space) remain correct on HiDPI displays.
            float dpi = DpiScale > 0f ? DpiScale : 1f;
            RenderedScrollbars[path] = new ScrollbarHit(trackX / dpi, trackY / dpi, trackH / dpi,
                                                        thumbY / dpi, thumbH / dpi,
                                                        maxScroll / dpi, maxScrollX / dpi);
        }

        /// <summary>Word-wrap a single line of text to fit within maxWidth (layout pixels at fontPx). Returns sub-lines with char offsets.</summary>
        private List<(string Text, int Start, int End)> WrapTextLine(string line, int offset, float maxWidth, float fontPx = 16f)
        {
            var result = new List<(string, int, int)>();
            if (_fonts == null || maxWidth <= 0 || line.Length == 0)
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
                    float cw = _fonts.MeasureWidth(line.AsSpan(end, 1), fontPx);
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

        /// <summary>Compute total content height spanned by direct children of a fiber (in layout-space Y).</summary>
        private static float ComputeChildrenContentHeight(Fiber? child)
        {
            float maxBottom = 0f;
            var c = child;
            while (c != null)
            {
                float bottom = c.Layout.Y + c.Layout.Height;
                if (bottom > maxBottom) maxBottom = bottom;
                c = c.Sibling;
            }
            return maxBottom;
        }

        /// <summary>Compute total content width spanned by direct children of a fiber (in layout-space X).</summary>
        private static float ComputeChildrenContentWidth(Fiber? child)
        {
            float maxRight = 0f;
            var c = child;
            while (c != null)
            {
                float right = c.Layout.X + c.Layout.Width;
                if (right > maxRight) maxRight = right;
                c = c.Sibling;
            }
            return maxRight;
        }

        /// <summary>Draw selection highlight for one line (call before DrawText so it appears behind).</summary>
        private void DrawSelectionForLine(string line, int lineStart, int lineEnd, LayoutBox fullLb, LayoutBox lineBox, StyleSheet style, float scrollX, float scrollY, float inputScrollX = 0f)
        {
            if (_fonts == null) return;
            int selMin = Math.Min(FocusedInputSelStart, FocusedInputSelEnd);
            int selMax = Math.Max(FocusedInputSelStart, FocusedInputSelEnd);
            int lineSelStart = Math.Max(selMin, lineStart) - lineStart;
            int lineSelEnd   = Math.Min(selMax, lineEnd) - lineStart;
            if (lineSelStart >= lineSelEnd || lineSelStart < 0 || lineSelEnd > line.Length) return;
            float fontPx   = SilkTextMeasurer.ResolveFontPx(style);
            string? fam    = style.FontFamily;
            var weight     = style.FontWeight;
            float textH    = _fonts.LineHeight(fontPx, fam, weight);
            var (padTop, _, _, padLeft) = BoxModel.PaddingPixels(style, fullLb.Width, fullLb.Height);
            float xLayout  = fullLb.AbsoluteX + padLeft - inputScrollX;
            float baseline = lineBox.AbsoluteY + padTop + (textH * 0.8f);
            float w0 = _fonts.MeasureWidth(line.AsSpan(0, lineSelStart), fontPx, fam, weight);
            float w1 = _fonts.MeasureWidth(line.AsSpan(0, lineSelEnd), fontPx, fam, weight);
            float x0 = (xLayout - scrollX + w0) * ScaleX;
            float y0 = (baseline - scrollY - textH * 0.8f) * ScaleY;
            float w  = (w1 - w0) * ScaleX;
            float h  = textH * ScaleY;
            if (w > 0 && h > 0)
                DrawRect(x0, y0, w, h, 0.3f, 0.5f, 0.9f, 0.4f, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>Draw caret for one line (call after DrawText so it appears on top).</summary>
        private void DrawCaretForLine(string line, int lineStart, int lineEnd, LayoutBox fullLb, LayoutBox lineBox, StyleSheet style, PaperColour col, float opacity, float scrollX, float scrollY, float inputScrollX = 0f)
        {
            if (!FocusedInputCaretVisible || _fonts == null || FocusedInputCaret < lineStart || FocusedInputCaret > lineEnd + 1) return;
            float fontPx   = SilkTextMeasurer.ResolveFontPx(style);
            string? fam    = style.FontFamily;
            var weight     = style.FontWeight;
            float textH    = _fonts.LineHeight(fontPx, fam, weight);
            var (padTop, _, _, padLeft) = BoxModel.PaddingPixels(style, fullLb.Width, fullLb.Height);
            float xLayout  = fullLb.AbsoluteX + padLeft - inputScrollX;
            float baseline = lineBox.AbsoluteY + padTop + (textH * 0.8f);
            int   caretOffset = Math.Min(FocusedInputCaret - lineStart, line.Length);
            float caretX   = xLayout + (caretOffset <= 0 ? 0 : _fonts.MeasureWidth(line.AsSpan(0, caretOffset), fontPx, fam, weight));
            DrawCaretAt(caretX, baseline, 1f, textH, col, opacity, scrollX, scrollY);
        }

        private void DrawCaretAt(float xLayout, float baseline, float scale, float textH, PaperColour col, float opacity, float scrollX, float scrollY)
        {
            float x = (xLayout - scrollX) * ScaleX;
            float y = (baseline - scrollY - textH * 0.8f) * ScaleY;
            float h = Math.Max(14f, textH * ScaleY); // ensure visible height
            float caretW = Math.Max(2f, 2f * ScaleX);
            // Use text colour so caret is visible on both light and dark backgrounds
            DrawRect(x, y, caretW, h, col.R, col.G, col.B, col.A * opacity, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// Called ONCE on cache miss. Computes (text, xOffset, color) for every segment in a row.
        /// The render loop then just does batch.Add per segment with the pre-computed xOffset.
        /// </summary>
        private MdSegment[] BuildRowSegments(
            string lineText, int lineStart, int lineEnd,
            IReadOnlyList<MarkdownToken> tokens, MarkdownTheme theme,
            PaperColour defaultCol, float fontPx, string? fam, FontWeight? weight, FontStyle? fontStyle)
        {
            if (_fonts == null) return Array.Empty<MdSegment>();
            var result = new System.Collections.Generic.List<MdSegment>();
            int cursor = 0;
            float runW = 0f;
            foreach (var tok in tokens)
            {
                if (tok.End <= lineStart || tok.Start >= lineEnd) continue;
                int tokLocalStart = Math.Max(tok.Start, lineStart) - lineStart;
                int tokLocalEnd   = Math.Min(tok.End,   lineEnd)   - lineStart;
                // Plain text before this token
                if (cursor < tokLocalStart)
                {
                    var span = lineText.AsSpan(cursor, tokLocalStart - cursor);
                    result.Add(new MdSegment(lineText[cursor..tokLocalStart], runW,
                        defaultCol.R, defaultCol.G, defaultCol.B, defaultCol.A));
                    runW += _fonts.MeasureWidth(span, fontPx, fam, weight, fontStyle);
                    cursor = tokLocalStart;
                }
                var tokCol = GetMarkdownTokenColor(tok.Type, theme, defaultCol);
                result.Add(new MdSegment(lineText[tokLocalStart..tokLocalEnd], runW,
                    tokCol.R, tokCol.G, tokCol.B, tokCol.A));
                runW += _fonts.MeasureWidth(lineText.AsSpan(tokLocalStart, tokLocalEnd - tokLocalStart), fontPx, fam, weight, fontStyle);
                cursor = tokLocalEnd;
            }
            // Trailing plain text
            if (cursor < lineText.Length)
                result.Add(new MdSegment(lineText[cursor..], runW,
                    defaultCol.R, defaultCol.G, defaultCol.B, defaultCol.A));
            return result.ToArray();
        }

        private static PaperColour GetMarkdownTokenColor(MarkdownTokenType type, MarkdownTheme theme, PaperColour fallback) => type switch
        {
            MarkdownTokenType.HeadingMarker    => theme.HeadingMarkerColor,
            MarkdownTokenType.HeadingText      => theme.HeadingColor,
            MarkdownTokenType.Delimiter        => theme.DelimiterColor,
            MarkdownTokenType.Bold             => theme.ProseColor,
            MarkdownTokenType.Italic           => theme.ProseColor,
            MarkdownTokenType.BoldItalic       => theme.ProseColor,
            MarkdownTokenType.InlineCode       => theme.InlineCodeColor,
            MarkdownTokenType.BlockquoteMarker => theme.DelimiterColor,
            MarkdownTokenType.ListMarker       => theme.ListMarkerColor,
            MarkdownTokenType.HrMarker         => theme.HrColor,
            MarkdownTokenType.CodeFenceMarker  => theme.DelimiterColor,
            MarkdownTokenType.CodeFenceContent => theme.CodeFenceColor,
            MarkdownTokenType.Text             => theme.ProseColor,
            _                                  => fallback,
        };

        private void DrawText(string label, LayoutBox lb, StyleSheet style,
                              PaperColour col, float opacity, float scrollX = 0f, float scrollY = 0f, float inputScrollX = 0f)
        {
            if (_fonts == null) return;

            float fontPx     = SilkTextMeasurer.ResolveFontPx(style);
            string? fam      = style.FontFamily;
            var weight       = style.FontWeight;
            var fontStyle    = style.FontStyle;

            // Apply TextTransform before measurement / rendering
            var transform = style.TextTransform;
            if (transform != null && transform != TextTransform.None)
                label = transform switch
                {
                    TextTransform.Uppercase  => label.ToUpperInvariant(),
                    TextTransform.Lowercase  => label.ToLowerInvariant(),
                    TextTransform.Capitalize => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(label.ToLowerInvariant()),
                    _ => label,
                };

            // Select the atlas at physical pixel size so glyphs are rasterized at native
            // Retina resolution (no upscaling). atlasLineH stays in logical pixels for
            // layout/baseline calculations which are done in logical space.
            var (batch, batchScale) = _fonts.Get(fontPx * DpiScale, fam, weight, fontStyle);
            float atlasLineH = _fonts.LineHeight(fontPx, fam, weight, fontStyle);

            // Synthetic italic: apply shear when the style requests italic but the resolved batch
            // is not a dedicated italic font (covers regular→italic and bold→bold-italic gaps).
            if (_fonts.WillUseSyntheticItalic(fam, weight, fontStyle))
                batch.ItalicSkew = 0.21f;  // ~12° slant (tan of typical italic angle)

            // Padding from style (respects individual PaddingTop/Right/Bottom/Left overrides)
            var (padTop, padRight, padBottom, padLeft) = BoxModel.PaddingPixels(style, lb.Width, lb.Height);

            // Vertical alignment: centre in content area using raw glyph height; otherwise top-align.
            // Only skip centering when the box is smaller than the text itself.
            float contentH = lb.Height - padTop - padBottom;
            float baseline;
            if (contentH >= atlasLineH)
            {
                float centreY = lb.AbsoluteY + padTop + (contentH - atlasLineH) / 2f;
                baseline = centreY + (atlasLineH * 0.8f);
            }
            else
            {
                baseline = lb.AbsoluteY + padTop + (atlasLineH * 0.8f);
            }

            // Horizontal alignment (TextAlign). When inputScrollX is set (single-line input), left-align and apply scroll.
            float textW    = _fonts.MeasureWidth(label.AsSpan(), fontPx, fam, weight, fontStyle);
            float contentW = lb.Width - padLeft - padRight;

            // TextOverflow: Ellipsis — truncate label to fit contentW if needed
            ReadOnlySpan<char> drawSpan = label.AsSpan();
            if (inputScrollX == 0f && contentW > 0 && textW > contentW &&
                (style.TextOverflow ?? TextOverflow.Clip) == TextOverflow.Ellipsis)
            {
                const string ellipsis = "…";
                float ellipsisW = _fonts.MeasureWidth(ellipsis.AsSpan(), fontPx, fam, weight, fontStyle);
                float available = contentW - ellipsisW;
                if (available > 0)
                {
                    int lo = 0, hi = label.Length;
                    while (lo < hi)
                    {
                        int mid = (lo + hi + 1) / 2;
                        if (_fonts.MeasureWidth(label.AsSpan(0, mid), fontPx, fam, weight, fontStyle) <= available)
                            lo = mid;
                        else
                            hi = mid - 1;
                    }
                    label = label[..lo] + ellipsis;
                    textW = _fonts.MeasureWidth(label.AsSpan(), fontPx, fam, weight, fontStyle);
                }
                else
                {
                    label = ellipsis;
                    textW = ellipsisW;
                }
                drawSpan = label.AsSpan();
            }

            // Word-wrap mode: when WhiteSpace.Normal and text overflows contentW
            bool doWrap = style.WhiteSpace == WhiteSpace.Normal && inputScrollX == 0f &&
                          contentW > 0 && textW > contentW;
            if (doWrap)
            {
                float spaceW      = _fonts.MeasureWidth(" ".AsSpan(), fontPx, fam, weight, fontStyle);
                if (spaceW <= 0) spaceW = atlasLineH * 0.3f;
                float lineSpacing = atlasLineH * Math.Max(0.5f, style.LineHeight ?? 1.4f);
                float wrapBaseline = lb.AbsoluteY + padTop + (atlasLineH * 0.8f);
                float xOrigin = lb.AbsoluteX + padLeft;

                var words     = label.Split(' ');
                var lineWords = new System.Collections.Generic.List<string>(words.Length);
                float lineW   = 0;

                foreach (var word in words)
                {
                    float wordW = _fonts.MeasureWidth(word.AsSpan(), fontPx, fam, weight, fontStyle);
                    if (lineWords.Count > 0 && lineW + spaceW + wordW > contentW)
                    {
                        float lx = (xOrigin - scrollX) * ScaleX;
                        float ly = (wrapBaseline - scrollY) * ScaleY;
                        batch.Add(string.Join(' ', lineWords).AsSpan(), lx, ly, col.R, col.G, col.B, col.A * opacity, batchScale);
                        wrapBaseline += lineSpacing;
                        lineWords.Clear();
                        lineW = 0;
                    }
                    lineWords.Add(word);
                    lineW += (lineW > 0 ? spaceW : 0) + wordW;
                }
                if (lineWords.Count > 0)
                {
                    float lx = (xOrigin - scrollX) * ScaleX;
                    float ly = (wrapBaseline - scrollY) * ScaleY;
                    batch.Add(string.Join(' ', lineWords).AsSpan(), lx, ly, col.R, col.G, col.B, col.A * opacity, batchScale);
                }
                return;
            }

            float xLayout;
            bool isRtl = style.Direction == Direction.Rtl;
            if (inputScrollX != 0f)
                xLayout = lb.AbsoluteX + padLeft - inputScrollX;
            else
            {
                // In RTL mode, swap left and right alignment
                var textAlign = style.TextAlign ?? TextAlign.Left;
                if (isRtl)
                {
                    textAlign = textAlign switch
                    {
                        TextAlign.Left => TextAlign.Right,
                        TextAlign.Right => TextAlign.Left,
                        _ => textAlign
                    };
                }
                xLayout = textAlign switch
                {
                    TextAlign.Center => lb.AbsoluteX + padLeft + Math.Max(0, (contentW - textW) / 2f),
                    TextAlign.Right => lb.AbsoluteX + lb.Width - padRight - textW,
                    _ => lb.AbsoluteX + padLeft, // Left, Justify
                };
            }

            float x = (xLayout - scrollX) * ScaleX;
            float y = (baseline - scrollY) * ScaleY;
            batch.Add(drawSpan, x, y, col.R, col.G, col.B, col.A * opacity, batchScale);
        }
    }
}
