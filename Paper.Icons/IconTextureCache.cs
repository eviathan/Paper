using Paper.Core.VirtualDom;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace Paper.Icons;

/// <summary>
/// Rasterizes icons on demand and caches the resulting OpenGL textures.
/// Cache key: (set, name, sizePx, colorARGB).  Colored icons always use a fixed key.
/// </summary>
public sealed class IconTextureCache : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<(string Set, string Name, int Size, uint Color), uint> _cache = new();
    private bool _disposed;

    public IconTextureCache(GL gl) => _gl = gl;

    /// <summary>
    /// Returns an OpenGL texture handle for the icon, rasterizing it if not already cached.
    /// <paramref name="r"/>, <paramref name="g"/>, <paramref name="b"/>, <paramref name="a"/> are [0,1] float channels.
    /// Returns 0 if the icon is not found or rasterization fails.
    /// </summary>
    public uint GetTexture(IconRef iconRef, int sizePx, float r, float g, float b, float a)
    {
        if (_disposed || sizePx <= 0) return 0;

        var data = IconRegistry.Get(iconRef);
        if (data == null) return 0;

        uint colorKey = data.IsColored ? 0xFFFFFFFFu : PackColor(r, g, b, a);
        var cacheKey  = (iconRef.Set, iconRef.Name, sizePx, colorKey);

        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

        var skColor = data.IsColored
            ? SKColors.White
            : new SKColor((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), (byte)(a * 255f));

        using var bitmap = IconRasterizer.Rasterize(data, sizePx, skColor);
        if (bitmap == null) return 0;

        var handle = UploadToGpu(bitmap);
        _cache[cacheKey] = handle;
        return handle;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var handle in _cache.Values)
            if (handle != 0) _gl.DeleteTexture(handle);
        _cache.Clear();
    }

    private static uint PackColor(float r, float g, float b, float a) =>
        ((uint)(a * 255) << 24) | ((uint)(r * 255) << 16) | ((uint)(g * 255) << 8) | (uint)(b * 255);

    private unsafe uint UploadToGpu(SKBitmap bitmap)
    {
        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);

        fixed (byte* ptr = bitmap.Bytes)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0,
                InternalFormat.Rgba,
                (uint)bitmap.Width, (uint)bitmap.Height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,     (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,     (int)TextureWrapMode.ClampToEdge);
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        return handle;
    }
}
