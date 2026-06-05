using Paper.Core.Rendering;
using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// OpenGL implementation of <see cref="ITextureFactory"/>.
    /// All methods must be called from the render thread (the thread that owns the GL context).
    /// </summary>
    internal sealed class GlTextureFactory : ITextureFactory
    {
        private readonly GL _gl;

        internal GlTextureFactory(GL gl) => _gl = gl;

        public TextureHandle CreateRgba(int width, int height, ReadOnlySpan<byte> pixels)
        {
            if (width <= 0 || height <= 0)
                return TextureHandle.Invalid;

            uint handle = _gl.GenTexture();
            _gl.BindTexture(GLEnum.Texture2D, handle);
            SetDefaultSamplerParams();

            unsafe
            {
                fixed (byte* p = pixels)
                    _gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba,
                                   (uint)width, (uint)height, 0,
                                   GLEnum.Rgba, GLEnum.UnsignedByte, p);
            }

            _gl.BindTexture(GLEnum.Texture2D, 0);
            return new TextureHandle(handle);
        }

        public TextureHandle CreateRgba(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return TextureHandle.Invalid;

            uint handle = _gl.GenTexture();
            _gl.BindTexture(GLEnum.Texture2D, handle);
            SetDefaultSamplerParams();

            unsafe
            {
                _gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba,
                               (uint)width, (uint)height, 0,
                               GLEnum.Rgba, GLEnum.UnsignedByte, (void*)null);
            }

            _gl.BindTexture(GLEnum.Texture2D, 0);
            return new TextureHandle(handle);
        }

        public void UpdateRegion(TextureHandle texture, int x, int y, int w, int h,
                                 ReadOnlySpan<byte> pixels)
        {
            if (!texture.IsValid || w <= 0 || h <= 0) return;

            _gl.BindTexture(GLEnum.Texture2D, texture.GlHandle);
            unsafe
            {
                fixed (byte* p = pixels)
                    _gl.TexSubImage2D(GLEnum.Texture2D, 0,
                                      x, y, (uint)w, (uint)h,
                                      GLEnum.Rgba, GLEnum.UnsignedByte, p);
            }
            _gl.BindTexture(GLEnum.Texture2D, 0);
        }

        public void Dispose(TextureHandle texture)
        {
            if (!texture.IsValid) return;
            uint handle = texture.GlHandle;
            _gl.DeleteTexture(handle);
        }

        private void SetDefaultSamplerParams()
        {
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS,     (int)GLEnum.ClampToEdge);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT,     (int)GLEnum.ClampToEdge);
        }
    }
}
