using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET.Text
{
    /// <summary>
    /// GPU-instanced text renderer for Paper UI.
    /// Batches all glyphs across a frame into a single draw call per Flush().
    /// Uses Shaders.TextVert / TextFrag (samples R8 atlas, uses red channel as alpha).
    /// </summary>
    internal sealed class TextBatch : IDisposable
    {
        private const int MaxGlyphs = 8192;

        // Instance layout: iPos(2) + iSize(2) + iUVRect(4) + iColor(4) = 12 floats
        private const int FloatsPerGlyph = 12;

        private readonly GL            _gl;
        private readonly PaperFontAtlas _atlas;
        private readonly uint          _program;
        private readonly uint          _vao;
        private readonly uint          _quadVbo;
        private readonly uint          _instanceVbo;

        private readonly float[] _instanceData = new float[MaxGlyphs * FloatsPerGlyph];
        private int _count;

        private readonly int _uResolution;
        private readonly int _uFontAtlas;

        public PaperFontAtlas Atlas => _atlas;

        public unsafe TextBatch(GL gl, PaperFontAtlas atlas)
        {
            _gl      = gl;
            _atlas   = atlas;
            _program = GlHelpers.LinkProgram(gl, Shaders.TextVert, Shaders.TextFrag);
            _uResolution = GlHelpers.Uniform(gl, _program, "uResolution");
            _uFontAtlas  = GlHelpers.Uniform(gl, _program, "uFontAtlas");

            // Base quad: pos[0..1] + uv[0..1]
            float[] quad =
            [
                // pos    uv
                0f, 0f,  0f, 0f,
                1f, 0f,  1f, 0f,
                1f, 1f,  1f, 1f,
                0f, 0f,  0f, 0f,
                1f, 1f,  1f, 1f,
                0f, 1f,  0f, 1f,
            ];

            _vao = gl.GenVertexArray();
            gl.BindVertexArray(_vao);

            _quadVbo = GlHelpers.CreateVbo(gl, quad, GLEnum.StaticDraw);

            uint stride = 4 * sizeof(float);   // 4 floats per vertex: pos.xy + uv.xy = 16 bytes
            // location 0: aPos (vec2)
            gl.VertexAttribPointer(0, 2, GLEnum.Float, false, stride, 0);
            gl.EnableVertexAttribArray(0);
            // location 1: aUV (vec2) — offset 2 floats (8 bytes) into each vertex
            gl.VertexAttribPointer(1, 2, GLEnum.Float, false, stride, (void*)(2 * sizeof(float)));
            gl.EnableVertexAttribArray(1);

            // Instance VBO
            _instanceVbo = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ArrayBuffer, _instanceVbo);
            nuint instanceBytes = (nuint)(MaxGlyphs * FloatsPerGlyph * sizeof(float));
            gl.BufferData(GLEnum.ArrayBuffer, instanceBytes, (void*)null, GLEnum.DynamicDraw);

            uint istride = (uint)(FloatsPerGlyph * sizeof(float));
            uint offset  = 0;

            SetInstanceAttrib(2, 2, istride, ref offset); // iPos
            SetInstanceAttrib(3, 2, istride, ref offset); // iSize
            SetInstanceAttrib(4, 4, istride, ref offset); // iUVRect
            SetInstanceAttrib(5, 4, istride, ref offset); // iColor

            gl.BindVertexArray(0);
        }

        private unsafe void SetInstanceAttrib(uint loc, int count, uint stride, ref uint offset)
        {
            _gl.VertexAttribPointer(loc, count, GLEnum.Float, false, stride, (void*)offset);
            _gl.EnableVertexAttribArray(loc);
            _gl.VertexAttribDivisor(loc, 1);
            offset += (uint)(count * sizeof(float));
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Measures the pixel width of a string. <paramref name="scale"/> is applied to each
        /// glyph advance — use 1f for the atlas native size, or fontPx/BaseSize for a scaled size.
        /// </summary>
        public float MeasureWidth(ReadOnlySpan<char> text, float scale = 1f)
        {
            float w = 0;
            foreach (char c in text)
                if (_atlas.TryGetGlyph(c, out var m))
                    w += m.Advance * scale;
            return w;
        }

        /// <summary>
        /// Queues a text run at pixel position (x, y = baseline).
        /// <paramref name="scale"/> scales glyph dimensions so the text renders at
        /// a size other than the atlas base size. Call Flush() to issue the draw call.
        /// </summary>
        public void Add(ReadOnlySpan<char> text, float x, float y,
                        float r, float g, float b, float a, float scale = 1f)
        {
            float penX = x;

            foreach (char c in text)
            {
                if (c == ' ')
                {
                    if (_atlas.TryGetGlyph(' ', out var sp)) penX += sp.Advance * scale;
                    else penX += _atlas.BaseSize * 0.3f * scale;
                    continue;
                }

                if (!_atlas.TryGetGlyph(c, out var m)) continue;
                if (_count >= MaxGlyphs) Flush(0, 0);

                float gx = penX + m.BearingX * scale;
                float gy = y    - m.BearingY * scale;   // top-left of glyph bitmap

                int i = _count * FloatsPerGlyph;
                _instanceData[i++] = gx;
                _instanceData[i++] = gy;
                _instanceData[i++] = m.Width  * scale;
                _instanceData[i++] = m.Height * scale;
                _instanceData[i++] = m.U0;
                _instanceData[i++] = m.V0;
                _instanceData[i++] = m.U1;
                _instanceData[i++] = m.V1;
                _instanceData[i++] = r;
                _instanceData[i++] = g;
                _instanceData[i++] = b;
                _instanceData[i]   = a;

                _count++;
                penX += m.Advance * scale;
            }
        }

        public unsafe void Flush(float screenW, float screenH)
        {
            if (_count == 0) return;

            GlHelpers.UpdateVbo(_gl, _instanceVbo, _instanceData, _count * FloatsPerGlyph, MaxGlyphs * FloatsPerGlyph);

            _gl.UseProgram(_program);
            _gl.Uniform2(_uResolution, screenW, screenH);

            // Bind the font atlas to texture unit 0
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _atlas.TextureHandle);
            _gl.Uniform1(_uFontAtlas, 0);

            // Single-channel texture — tell OpenGL how to swizzle (R → RGBA)
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            _gl.BindVertexArray(_vao);
            _gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, (uint)_count);
            _gl.BindVertexArray(0);

            _gl.BindTexture(TextureTarget.Texture2D, 0);
            _count = 0;
        }

        public void Dispose()
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_quadVbo);
            _gl.DeleteBuffer(_instanceVbo);
            _gl.DeleteProgram(_program);
        }
    }
}
