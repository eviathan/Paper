namespace Paper.Core.Styles
{
    /// <summary>
    /// A CSS-like box shadow definition.
    /// </summary>
    public readonly struct BoxShadow
    {
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float BlurRadius { get; }
        public float SpreadRadius { get; }
        public PaperColour Colour { get; }
        public bool Inset { get; }

        public BoxShadow(float offsetX, float offsetY, float blurRadius,
                         PaperColour colour, float spreadRadius = 0f, bool inset = false)
        {
            OffsetX = offsetX;
            OffsetY = offsetY;
            BlurRadius = blurRadius;
            SpreadRadius = spreadRadius;
            Colour = colour;
            Inset = inset;
        }
    }
}
