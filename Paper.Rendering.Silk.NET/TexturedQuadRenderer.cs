using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// Renders a single textured quad at a given screen-space rect.
    /// Used by FiberRenderer to display the embedded engine's game-view framebuffer.
    /// </summary>
    internal sealed class TexturedQuadRenderer : IDisposable
    {
        private readonly GL   _gl;
        private readonly uint _program;
        private readonly uint _vao;
        private readonly uint _vbo;

        private readonly int _uRect;
        private readonly int _uUV;
        private readonly int _uResolution;
        private readonly int _uTexture;

        public unsafe TexturedQuadRenderer(GL gl)
        {
            _gl      = gl;
            _program = GlHelpers.LinkProgram(gl, Shaders.ViewportVert, Shaders.ViewportFrag);
            _uRect       = GlHelpers.Uniform(gl, _program, "uRect");
            _uUV         = GlHelpers.Uniform(gl, _program, "uUV");
            _uResolution = GlHelpers.Uniform(gl, _program, "uResolution");
            _uTexture    = GlHelpers.Uniform(gl, _program, "uTexture");

            // Interleaved: pos.xy (2 floats) + uv.xy (2 floats) = 4 floats per vertex
            float[] quad = [
                // aPos    aUV
                0f, 0f,   0f, 0f,
                1f, 0f,   1f, 0f,
                1f, 1f,   1f, 1f,
                0f, 0f,   0f, 0f,
                1f, 1f,   1f, 1f,
                0f, 1f,   0f, 1f,
            ];

            _vao = gl.GenVertexArray();
            gl.BindVertexArray(_vao);

            _vbo = GlHelpers.CreateVbo(gl, quad, GLEnum.StaticDraw);

            uint stride = (uint)(4 * sizeof(float));

            // location 0: aPos (vec2)
            gl.VertexAttribPointer(0, 2, GLEnum.Float, false, stride, (void*)0);
            gl.EnableVertexAttribArray(0);

            // location 1: aUV (vec2)
            gl.VertexAttribPointer(1, 2, GLEnum.Float, false, stride, (void*)(2 * sizeof(float)));
            gl.EnableVertexAttribArray(1);

            gl.BindVertexArray(0);
        }

        /// <summary>
        /// Draw a textured quad at the given screen-space rect using <paramref name="textureHandle"/>.
        /// Full texture is used (UV 0,0 to 1,1).
        /// </summary>
        public void Draw(float x, float y, float w, float h,
                         uint textureHandle, float screenW, float screenH)
        {
            DrawWithUV(x, y, w, h, 0f, 0f, 1f, 1f, textureHandle, screenW, screenH);
        }

        /// <summary>
        /// Draw a textured quad with a custom UV rect (for object-fit cover/contain).
        /// </summary>
        public void DrawWithUV(float x, float y, float w, float h,
            float u0, float v0, float u1, float v1,
            uint textureHandle, float screenW, float screenH)
        {
            if (textureHandle == 0) return;

            _gl.UseProgram(_program);
            _gl.Uniform4(_uRect,       x, y, w, h);
            _gl.Uniform4(_uUV,         u0, v0, u1, v1);
            _gl.Uniform2(_uResolution, screenW, screenH);
            _gl.Uniform1(_uTexture, 0);

            _gl.ActiveTexture(GLEnum.Texture0);
            _gl.BindTexture(GLEnum.Texture2D, textureHandle);

            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
            _gl.BindVertexArray(0);

            _gl.BindTexture(GLEnum.Texture2D, 0);
        }

        public void Dispose()
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteProgram(_program);
        }
    }
}
