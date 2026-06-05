using Paper.Core.Rendering;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Paper.Rendering.Silk.NET.Text;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// OpenGL-backed implementation of <see cref="ICanvas2DContext"/>.
    /// Converts element-local coordinates to framebuffer pixels and dispatches to the
    /// backing GPU batches.
    /// </summary>
    internal sealed class Canvas2DContext : ICanvas2DContext
    {
        private readonly LineBatch             _lines;
        private readonly RectBatch             _rects;
        private readonly TextBatch?            _text;
        private readonly TexturedQuadRenderer? _viewports;

        // Element's screen-space top-left in framebuffer pixels (already scaled)
        private readonly float _ox;
        private readonly float _oy;
        private readonly float _scaleX;
        private readonly float _scaleY;
        private readonly float _screenW;
        private readonly float _screenH;

        public float Width    { get; }
        public float Height   { get; }
        public float DpiScale { get; }

        public Canvas2DContext(
            LineBatch             lines,
            RectBatch             rects,
            TextBatch?            text,
            TexturedQuadRenderer? viewports,
            float drawX, float drawY, float drawW, float drawH,
            float scaleX, float scaleY,
            float screenW, float screenH)
        {
            _lines    = lines;
            _rects    = rects;
            _text     = text;
            _viewports = viewports;
            _ox       = drawX;
            _oy       = drawY;
            _scaleX   = scaleX;
            _scaleY   = scaleY;
            _screenW  = screenW;
            _screenH  = screenH;
            DpiScale  = scaleX;   // scaleX == scaleY for square pixels
            Width     = scaleX > 0f ? drawW / scaleX : drawW;
            Height    = scaleY > 0f ? drawH / scaleY : drawH;
        }

        // ── Single-primitive draws ────────────────────────────────────────────

        public void DrawLine(float x1, float y1, float x2, float y2,
                             PaperColour color, float thickness = 1f)
        {
            _lines.Add(
                _ox + x1 * _scaleX, _oy + y1 * _scaleY,
                _ox + x2 * _scaleX, _oy + y2 * _scaleY,
                color.R, color.G, color.B, color.A,
                Math.Max(1f, thickness * _scaleX));
        }

        public void DrawPolyline(IReadOnlyList<(float x, float y)> points,
                                 PaperColour color, float thickness = 1f)
        {
            if (points.Count < 2) return;
            for (int i = 0; i < points.Count - 1; i++)
                DrawLine(points[i].x, points[i].y, points[i + 1].x, points[i + 1].y,
                         color, thickness);
        }

        public void DrawCircle(float cx, float cy, float radius, PaperColour color)
        {
            float sx = _ox + (cx - radius) * _scaleX;
            float sy = _oy + (cy - radius) * _scaleY;
            float sw = radius * 2f * _scaleX;
            float sh = radius * 2f * _scaleY;
            float r  = Math.Min(sw, sh) * 0.5f;
            _rects.Add(sx, sy, sw, sh,
                color.R, color.G, color.B, color.A,
                0f, 0f, 0f, 0f, 0f,
                r, r, r, r);
        }

        public void FillRect(float x, float y, float w, float h,
                             PaperColour color, float cornerRadius = 0f)
        {
            float r = cornerRadius * Math.Min(_scaleX, _scaleY);
            _rects.Add(
                _ox + x * _scaleX, _oy + y * _scaleY,
                w * _scaleX,       h * _scaleY,
                color.R, color.G, color.B, color.A,
                0f, 0f, 0f, 0f, 0f,
                r, r, r, r);
        }

        public void DrawText(string text, float x, float y, float fontSize, PaperColour color)
        {
            if (_text == null || string.IsNullOrEmpty(text)) return;
            float baseSize = _text.Atlas.BaseSize > 0 ? _text.Atlas.BaseSize : 16f;
            float scale    = fontSize * _scaleX / baseSize;
            float sx       = _ox + x * _scaleX;
            float sy       = _oy + (y + fontSize * 0.82f) * _scaleY; // top-left y → baseline
            _text.Add(text.AsSpan(), sx, sy, color.R, color.G, color.B, color.A, scale);
        }

        // ── Bulk draws ────────────────────────────────────────────────────────

        public void DrawRects(ReadOnlySpan<PaperRect> rects)
        {
            for (int i = 0; i < rects.Length; i++)
            {
                ref readonly var pr = ref rects[i];
                float r = pr.CornerRadius * Math.Min(_scaleX, _scaleY);
                _rects.Add(
                    _ox + pr.X     * _scaleX, _oy + pr.Y      * _scaleY,
                    pr.Width * _scaleX,        pr.Height * _scaleY,
                    pr.Fill.R,   pr.Fill.G,   pr.Fill.B,   pr.Fill.A,
                    pr.Border.R, pr.Border.G, pr.Border.B, pr.Border.A,
                    pr.BorderWidth * _scaleX,
                    r, r, r, r);
            }
        }

        public void DrawLines(ReadOnlySpan<PaperLine> lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                ref readonly var pl = ref lines[i];
                _lines.Add(
                    _ox + pl.X1 * _scaleX, _oy + pl.Y1 * _scaleY,
                    _ox + pl.X2 * _scaleX, _oy + pl.Y2 * _scaleY,
                    pl.Color.R, pl.Color.G, pl.Color.B, pl.Color.A,
                    Math.Max(1f, pl.Thickness * _scaleX));
            }
        }

        // ── Texture draws ─────────────────────────────────────────────────────

        public void DrawTexture(TextureHandle texture,
                                float x, float y, float w, float h)
            => DrawTexture(texture, x, y, w, h, 0f, 0f, 1f, 1f);

        public void DrawTexture(TextureHandle texture,
                                float x, float y, float w, float h,
                                float u0, float v0, float u1, float v1)
        {
            if (!texture.IsValid || _viewports == null) return;

            // Flush pending batches so the texture composites correctly in draw order.
            _rects.Flush(_screenW, _screenH);
            _lines.Flush(_screenW, _screenH);

            float sx = _ox + x * _scaleX;
            float sy = _oy + y * _scaleY;
            float sw = w * _scaleX;
            float sh = h * _scaleY;
            _viewports.DrawWithUV(sx, sy, sw, sh, u0, v0, u1, v1,
                                  texture.GlHandle, _screenW, _screenH);
        }
    }
}
