using System.Collections.Concurrent;
using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// Result of loading an image: texture handle and pixel dimensions for aspect ratio and object-fit.
    /// </summary>
    public readonly struct ImageTextureResult
    {
        public uint Handle { get; }
        public int Width { get; }
        public int Height { get; }

        public ImageTextureResult(uint handle, int width, int height)
        {
            Handle = handle;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Loads images from file paths (e.g. PNG) into OpenGL textures and caches them by path.
    /// Exposes dimensions for layout (aspect ratio) and rendering (object-fit).
    /// </summary>
    public sealed class ImageTextureLoader
    {
        private readonly GL _gl;
        private readonly ConcurrentDictionary<string, ImageTextureResult> _cache = new();

        public ImageTextureLoader(GL gl)
        {
            _gl = gl;
        }

        /// <summary>
        /// Returns texture handle and dimensions for the image at <paramref name="path"/> (cached).
        /// Handle is 0 if the file cannot be loaded or path is null/empty.
        /// </summary>
        public ImageTextureResult GetOrLoad(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return default;

            string key = Path.GetFullPath(path);
            if (_cache.TryGetValue(key, out var result))
                return result;

            var loaded = LoadFromFile(key);
            if (loaded.Handle != 0)
                _cache[key] = loaded;
            return loaded;
        }

        /// <summary>
        /// Returns only the texture handle (for callers that don't need dimensions).
        /// </summary>
        public uint GetHandle(string? path) => GetOrLoad(path).Handle;

        /// <summary>
        /// Returns (width, height) in pixels for the image at path, or null if not loaded.
        /// Used by layout to infer the missing dimension from aspect ratio.
        /// </summary>
        public (int w, int h)? GetDimensions(string? path)
        {
            var r = GetOrLoad(path);
            if (r.Handle == 0 || r.Width <= 0 || r.Height <= 0) return null;
            return (r.Width, r.Height);
        }

        private unsafe ImageTextureResult LoadFromFile(string path)
        {
            if (!File.Exists(path)) return default;

            try
            {
                using var stream = File.OpenRead(path);
                var image = StbImageSharp.ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                if (image == null || image.Data == null || image.Data.Length == 0) return default;

                uint handle = _gl.GenTexture();
                _gl.BindTexture(TextureTarget.Texture2D, handle);

                fixed (byte* ptr = image.Data)
                {
                    _gl.TexImage2D(TextureTarget.Texture2D, 0,
                        InternalFormat.Rgba,
                        (uint)image.Width, (uint)image.Height, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                }

                _gl.TexParameter(TextureTarget.Texture2D,
                    TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                _gl.TexParameter(TextureTarget.Texture2D,
                    TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                _gl.TexParameter(TextureTarget.Texture2D,
                    TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                _gl.TexParameter(TextureTarget.Texture2D,
                    TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                _gl.BindTexture(TextureTarget.Texture2D, 0);
                return new ImageTextureResult(handle, image.Width, image.Height);
            }
            catch
            {
                return default;
            }
        }
    }
}
