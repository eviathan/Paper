using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// GPU-instanced renderer for anti-aliased line segments.
    /// Each segment is a single instance: p0, p1, color, thickness.
    /// One draw call flushes all queued segments.
    /// </summary>
    internal sealed class LineBatch : IDisposable
    {
        private const int MaxSegments  = 8192;
        private const int FloatsPerSeg = 9; // p0.xy, p1.xy, color.rgba, thickness

        private readonly GL   _gl;
        private readonly uint _program;
        private readonly uint _vao;
        private readonly uint _quadVbo;
        private readonly uint _instanceVbo;

        private readonly float[] _instanceData = new float[MaxSegments * FloatsPerSeg];
        private int _count;

        private readonly int _uResolution;

        public unsafe LineBatch(GL gl)
        {
            _gl = gl;
            _program     = GlHelpers.LinkProgram(gl, Shaders.LineVert, Shaders.LineFrag);
            _uResolution = GlHelpers.Uniform(gl, _program, "uResolution");

            // Base quad: unit [0,1]×[0,1] expanded per-instance into the segment rect
            float[] quad =
            [
                0f, 0f,
                1f, 0f,
                1f, 1f,
                0f, 0f,
                1f, 1f,
                0f, 1f,
            ];

            _vao = gl.GenVertexArray();
            gl.BindVertexArray(_vao);

            _quadVbo = GlHelpers.CreateVbo(gl, quad, GLEnum.StaticDraw);
            gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 8, 0);
            gl.EnableVertexAttribArray(0);

            _instanceVbo = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ArrayBuffer, _instanceVbo);
            nuint instanceBytes = (nuint)(MaxSegments * FloatsPerSeg * sizeof(float));
            gl.BufferData(GLEnum.ArrayBuffer, instanceBytes, (void*)null, GLEnum.DynamicDraw);

            uint stride = (uint)(FloatsPerSeg * sizeof(float));
            uint offset = 0;

            SetInstanceAttrib(1, 2, stride, ref offset); // iP0
            SetInstanceAttrib(2, 2, stride, ref offset); // iP1
            SetInstanceAttrib(3, 4, stride, ref offset); // iColor
            SetInstanceAttrib(4, 1, stride, ref offset); // iThickness

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

        public void Add(float x0, float y0, float x1, float y1,
                        float r, float g, float b, float a,
                        float thickness = 1f)
        {
            if (_count >= MaxSegments) Flush(0, 0);

            int i = _count * FloatsPerSeg;
            _instanceData[i++] = x0;
            _instanceData[i++] = y0;
            _instanceData[i++] = x1;
            _instanceData[i++] = y1;
            _instanceData[i++] = r;
            _instanceData[i++] = g;
            _instanceData[i++] = b;
            _instanceData[i++] = a;
            _instanceData[i++] = thickness;
            _count++;
        }

        public unsafe void Flush(float screenW, float screenH)
        {
            if (_count == 0) return;

            GlHelpers.UpdateVbo(_gl, _instanceVbo, _instanceData, _count * FloatsPerSeg, MaxSegments * FloatsPerSeg);

            _gl.UseProgram(_program);
            _gl.Uniform2(_uResolution, screenW, screenH);

            _gl.BindVertexArray(_vao);
            _gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, (uint)_count);
            _gl.BindVertexArray(0);

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
