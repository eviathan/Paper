namespace Paper.Core.Styles
{
    /// <summary>
    /// A CSS-like length value: pixels, percentage, em, auto, or none.
    /// </summary>
    public readonly struct Length : IEquatable<Length>
    {
        public enum Unit { Px, Percent, Em, Auto, None }

        public float Value { get; }
        public Unit Kind { get; }

        public static readonly Length Auto = new(0f, Unit.Auto);
        public static readonly Length None = new(0f, Unit.None);
        public static readonly Length Zero = new(0f, Unit.Px);

        private Length(float value, Unit kind) { Value = value; Kind = kind; }

        public static Length Px(float v) => new(v, Unit.Px);
        public static Length Percent(float v) => new(v, Unit.Percent);
        public static Length Em(float v) => new(v, Unit.Em);

        public bool IsAuto => Kind == Unit.Auto;
        public bool IsNone => Kind == Unit.None;

        /// <summary>
        /// Resolves this length to pixels given a container size (used for %) and font size (used for em).
        /// Returns 0 for Auto/None — caller must handle those cases.
        /// </summary>
        public float Resolve(float containerSize = 0f, float fontSize = 16f) => Kind switch
        {
            Unit.Px => Value,
            Unit.Percent => Value / 100f * containerSize,
            Unit.Em => Value * fontSize,
            _ => 0f,
        };

        // Implicit conversions for ergonomic use
        public static implicit operator Length(float v) => Px(v);
        public static implicit operator Length(int v) => Px(v);

        public bool Equals(Length other) => Kind == other.Kind && Value == other.Value;
        public override bool Equals(object? obj) => obj is Length l && Equals(l);
        public override int GetHashCode() => HashCode.Combine(Kind, Value);
        public static bool operator ==(Length a, Length b) => a.Equals(b);
        public static bool operator !=(Length a, Length b) => !a.Equals(b);

        public override string ToString() => Kind switch
        {
            Unit.Px => $"{Value}px",
            Unit.Percent => $"{Value}%",
            Unit.Em => $"{Value}em",
            Unit.Auto => "auto",
            Unit.None => "none",
            _ => "0",
        };
    }
}
