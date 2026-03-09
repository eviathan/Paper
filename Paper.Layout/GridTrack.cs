namespace Paper.Layout
{
    /// <summary>
    /// The sizing function of a CSS grid track.
    /// </summary>
    internal enum GridTrackKind
    {
        /// <summary>Fixed pixel size.</summary>
        Px,
        /// <summary>Percentage of the grid container.</summary>
        Percent,
        /// <summary>Fractional unit — shares remaining space after fixed/percent tracks.</summary>
        Fr,
        /// <summary>Size to fit content (Paper approximates as zero base size, then grows with fr).</summary>
        Auto,
    }

    /// <summary>
    /// Represents one column or row track in a CSS grid.
    /// </summary>
    internal sealed class GridTrack
    {
        public GridTrackKind Kind  { get; }
        public float         Value { get; }

        public GridTrack(GridTrackKind kind, float value)
        {
            Kind  = kind;
            Value = value;
        }

        /// <summary>
        /// Resolve this track's size in pixels given the grid container's size.
        /// Returns NaN for <see cref="GridTrackKind.Fr"/> and <see cref="GridTrackKind.Auto"/>
        /// — these are resolved after all fixed tracks are sized.
        /// </summary>
        public float Resolve(float containerSize) => Kind switch
        {
            GridTrackKind.Px      => Value,
            GridTrackKind.Percent => Value / 100f * containerSize,
            _                     => float.NaN,
        };

        public bool IsFractional => Kind == GridTrackKind.Fr;
        public bool IsAuto       => Kind == GridTrackKind.Auto;
    }

    /// <summary>
    /// A resolved track: its final pixel size and starting position.
    /// </summary>
    internal struct ResolvedTrack
    {
        public float Start  { get; set; }
        public float Size   { get; set; }
        public float End    => Start + Size;
    }
}
