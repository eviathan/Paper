using Silk.NET.OpenGL;
using SkiaSharp;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// Renders one frame of a bitmap sprite sheet at the exact physical pixel size it's drawn at,
    /// caching the result by (sheet, frame, size) — the same "rasterize on demand, cache per size"
    /// approach <see cref="Paper.Icons.IconTextureCache"/> uses for vector icons, applied to a bitmap
    /// source. Without this, a sprite frame is uploaded once at its native sheet resolution and then
    /// GPU-stretched to fit; that stretch is a fixed bilinear blur that reads as crisp on a Retina
    /// display (many physical pixels absorb it) and soft on a standard one (few physical pixels
    /// magnify it). Cropping and resampling fresh at the exact requested size removes the stretch
    /// step, so the result is the same at any DPI.
    ///
    /// Bounded to <see cref="MaxCacheEntries"/> textures, evicted least-recently-used: sizePx tracks
    /// live physical layout pixels, so a continuously resized/animated sprite would otherwise mint a
    /// new GPU texture forever and never free any of them.
    /// </summary>
    public sealed class SpriteTextureCache : IDisposable
    {
        private const int MaxCacheEntries = 512;

        private readonly GL _gl;
        private readonly Dictionary<string, SKBitmap?> _sheets = new();
        private readonly LinkedList<(string Path, int FrameIndex, int Size)> _lru = new();
        private readonly Dictionary<(string Path, int FrameIndex, int Size),
            (uint Handle, LinkedListNode<(string Path, int FrameIndex, int Size)> Node)> _cache = new();
        private bool _disposed;

        public SpriteTextureCache(GL gl) => _gl = gl;

        /// <summary>
        /// Returns an OpenGL texture handle for one <paramref name="frameWidth"/>x<paramref name="frameHeight"/>
        /// cell of the sheet at <paramref name="path"/>, rasterized at <paramref name="sizePx"/>x<paramref name="sizePx"/>.
        /// Returns 0 if the sheet can't be loaded or the frame falls outside it.
        /// </summary>
        public uint GetTexture(string? path, int frameIndex, float frameWidth, float frameHeight, int sizePx)
        {
            if (_disposed || sizePx <= 0 || frameWidth <= 0 || frameHeight <= 0 || string.IsNullOrEmpty(path))
                return 0;

            string key = Path.GetFullPath(path);
            var cacheKey = (key, frameIndex, sizePx);
            if (_cache.TryGetValue(cacheKey, out var entry))
            {
                _lru.Remove(entry.Node);
                _lru.AddFirst(entry.Node);
                return entry.Handle;
            }

            var sheet = GetOrLoadSheet(key);
            if (sheet == null) return 0;

            int columns = Math.Max(1, (int)(sheet.Width / frameWidth));
            int col = frameIndex % columns;
            int row = frameIndex / columns;
            var srcRect = new SKRect(col * frameWidth, row * frameHeight, (col + 1) * frameWidth, (row + 1) * frameHeight);
            if (srcRect.Right > sheet.Width || srcRect.Bottom > sheet.Height) return 0;

            using var frame = RenderFrame(sheet, srcRect, sizePx);
            if (frame == null) return 0;

            uint handle = UploadToGpu(frame);
            var node = _lru.AddFirst(cacheKey);
            _cache[cacheKey] = (handle, node);
            EvictOverflow();
            return handle;
        }

        private void EvictOverflow()
        {
            while (_cache.Count > MaxCacheEntries && _lru.Last != null)
            {
                var lruKey = _lru.Last.Value;
                _lru.RemoveLast();
                if (_cache.Remove(lruKey, out var evicted) && evicted.Handle != 0)
                    _gl.DeleteTexture(evicted.Handle);
            }
        }

        private static SKBitmap RenderFrame(SKBitmap sheet, SKRect srcRect, int sizePx)
        {
            var info = new SKImageInfo(sizePx, sizePx, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var target = new SKBitmap(info);
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
            canvas.DrawBitmap(sheet, srcRect, new SKRect(0, 0, sizePx, sizePx), paint);
            return target;
        }

        private SKBitmap? GetOrLoadSheet(string path)
        {
            if (_sheets.TryGetValue(path, out var cached)) return cached;

            SKBitmap? bitmap = null;
            try
            {
                if (File.Exists(path))
                    bitmap = SKBitmap.Decode(path);
            }
            catch { bitmap = null; }

            _sheets[path] = bitmap;
            return bitmap;
        }

        private unsafe uint UploadToGpu(SKBitmap bitmap)
        {
            uint handle = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, handle);

            int w = bitmap.Width;
            int h = bitmap.Height;
            int stride = bitmap.RowBytes;

            // SkiaSharp stores rows top-to-bottom; OpenGL expects bottom-to-top — flip rows.
            var pixels = new byte[h * stride];
            fixed (byte* dst = pixels)
            {
                var src = (byte*)bitmap.GetPixels().ToPointer();
                for (int y = 0; y < h; y++)
                    System.Buffer.MemoryCopy(src + (long)(h - 1 - y) * stride, dst + (long)y * stride, stride, stride);
            }

            fixed (byte* ptr = pixels)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0,
                    InternalFormat.Rgba,
                    (uint)w, (uint)h, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }

            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,     (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,     (int)TextureWrapMode.ClampToEdge);
            _gl.BindTexture(TextureTarget.Texture2D, 0);

            return handle;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var entry in _cache.Values)
                if (entry.Handle != 0) _gl.DeleteTexture(entry.Handle);
            _cache.Clear();
            _lru.Clear();

            foreach (var bmp in _sheets.Values)
                bmp?.Dispose();
            _sheets.Clear();
        }
    }
}
