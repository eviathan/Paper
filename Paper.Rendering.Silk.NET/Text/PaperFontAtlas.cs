namespace Paper.Rendering.Silk.NET.Text
{
    internal sealed class PaperFontAtlas
    {
        public uint  TextureHandle { get; internal set; }
        public int   AtlasSize     { get; }
        public int   BaseSize      { get; }
        public float LineHeight    { get; internal set; }

        private readonly Dictionary<int, GlyphMetrics> _glyphs = new();
        public  IReadOnlyDictionary<int, GlyphMetrics>  Glyphs => _glyphs;

        public PaperFontAtlas(int atlasSize, int baseSize)
        {
            AtlasSize = atlasSize;
            BaseSize  = baseSize;
        }

        internal void AddGlyph(int codepoint, GlyphMetrics m) => _glyphs[codepoint] = m;

        public bool TryGetGlyph(char c, out GlyphMetrics m) =>
            _glyphs.TryGetValue(c, out m);
    }

    internal readonly struct GlyphMetrics
    {
        /// <summary>Normalised UV coordinates in the atlas texture.</summary>
        public float U0 { get; init; }
        public float V0 { get; init; }
        public float U1 { get; init; }
        public float V1 { get; init; }

        /// <summary>Pixel dimensions of the glyph bitmap in the atlas.</summary>
        public float Width   { get; init; }
        public float Height  { get; init; }

        /// <summary>Offset from pen position to top-left of glyph bitmap.</summary>
        public float BearingX { get; init; }
        public float BearingY { get; init; }

        /// <summary>How far to advance the pen after this glyph (pixels).</summary>
        public float Advance { get; init; }
    }
}
