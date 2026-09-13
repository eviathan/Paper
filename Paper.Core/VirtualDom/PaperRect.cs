using Paper.Core.Styles;

namespace Paper.Core.VirtualDom
{
    /// <summary>
    /// A rectangle to draw via <see cref="ICanvas2DContext.DrawRects"/>.
    /// Coordinates are in element-local logical pixels.
    /// </summary>
    public readonly struct PaperRect
    {
        public readonly float      X;
        public readonly float      Y;
        public readonly float      Width;
        public readonly float      Height;
        public readonly PaperColour Fill;
        public readonly float      CornerRadius;
        public readonly PaperColour Border;
        public readonly float      BorderWidth;

        public PaperRect(float x, float y, float width, float height,
                         PaperColour fill,
                         float cornerRadius = 0f,
                         PaperColour border = default,
                         float borderWidth = 0f)
        {
            X            = x;
            Y            = y;
            Width        = width;
            Height       = height;
            Fill         = fill;
            CornerRadius = cornerRadius;
            Border       = border;
            BorderWidth  = borderWidth;
        }
    }
}
