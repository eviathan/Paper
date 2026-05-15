using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// OpenGL-backed implementation of <see cref="ICanvas2DContext"/>.
    /// Converts element-local coordinates to screen pixels and dispatches to the backing batches.
    /// </summary>
    internal sealed class Canvas2DContext : ICanvas2DContext
    {
        private readonly LineBatch  _lines;
        private readonly RectBatch  _rects;

        // Element's screen-space top-left (already scaled)
        private readonly float _ox;
        private readonly float _oy;
        private readonly float _scaleX;
        private readonly float _scaleY;

        public float Width  { get; }
        public float Height { get; }

        public Canvas2DContext(LineBatch lines, RectBatch rects,
            float drawX, float drawY, float drawW, float drawH,
            float scaleX, float scaleY)
        {
            _lines  = lines;
            _rects  = rects;
            _ox     = drawX;
            _oy     = drawY;
            _scaleX = scaleX;
            _scaleY = scaleY;
            Width   = scaleX > 0f ? drawW / scaleX : drawW;
            Height  = scaleY > 0f ? drawH / scaleY : drawH;
        }

        public void DrawLine(float x1, float y1, float x2, float y2, PaperColour color, float thickness = 1f)
        {
            _lines.Add(
                _ox + x1 * _scaleX, _oy + y1 * _scaleY,
                _ox + x2 * _scaleX, _oy + y2 * _scaleY,
                (float)color.R, (float)color.G, (float)color.B, (float)color.A,
                Math.Max(1f, thickness * _scaleX));
        }

        public void DrawPolyline(IReadOnlyList<(float x, float y)> points, PaperColour color, float thickness = 1f)
        {
            if (points.Count < 2) return;
            for (int i = 0; i < points.Count - 1; i++)
                DrawLine(points[i].x, points[i].y, points[i + 1].x, points[i + 1].y, color, thickness);
        }

        public void DrawCircle(float cx, float cy, float radius, PaperColour color)
        {
            // Approximate circle with the existing RectBatch via a fully-rounded rect.
            float sx = _ox + (cx - radius) * _scaleX;
            float sy = _oy + (cy - radius) * _scaleY;
            float sw = radius * 2f * _scaleX;
            float sh = radius * 2f * _scaleY;
            float r  = Math.Min(sw, sh) * 0.5f;
            _rects.Add(sx, sy, sw, sh,
                (float)color.R, (float)color.G, (float)color.B, (float)color.A,
                0f, 0f, 0f, 0f, 0f,
                r, r, r, r);
        }

        public void FillRect(float x, float y, float w, float h, PaperColour color, float cornerRadius = 0f)
        {
            float sx = _ox + x * _scaleX;
            float sy = _oy + y * _scaleY;
            float sw = w * _scaleX;
            float sh = h * _scaleY;
            float r  = cornerRadius * Math.Min(_scaleX, _scaleY);
            _rects.Add(sx, sy, sw, sh,
                (float)color.R, (float)color.G, (float)color.B, (float)color.A,
                0f, 0f, 0f, 0f, 0f,
                r, r, r, r);
        }
    }
}
