using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>OpenGL shader / buffer helper utilities.</summary>
    internal static class GlHelpers
    {
        public static uint CompileShader(GL gl, ShaderType type, string source)
        {
            uint shader = gl.CreateShader(type);
            gl.ShaderSource(shader, source);
            gl.CompileShader(shader);

            gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
            {
                string log = gl.GetShaderInfoLog(shader);
                gl.DeleteShader(shader);
                throw new Exception($"Shader compile error ({type}): {log}");
            }
            return shader;
        }

        public static uint LinkProgram(GL gl, string vert, string frag)
        {
            uint vs = CompileShader(gl, ShaderType.VertexShader, vert);
            uint fs = CompileShader(gl, ShaderType.FragmentShader, frag);
            uint prg = gl.CreateProgram();
            gl.AttachShader(prg, vs);
            gl.AttachShader(prg, fs);
            gl.LinkProgram(prg);
            gl.DeleteShader(vs);
            gl.DeleteShader(fs);

            gl.GetProgram(prg, ProgramPropertyARB.LinkStatus, out int linked);
            if (linked == 0)
            {
                string log = gl.GetProgramInfoLog(prg);
                gl.DeleteProgram(prg);
                throw new Exception($"Program link error: {log}");
            }
            return prg;
        }

        public static int Uniform(GL gl, uint program, string name) =>
            gl.GetUniformLocation(program, name);

        public static unsafe uint CreateVbo(GL gl, float[] data, GLEnum usage)
        {
            uint vbo = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            fixed (float* ptr = data)
                gl.BufferData(GLEnum.ArrayBuffer,
                    (nuint)(data.Length * sizeof(float)), ptr, usage);
            return vbo;
        }

        public static unsafe void UpdateVbo(GL gl, uint vbo, float[] data, int count, int maxCount)
        {
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            // Orphan the buffer before writing — tells the driver we're done with the old data
            // so it doesn't stall the CPU waiting for the GPU to finish reading it (pipeline sync).
            // On macOS's Metal-backed OpenGL this makes a ~100ms stall per call into a ~0ms one.
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(maxCount * sizeof(float)), (void*)null, GLEnum.StreamDraw);
            fixed (float* ptr = data)
                gl.BufferSubData(GLEnum.ArrayBuffer, 0, (nuint)(count * sizeof(float)), ptr);
        }
    }
}
