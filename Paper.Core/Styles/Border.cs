namespace Paper.Core.Styles
{
    /// <summary>
    /// A CSS-like border definition: width, style, and colour.
    /// </summary>
    public readonly struct Border : IEquatable<Border>
    {
        public float Width { get; }
        public BorderStyle Style { get; }
        public PaperColour Colour { get; }

        public Border(float width, PaperColour colour, BorderStyle style = BorderStyle.Solid)
        {
            Width = width;
            Style = style;
            Colour = colour;
        }

        public static readonly Border None = new(0f, PaperColour.Transparent, BorderStyle.None);

        public bool Equals(Border other) =>
            Width == other.Width && Style == other.Style && Colour == other.Colour;
        public override bool Equals(object? obj) => obj is Border b && Equals(b);
        public override int GetHashCode() => HashCode.Combine(Width, Style, Colour);
        public static bool operator ==(Border a, Border b) => a.Equals(b);
        public static bool operator !=(Border a, Border b) => !a.Equals(b);

        public override string ToString() => $"{Width}px {Style} {Colour}";
    }

    /// <summary>
    /// Per-side border definitions, allowing each edge to have a different border.
    /// </summary>
    public sealed class BorderEdges
    {
        public Border Top { get; init; }
        public Border Right { get; init; }
        public Border Bottom { get; init; }
        public Border Left { get; init; }

        public BorderEdges(Border all) { Top = Right = Bottom = Left = all; }
        public BorderEdges() { }

        public static implicit operator BorderEdges(Border b) => new(b);
    }
}
