using Paper.Core.Rendering;
using Paper.Core.Styles;

namespace Paper.Core.VirtualDom
{
    /// <summary>
    /// Immediate-mode 2D drawing surface for a <c>canvas2d</c> element.
    /// Coordinates are in element-local logical pixels: (0,0) = top-left of the element.
    ///
    /// The context is only valid for the duration of the draw callback — do not store it.
    /// </summary>
    public interface ICanvas2DContext
    {
        // ── Metrics ───────────────────────────────────────────────────────────

        /// <summary>Element width in logical pixels.</summary>
        float Width  { get; }

        /// <summary>Element height in logical pixels.</summary>
        float Height { get; }

        /// <summary>
        /// Physical-to-logical pixel ratio (e.g. 2 on a Retina display).
        /// Use this to produce crisp pixel-art or to size textures correctly.
        /// </summary>
        float DpiScale { get; }

        // ── Single-primitive draws ────────────────────────────────────────────

        void DrawLine(float x1, float y1, float x2, float y2,
                      PaperColour color, float thickness = 1f);

        void DrawPolyline(IReadOnlyList<(float x, float y)> points,
                          PaperColour color, float thickness = 1f);

        /// <summary>Filled circle — useful for keyframe dots and handles.</summary>
        void DrawCircle(float cx, float cy, float radius, PaperColour color);

        /// <summary>Filled rectangle — useful for dope-sheet diamonds and selection rects.</summary>
        void FillRect(float x, float y, float w, float h,
                      PaperColour color, float cornerRadius = 0f);

        /// <summary>Draws text at element-local (x, y) where y is the top of the text bounding box.</summary>
        void DrawText(string text, float x, float y, float fontSize, PaperColour color);

        // ── Bulk draws (zero-allocation, span-based) ─────────────────────────

        /// <summary>
        /// Draw many rectangles in a single instanced call.
        /// For DAW use cases (meter bars, note blocks, grid lines) this is significantly cheaper
        /// than calling <see cref="FillRect"/> in a loop.
        /// </summary>
        void DrawRects(ReadOnlySpan<PaperRect> rects);

        /// <summary>
        /// Draw many anti-aliased line segments in a single batched call.
        /// </summary>
        void DrawLines(ReadOnlySpan<PaperLine> lines);

        // ── Texture draws ─────────────────────────────────────────────────────

        /// <summary>
        /// Draw a GPU texture, stretching it to fill the given element-local rect.
        /// The texture must have been created via <c>Canvas.TextureFactory</c>.
        /// Pending rect/line batches are automatically flushed beforehand so draw order
        /// is preserved.
        /// </summary>
        void DrawTexture(TextureHandle texture, float x, float y, float w, float h);

        /// <summary>
        /// Draw a sub-region of a GPU texture using custom UV coordinates.
        /// UV (0,0) = top-left, (1,1) = bottom-right of the texture.
        /// Useful for tiling or atlas sampling.
        /// </summary>
        void DrawTexture(TextureHandle texture,
                         float x, float y, float w, float h,
                         float u0, float v0, float u1, float v1);
    }
}
