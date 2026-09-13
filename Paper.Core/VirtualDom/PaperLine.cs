using Paper.Core.Styles;

namespace Paper.Core.VirtualDom
{
    /// <summary>
    /// An anti-aliased line segment to draw via <see cref="ICanvas2DContext.DrawLines"/>.
    /// Coordinates are in element-local logical pixels.
    /// </summary>
    public readonly struct PaperLine
    {
        public readonly float      X1;
        public readonly float      Y1;
        public readonly float      X2;
        public readonly float      Y2;
        public readonly PaperColour Color;
        public readonly float      Thickness;

        public PaperLine(float x1, float y1, float x2, float y2,
                         PaperColour color,
                         float thickness = 1f)
        {
            X1        = x1;
            Y1        = y1;
            X2        = x2;
            Y2        = y2;
            Color     = color;
            Thickness = thickness;
        }
    }
}
