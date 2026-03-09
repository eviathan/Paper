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

        private static unsafe PaperFontAtlas LoadSingle(GL gl, FT_FaceRec_* face, int pixelSize, int atlasSize)
        {
            var atlas      = new PaperFontAtlas(atlasSize, pixelSize);
            var atlasBytes = new byte[atlasSize * atlasSize];

            FT_Set_Pixel_Sizes(face, 0, (uint)pixelSize);

            int cursorX   = Padding;
            int cursorY   = Padding;
            int rowHeight = 0;

            for (int cp = 32; cp < 128; cp++)
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

                if (cursorY + bh > atlasSize) break;

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

            atlas.LineHeight = (float)((int)face->size->metrics.height >> 6);

            UploadTexture(gl, atlas, atlasBytes, atlasSize);
            return atlas;
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
