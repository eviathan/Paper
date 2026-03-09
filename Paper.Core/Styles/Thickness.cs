namespace Paper.Core.Styles
{
    /// <summary>
    /// Four-sided spacing value used for padding and margin.
    /// </summary>
    public readonly struct Thickness : IEquatable<Thickness>
    {
        public Length Top    { get; }
        public Length Right  { get; }
        public Length Bottom { get; }
        public Length Left   { get; }

        public static readonly Thickness Zero = new(Length.Zero);

        public Thickness(Length all)
            : this(all, all, all, all) { }

        public Thickness(Length vertical, Length horizontal)
            : this(vertical, horizontal, vertical, horizontal) { }

        public Thickness(Length top, Length right, Length bottom, Length left)
        {
            Top    = top;
            Right  = right;
            Bottom = bottom;
            Left   = left;
        }

        /// <summary>Create thickness with uniform length on all sides.</summary>
        public static Thickness All(Length length) => new(length);
        
        /// <summary>Create thickness with uniform length on all sides (pixels).</summary>
        public static Thickness All(float px) => new(Length.Px(px));
        
        /// <summary>Create thickness with uniform length on all sides (pixels).</summary>
        public static Thickness All(int px) => new(Length.Px(px));
        
        /// <summary>Create thickness with horizontal and vertical lengths.</summary>
        public static Thickness Horizontal(Length length) => new(Length.Zero, length, Length.Zero, length);
        
        /// <summary>Create thickness with horizontal and vertical lengths (pixels).</summary>
        public static Thickness Horizontal(float px) => new(Length.Zero, Length.Px(px), Length.Zero, Length.Px(px));
        
        /// <summary>Create thickness with horizontal and vertical lengths (pixels).</summary>
        public static Thickness Horizontal(int px) => new(Length.Zero, Length.Px(px), Length.Zero, Length.Px(px));
        
        /// <summary>Create thickness with horizontal and vertical lengths.</summary>
        public static Thickness Vertical(Length length) => new(length, Length.Zero, length, Length.Zero);
        
        /// <summary>Create thickness with horizontal and vertical lengths (pixels).</summary>
        public static Thickness Vertical(float px) => new(Length.Px(px), Length.Zero, Length.Px(px), Length.Zero);
        
        /// <summary>Create thickness with horizontal and vertical lengths (pixels).</summary>
        public static Thickness Vertical(int px) => new(Length.Px(px), Length.Zero, Length.Px(px), Length.Zero);

        /// <summary>Uniform pixel shorthand: <c>new Thickness(8)</c> = 8px on all sides.</summary>
        public static implicit operator Thickness(float px) => new(Length.Px(px));
        public static implicit operator Thickness(int   px) => new(Length.Px(px));

        public bool Equals(Thickness other) =>
            Top == other.Top && Right == other.Right &&
            Bottom == other.Bottom && Left == other.Left;

        public override bool Equals(object? obj) => obj is Thickness t && Equals(t);
        public override int  GetHashCode() => HashCode.Combine(Top, Right, Bottom, Left);
        public static bool operator ==(Thickness a, Thickness b) => a.Equals(b);
        public static bool operator !=(Thickness a, Thickness b) => !a.Equals(b);

        public override string ToString() => $"{Top} {Right} {Bottom} {Left}";
    }
}
