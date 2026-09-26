using System;

namespace Paper.Core.VirtualDom
{
    /// <summary>
    /// Which part of a sprite sheet a <see cref="ElementTypes.Sprite"/> element draws. Either:
    /// <list type="bullet">
    /// <item>a <see cref="Region"/> — an explicit pixel rectangle, for packed atlases whose frames
    /// sit anywhere and can differ in size (the rectangle an <c>.atlas</c> manifest gives); or</item>
    /// <item>a <see cref="Cell"/> — a flat row-major index into a uniform grid of equal cells, for
    /// simple strips and grids. The grid's column count comes from the sheet's width, so a cell only
    /// becomes a rectangle once the sheet is known (<see cref="Resolve"/>).</item>
    /// </list>
    /// A value with equality, so the reconciler only re-renders a sprite when its slice changes.
    /// </summary>
    public readonly struct SpriteSlice : IEquatable<SpriteSlice>
    {
        private SpriteSlice(bool isCell, int index, float x, float y, float width, float height)
        {
            IsCell = isCell;
            Index  = index;
            X      = x;
            Y      = y;
            Width  = width;
            Height = height;
        }

        /// <summary>True for a grid <see cref="Cell"/>; false for an explicit <see cref="Region"/>.</summary>
        public bool  IsCell { get; }

        /// <summary>The cell index (grid cells only).</summary>
        public int   Index  { get; }

        /// <summary>Top-left corner in sheet pixels, origin top-left (regions only).</summary>
        public float X      { get; }
        public float Y      { get; }

        /// <summary>Size in sheet pixels — the region's, or each grid cell's.</summary>
        public float Width  { get; }
        public float Height { get; }

        /// <summary>An explicit rectangle of the sheet, in pixels from its top-left.</summary>
        public static SpriteSlice Region(float x, float y, float width, float height) =>
            new(false, 0, x, y, width, height);

        /// <summary>Cell <paramref name="index"/> (row-major) of a grid of
        /// <paramref name="cellWidth"/>×<paramref name="cellHeight"/> cells.</summary>
        public static SpriteSlice Cell(int index, float cellWidth, float cellHeight) =>
            new(true, index, 0, 0, cellWidth, cellHeight);

        /// <summary>Whether this can address anything at all.</summary>
        public bool IsValid => Width > 0 && Height > 0 && (!IsCell || Index >= 0);

        /// <summary>
        /// The pixel rectangle on a sheet <paramref name="sheetWidth"/>×<paramref name="sheetHeight"/>
        /// px, or false if it falls outside the sheet.
        /// </summary>
        public bool Resolve(int sheetWidth, int sheetHeight, out float x, out float y, out float width, out float height)
        {
            width = Width;
            height = Height;
            if (IsCell)
            {
                int columns = Math.Max(1, (int)(sheetWidth / Width));
                x = Index % columns * Width;
                y = Index / columns * Height;
            }
            else
            {
                x = X;
                y = Y;
            }
            return IsValid && x >= 0 && y >= 0 && x + width <= sheetWidth && y + height <= sheetHeight;
        }

        public bool Equals(SpriteSlice other) =>
            IsCell == other.IsCell && Index == other.Index
            && X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);

        public override bool Equals(object? obj) => obj is SpriteSlice other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(IsCell, Index, X, Y, Width, Height);

        public static bool operator ==(SpriteSlice left, SpriteSlice right) => left.Equals(right);
        public static bool operator !=(SpriteSlice left, SpriteSlice right) => !left.Equals(right);

        public override string ToString() =>
            IsCell ? $"cell {Index} ({Width}x{Height})" : $"region ({X}, {Y}) {Width}x{Height}";
    }
}
