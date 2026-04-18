using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Rendering.Silk.NET.Text;
using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// Walks a committed fiber tree (after layout) and issues draw calls.
    /// Handles: background, borders, border-radius, opacity, text, viewport elements, overflow clip/scroll.
    /// </summary>
    internal sealed partial class FiberRenderer
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

        // Current visible clip rect in screen pixels. Narrowed when entering scroll/clip containers
        // so off-screen children can be skipped without GPU draw calls.
        private (float X, float Y, float W, float H) _cullRect;

        public record struct ScrollbarHit(float TrackX, float TrackY, float TrackH, float ThumbY, float ThumbH, float MaxScroll, float MaxScrollX);
        public readonly Dictionary<string, ScrollbarHit> RenderedScrollbars = new();

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
        private const int MaxCacheSize = 100;
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
            public float OpacityTarget = 1f;
            public bool Initialized;
        }

        private readonly Dictionary<string, TransitionState> _transitions = new();
        private int _evictionCounter;

        private void EvictCachesIfNeeded()
        {
            _evictionCounter++;
            if (_evictionCounter >= 60)
            {
                _evictionCounter = 0;
                if (_mdCache.Count > MaxCacheSize / 2)
                {
                    var toRemove = _mdCache.Keys.Take(_mdCache.Count / 4).ToList();
                    foreach (var k in toRemove) _mdCache.Remove(k);
                }
                if (_transitions.Count > MaxCacheSize / 2)
                {
                    var toRemove = _transitions.Keys.Take(_transitions.Count / 4).ToList();
                    foreach (var k in toRemove) _transitions.Remove(k);
                }
            }
        }
        private double _lastFrameTime = -1.0;
        private float _frameDt;

        /// <summary>True if any CSS transition is still animating (not yet converged to its target).</summary>
        public bool HasActiveTransitions
        {
            get
            {
                const float epsilon = 0.002f;
                foreach (var transitionState in _transitions.Values)
                {
                    if (!transitionState.Initialized) continue;
                    if (Math.Abs(transitionState.BgCurrent.R - transitionState.BgTarget.R) > epsilon ||
                        Math.Abs(transitionState.BgCurrent.G - transitionState.BgTarget.G) > epsilon ||
                        Math.Abs(transitionState.BgCurrent.B - transitionState.BgTarget.B) > epsilon ||
                        Math.Abs(transitionState.BgCurrent.A - transitionState.BgTarget.A) > epsilon ||
                        Math.Abs(transitionState.OpacityCurrent - transitionState.OpacityTarget) > epsilon)
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
            var durations = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
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
                    durations[tokens[0]] = Math.Max(0.001f, dur);
                }
            }
            _transitionSpecCache[spec] = durations;
            return durations;
        }

        private static float GetTransitionDuration(Dictionary<string, float> spec, string prop)
        {
            if (spec.TryGetValue(prop, out float duration)) return duration;
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
        /// <summary>
        /// Draws a small stylised panel schematic ghost (header strip + content area lines) centered
        /// on the cursor. Use this for dock panel drag previews instead of <see cref="RenderGhost"/>.
        /// <paramref name="cursorX"/>/<paramref name="cursorY"/> are in layout (window) pixel space.
        /// </summary>
        public void RenderPanelGhost(float cursorX, float cursorY)
        {
            const float ghostW = 180f, ghostH = 110f, headerH = 24f, radius = 5f;
            float s   = ScaleX;
            float gx  = (cursorX - ghostW / 2f) * s;
            float gy  = (cursorY - headerH / 2f) * s;
            float gw  = ghostW * s;
            float gh  = ghostH * s;
            float hh  = headerH * s;
            float br  = radius; // border radius

            // Body background
            _rects.Add(gx, gy, gw, gh,
                0.11f, 0.11f, 0.24f, 0.88f,
                radiusTL: br, radiusTR: br, radiusBR: br, radiusBL: br);

            // Header strip
            _rects.Add(gx, gy, gw, hh,
                0.17f, 0.17f, 0.38f, 0.92f,
                radiusTL: br, radiusTR: br);

            // Outer border
            _rects.Add(gx, gy, gw, gh,
                0f, 0f, 0f, 0f,
                0.28f, 0.35f, 0.72f, 0.95f,
                borderWidth: 1.5f * s,
                radiusTL: br, radiusTR: br, radiusBR: br, radiusBL: br);

            // Content-area placeholder lines
            float cx  = gx + 10f * s;
            float cw1 = gw * 0.60f, cw2 = gw * 0.45f, cw3 = gw * 0.55f;
            float lh  = 2f * s;
            float ly  = gy + (headerH + 14f) * s;
            _rects.Add(cx, ly,            cw1, lh, 0.28f, 0.28f, 0.52f, 0.55f);
            _rects.Add(cx, ly + 9f * s,  cw2, lh, 0.28f, 0.28f, 0.52f, 0.40f);
            _rects.Add(cx, ly + 18f * s, cw3, lh, 0.28f, 0.28f, 0.52f, 0.30f);

            // Two small "button" dots in header (visual cue)
            float dotY  = gy + (headerH / 2f - 3f) * s;
            float dotW  = 6f * s, dotH = 6f * s;
            float dotX2 = gx + gw - 12f * s;
            float dotX1 = dotX2 - 10f * s;
            _rects.Add(dotX1, dotY, dotW, dotH, 0.35f, 0.35f, 0.6f, 0.7f, radiusTL: 2, radiusTR: 2, radiusBR: 2, radiusBL: 2);
            _rects.Add(dotX2, dotY, dotW, dotH, 0.6f,  0.25f, 0.25f, 0.7f, radiusTL: 2, radiusTR: 2, radiusBR: 2, radiusBL: 2);
        }

        public void RenderGhost(Fiber? fiber, float cursorX, float cursorY, float opacity = 0.5f)
        {
            if (fiber == null) return;
            var layoutBox = fiber.Layout;
            _ghostOffsetX = cursorX - layoutBox.AbsoluteX - layoutBox.Width / 2f;
            _ghostOffsetY = cursorY - layoutBox.AbsoluteY - layoutBox.Height / 2f;

            var savedSibling = fiber.Sibling;
            fiber.Sibling = null;

            var saved = _zIndexed;
            _zIndexed = null;
            Render(fiber, opacity, "", 0, 0f, 0f);
            _zIndexed = saved;

            fiber.Sibling = savedSibling;
            _ghostOffsetX = 0f;
            _ghostOffsetY = 0f;
        }

        public void Render(Fiber? fiber, float inheritedOpacity = 1f)
        {
            EvictCachesIfNeeded();
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
    }
}
