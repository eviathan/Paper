using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// GPU-instanced renderer for axis-aligned rectangles with:
    /// - Solid background colour
    /// - Rounded corners (via SDF)
    /// - Border (uniform width, per-rect colour)
    /// One draw call flushes all queued rects.
    /// </summary>
    internal sealed class RectBatch : IDisposable
    {
        private const int MaxRects = 4096;

        // Instanced layout: 15 floats × 4 = 60 bytes per rect
        // [pos.xy, size.xy, bg.rgba, border.rgba, borderWidth, radius, rotation]
        private const int FloatsPerRect = 15;

        private readonly GL _gl;
        private readonly uint _program;
        private readonly uint _vao;
        private readonly uint _quadVbo;       // static base quad
        private readonly uint _instanceVbo;   // dynamic instance data

        private readonly float[] _instanceData = new float[MaxRects * FloatsPerRect];
        private int _count;

        private readonly int _uResolution;

        public unsafe RectBatch(GL gl)
        {
            _gl = gl;
            _program = GlHelpers.LinkProgram(gl, Shaders.RectVert, Shaders.RectFrag);
            _uResolution = GlHelpers.Uniform(gl, _program, "uResolution");

            // Base quad: 2 triangles covering [0,1]×[0,1]
            float[] quad = [
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

            // Instance VBO (allocated but empty)
            _instanceVbo = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ArrayBuffer, _instanceVbo);
            nuint instanceBytes = (nuint)(MaxRects * FloatsPerRect * sizeof(float));
            gl.BufferData(GLEnum.ArrayBuffer, instanceBytes, (void*)null, GLEnum.DynamicDraw);

            uint stride = (uint)(FloatsPerRect * sizeof(float));
            uint offset = 0;

            // location=1: pos (vec2)
            SetInstanceAttrib(1, 2, stride, ref offset);
            // location=2: size (vec2)
            SetInstanceAttrib(2, 2, stride, ref offset);
            // location=3: bgColor (vec4)
            SetInstanceAttrib(3, 4, stride, ref offset);
            // location=4: borderColor (vec4)
            SetInstanceAttrib(4, 4, stride, ref offset);
            // location=5: borderWidth (float)
            SetInstanceAttrib(5, 1, stride, ref offset);
            // location=6: radius (float)
            SetInstanceAttrib(6, 1, stride, ref offset);
            // location=7: rotation (float, radians)
            SetInstanceAttrib(7, 1, stride, ref offset);

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

        public void Add(float x, float y, float w, float h,
            float r, float g, float b, float a,
            float br = 0f, float bg_ = 0f, float bb = 0f, float ba = 0f,
            float borderWidth = 0f, float radius = 0f, float rotation = 0f)
        {
            if (_count >= MaxRects) Flush(0, 0); // emergency flush

            int i = _count * FloatsPerRect;
            _instanceData[i++] = x;
            _instanceData[i++] = y;
            _instanceData[i++] = w;
            _instanceData[i++] = h;
            _instanceData[i++] = r; _instanceData[i++] = g;
            _instanceData[i++] = b; _instanceData[i++] = a;
            _instanceData[i++] = br; _instanceData[i++] = bg_;
            _instanceData[i++] = bb; _instanceData[i++] = ba;
            _instanceData[i++] = borderWidth;
            _instanceData[i++] = radius;
            _instanceData[i++] = rotation;
            _count++;
        }

        public unsafe void Flush(float screenW, float screenH)
        {
            if (_count == 0) return;

            // Upload instance data
            GlHelpers.UpdateVbo(_gl, _instanceVbo, _instanceData, _count * FloatsPerRect);

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
