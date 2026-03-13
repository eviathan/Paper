namespace Paper.Core.Styles
{
    /// <summary>
    /// RGBA colour used throughout Paper's style system.
    /// Named PaperColour to avoid ambiguity if Paper is used alongside the engine's Colour type.
    /// </summary>
    public readonly struct PaperColour : IEquatable<PaperColour>
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        public PaperColour(float r, float g, float b, float a = 1f)
        {
            R = Math.Clamp(r, 0f, 1f);
            G = Math.Clamp(g, 0f, 1f);
            B = Math.Clamp(b, 0f, 1f);
            A = Math.Clamp(a, 0f, 1f);
        }

        /// <summary>Parse a hex colour string: #RGB, #RRGGBB, or #RRGGBBAA.</summary>
        public PaperColour(string hex)
        {
            hex = hex.TrimStart('#');
            switch (hex.Length)
            {
                case 3:
                    R = Convert.ToInt32(hex[0..1] + hex[0..1], 16) / 255f;
                    G = Convert.ToInt32(hex[1..2] + hex[1..2], 16) / 255f;
                    B = Convert.ToInt32(hex[2..3] + hex[2..3], 16) / 255f;
                    A = 1f;
                    break;
                case 6:
                    R = Convert.ToInt32(hex[0..2], 16) / 255f;
                    G = Convert.ToInt32(hex[2..4], 16) / 255f;
                    B = Convert.ToInt32(hex[4..6], 16) / 255f;
                    A = 1f;
                    break;
                case 8:
                    R = Convert.ToInt32(hex[0..2], 16) / 255f;
                    G = Convert.ToInt32(hex[2..4], 16) / 255f;
                    B = Convert.ToInt32(hex[4..6], 16) / 255f;
                    A = Convert.ToInt32(hex[6..8], 16) / 255f;
                    break;
                default:
                    throw new ArgumentException($"Invalid hex colour: #{hex}");
            }
        }

        public static readonly PaperColour Transparent = new(0f, 0f, 0f, 0f);
        public static readonly PaperColour Black = new(0f, 0f, 0f);
        public static readonly PaperColour White = new(1f, 1f, 1f);
        public static readonly PaperColour Red = new(1f, 0f, 0f);
        public static readonly PaperColour Green = new(0f, 1f, 0f);
        public static readonly PaperColour Blue = new(0f, 0f, 1f);

        public PaperColour WithAlpha(float a) => new(R, G, B, a);

        public PaperColour Lerp(PaperColour other, float t) =>
            new(R + (other.R - R) * t,
                G + (other.G - G) * t,
                B + (other.B - B) * t,
                A + (other.A - A) * t);

        public bool Equals(PaperColour other) =>
            R == other.R && G == other.G && B == other.B && A == other.A;
        public override bool Equals(object? obj) => obj is PaperColour c && Equals(c);
        public override int GetHashCode() => HashCode.Combine(R, G, B, A);
        public static bool operator ==(PaperColour a, PaperColour b) => a.Equals(b);
        public static bool operator !=(PaperColour a, PaperColour b) => !a.Equals(b);

        public override string ToString() => $"rgba({R:F2},{G:F2},{B:F2},{A:F2})";
    }
}
