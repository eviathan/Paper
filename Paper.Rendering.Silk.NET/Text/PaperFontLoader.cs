using FreeTypeSharp;
using Silk.NET.OpenGL;
using System.Runtime.InteropServices;
using static FreeTypeSharp.FT;
using static FreeTypeSharp.FT_LOAD;
using static FreeTypeSharp.FT_Render_Mode_;

namespace Paper.Rendering.Silk.NET.Text
{
    /// <summary>
    /// Loads a TrueType font with FreeType and packs ASCII glyphs into an R8
    /// atlas texture. Supports multiple sizes for crisp rendering at all scales.
    /// </summary>
    internal static class PaperFontLoader
    {
        // Sizes baked into the multi-size set (pixels). Covers typical UI text sizes.
        internal static readonly int[] DefaultSizes = [11, 13, 16, 20, 24, 28, 32, 40, 48];

        private const int Padding = 1;

        /// <summary>Loads a set of atlas sizes from the same font file (FreeType initialised once).</summary>
        public static unsafe Dictionary<int, PaperFontAtlas> LoadSet(GL gl, string fontPath, int[]? sizes = null)
        {
            sizes ??= DefaultSizes;

            FT_LibraryRec_* library;
            FT_Init_FreeType(&library);

            FT_FaceRec_* face;
            var pathPtr = Marshal.StringToHGlobalAnsi(fontPath);
            try   { FT_New_Face(library, (byte*)pathPtr, 0, &face); }
            finally { Marshal.FreeHGlobal(pathPtr); }

            var result = new Dictionary<int, PaperFontAtlas>();

            foreach (int px in sizes)
            {
                // Larger glyphs need a bigger atlas to fit 96 ASCII characters.
                // Each glyph is roughly px × px; atlas must fit ~96 of them.
                int atlasSize = px >= 32 ? 1024 : 512;
                var atlas = LoadSingle(gl, face, px, atlasSize);
                result[px] = atlas;
            }

            FT_Done_Face(face);
            FT_Done_FreeType(library);

            return result;
        }

        /// <summary>Loads a single atlas at the given pixel size (convenience overload).</summary>
        public static unsafe PaperFontAtlas Load(GL gl, string fontPath, int pixelSize = 16)
        {
            FT_LibraryRec_* library;
            FT_Init_FreeType(&library);

            FT_FaceRec_* face;
            var pathPtr = Marshal.StringToHGlobalAnsi(fontPath);
            try   { FT_New_Face(library, (byte*)pathPtr, 0, &face); }
            finally { Marshal.FreeHGlobal(pathPtr); }

            int atlasSize = pixelSize >= 32 ? 1024 : 512;
            var atlas = LoadSingle(gl, face, pixelSize, atlasSize);

            FT_Done_Face(face);
            FT_Done_FreeType(library);
            return atlas;
        }

        /// <summary>
        /// Loads an icon font: discovers all codepoints available in the font via FreeType's
        /// cmap iterator and packs them all into a larger atlas. Use for fonts like Material Icons
        /// or Font Awesome where glyphs are in Unicode Private Use Area ranges.
        /// </summary>
        public static unsafe Dictionary<int, PaperFontAtlas> LoadIconSet(GL gl, string fontPath, int[]? sizes = null)
        {
            sizes ??= DefaultSizes;

            FT_LibraryRec_* library;
            FT_Init_FreeType(&library);

            FT_FaceRec_* face;
            var pathPtr = Marshal.StringToHGlobalAnsi(fontPath);
            try   { FT_New_Face(library, (byte*)pathPtr, 0, &face); }
            finally { Marshal.FreeHGlobal(pathPtr); }

            // Discover all codepoints in the font via cmap iteration.
            var codepoints = new List<int>();
            uint glyphIdx = 0;
            nuint cp = FT_Get_First_Char(face, &glyphIdx);
            while (glyphIdx != 0)
            {
                codepoints.Add((int)(uint)cp);
                cp = FT_Get_Next_Char(face, (uint)cp, &glyphIdx);
            }

            var result = new Dictionary<int, PaperFontAtlas>();
            foreach (int px in sizes)
            {
                // Icon atlases need more space; use 2048² for all sizes.
                var atlas = LoadSingleCodepoints(gl, face, px, 2048, codepoints);
                result[px] = atlas;
            }

            FT_Done_Face(face);
            FT_Done_FreeType(library);
            return result;
        }

        /// <summary>Largest atlas this loader will grow to before giving up trying to fit every glyph.</summary>
        private const int MaxAtlasSize = 4096;

        private static unsafe PaperFontAtlas LoadSingleCodepoints(GL gl, FT_FaceRec_* face, int pixelSize, int atlasSize, IEnumerable<int> codepoints)
        {
            // Materialize once: codepoints is re-walked on every retry below.
            var codepointList = codepoints as IReadOnlyList<int> ?? codepoints.ToList();

            FT_Set_Pixel_Sizes(face, 0, (uint)pixelSize);

            // atlasSize is only a heuristic guess (px>=32?1024:512 by glyph pixel size, ignoring how
            // many codepoints this particular font actually has). A font with an unusually large
            // character set could still overflow that guess; growing and repacking instead of
            // silently dropping whatever didn't fit — which is what PackGlyphs used to just do via a
            // bare `break` — is what actually guarantees every glyph gets a slot, at any font size.
            while (true)
            {
                var atlas      = new PaperFontAtlas(atlasSize, pixelSize);
                var atlasBytes = new byte[atlasSize * atlasSize];

                int cursorX   = Padding;
                int cursorY   = Padding;
                int rowHeight = 0;

                bool overflowed = PackGlyphs(face, codepointList, atlasSize, atlasBytes, atlas, ref cursorX, ref cursorY, ref rowHeight);
                if (!overflowed || atlasSize >= MaxAtlasSize)
                {
                    atlas.LineHeight = (float)((int)face->size->metrics.height >> 6);
                    atlas.Ascender   = (float)((int)face->size->metrics.ascender >> 6);
                    UploadTexture(gl, atlas, atlasBytes, atlasSize);
                    return atlas;
                }

                atlasSize *= 2;
            }
        }

        private static unsafe PaperFontAtlas LoadSingle(GL gl, FT_FaceRec_* face, int pixelSize, int atlasSize)
        {
            // Discover all codepoints available in this font so that any supported character renders.
            // This is the same approach as LoadIconSet — no arbitrary ASCII-only restriction.
            var codepoints = new List<int>();
            uint glyphIdx = 0;
            nuint cp = FT_Get_First_Char(face, &glyphIdx);
            while (glyphIdx != 0)
            {
                codepoints.Add((int)(uint)cp);
                cp = FT_Get_Next_Char(face, (uint)cp, &glyphIdx);
            }

            // Was hardcoded to 2048 here regardless of what the caller passed in, silently
            // overriding LoadSet/Load's px>=32?1024:512 sizing — every regular-text atlas ended up
            // 2048x2048 (4x-16x more GPU memory than intended) no matter the glyph pixel size.
            return LoadSingleCodepoints(gl, face, pixelSize, atlasSize, codepoints);
        }

        /// <summary>
        /// Packs every codepoint into the atlas. Returns true if it ran out of room and had to stop
        /// early — the caller (<see cref="LoadSingleCodepoints"/>) retries at double the atlas size
        /// rather than accepting a partial bake, so no glyph is ever silently missing at render time.
        /// </summary>
        private static unsafe bool PackGlyphs(
            FT_FaceRec_* face,
            IReadOnlyList<int> codepoints,
            int atlasSize,
            byte[] atlasBytes,
            PaperFontAtlas atlas,
            ref int cursorX, ref int cursorY, ref int rowHeight)
        {
            foreach (int cp in codepoints)
            {
                uint glyphIndex = FT_Get_Char_Index(face, (uint)cp);
                if (glyphIndex == 0) continue;

                FT_Load_Glyph(face, glyphIndex, FT_LOAD_DEFAULT);
                FT_Render_Glyph(face->glyph, FT_RENDER_MODE_NORMAL);

                int bw = (int)face->glyph->bitmap.width;
                int bh = (int)face->glyph->bitmap.rows;

                // Shelf-pack: wrap to new row if needed
                if (cursorX + bw + Padding > atlasSize)
                {
                    cursorX   = Padding;
                    cursorY  += rowHeight + Padding;
                    rowHeight = 0;
                }

                if (cursorY + bh > atlasSize) return true;

                for (int row = 0; row < bh; row++)
                {
                    int srcRow = row * (int)face->glyph->bitmap.pitch;
                    int dstRow = (cursorY + row) * atlasSize + cursorX;
                    for (int col = 0; col < bw; col++)
                        atlasBytes[dstRow + col] = face->glyph->bitmap.buffer[srcRow + col];
                }

                atlas.AddGlyph(cp, new GlyphMetrics
                {
                    U0       = (float)cursorX        / atlasSize,
                    V0       = (float)cursorY        / atlasSize,
                    U1       = (float)(cursorX + bw) / atlasSize,
                    V1       = (float)(cursorY + bh) / atlasSize,
                    Width    = bw,
                    Height   = bh,
                    BearingX = face->glyph->bitmap_left,
                    BearingY = face->glyph->bitmap_top,
                    Advance  = (float)((int)face->glyph->advance.x >> 6),
                });

                cursorX  += bw + Padding;
                rowHeight = Math.Max(rowHeight, bh);
            }

            return false;
        }

        private static unsafe void UploadTexture(GL gl, PaperFontAtlas atlas, byte[] pixels, int atlasSize)
        {
            uint handle = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, handle);

            fixed (byte* ptr = pixels)
            {
                gl.TexImage2D(TextureTarget.Texture2D, 0,
                    InternalFormat.R8,
                    (uint)atlasSize, (uint)atlasSize, 0,
                    PixelFormat.Red, PixelType.UnsignedByte, ptr);
            }

            // Linear, not Nearest: text is only ever baked at 9 fixed sizes (PaperFontLoader.DefaultSizes)
            // and PaperFontSet.Get picks the *nearest* one, scaling the glyph quad to whatever size was
            // actually requested — most requested sizes don't land exactly on a baked size, so this
            // scale is very often != 1. Nearest filtering under a non-1 scale is what made text look
            // visibly blocky/aliased (worst on small text, e.g. badge counts, which land furthest from
            // any baked size). 1px of transparent padding around each glyph (see Padding below) keeps
            // bilinear sampling from ever reaching into a neighboring glyph's ink.
            gl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            gl.BindTexture(TextureTarget.Texture2D, 0);
            atlas.TextureHandle = handle;
        }
    }
}
