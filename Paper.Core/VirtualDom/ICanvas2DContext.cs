using Paper.Core.Styles;

namespace Paper.Core.VirtualDom
{
    /// <summary>
    /// Immediate-mode 2D drawing surface for a <c>canvas2d</c> element.
    /// Coordinates are in element-local pixels: (0,0) = top-left of the element.
    /// </summary>
    public interface ICanvas2DContext
    {
        /// <summary>Element width in logical pixels.</summary>
        float Width  { get; }
        /// <summary>Element height in logical pixels.</summary>
        float Height { get; }

        void DrawLine(float x1, float y1, float x2, float y2, PaperColour color, float thickness = 1f);

        void DrawPolyline(IReadOnlyList<(float x, float y)> points, PaperColour color, float thickness = 1f);

        /// <summary>Filled circle — useful for keyframe dots and handles.</summary>
        void DrawCircle(float cx, float cy, float radius, PaperColour color);

        /// <summary>Filled rectangle — useful for dope sheet diamonds and selection rects.</summary>
        void FillRect(float x, float y, float w, float h, PaperColour color, float cornerRadius = 0f);
    }
}
