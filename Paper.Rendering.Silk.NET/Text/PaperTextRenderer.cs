using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET.Text
{
    /// <summary>
    /// Paper's text rendering on its own, for a host drawing text outside a Paper UI tree — a game
    /// engine's labels — with the same font atlases, glyph placement and shader as Paper's UI text.
    /// <para>
    /// Fonts are TrueType files, keyed by path, each baked at Paper's standard pixel sizes the first
    /// time it's used; text at any other size uses the nearest bake, scaled. Coordinates are pixels
    /// of the target (the bound framebuffer's viewport), origin top-left. Queue text with
    /// <see cref="Draw"/>, then <see cref="Flush"/> with the target's size — before drawing anything
    /// that should sit on top of it.
    /// </para>
    /// </summary>
    public sealed class PaperTextRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly Dictionary<string, PaperFontSet?> _fonts = new(StringComparer.OrdinalIgnoreCase);

        public PaperTextRenderer(GL gl) => _gl = gl ?? throw new ArgumentNullException(nameof(gl));

        /// <summary>Whether <paramref name="fontPath"/> can be drawn with — loading it on first ask.
        /// A missing or unreadable file is remembered as unusable rather than retried every frame.</summary>
        public bool HasFont(string fontPath) => Font(fontPath) is not null;

        /// <summary>Width in pixels of <paramref name="text"/> at <paramref name="sizePx"/>.</summary>
        public float MeasureWidth(string fontPath, ReadOnlySpan<char> text, float sizePx) =>
            Font(fontPath)?.MeasureWidth(text, sizePx) ?? 0f;

        /// <summary>Line box height in pixels at <paramref name="sizePx"/>.</summary>
        public float LineHeight(string fontPath, float sizePx) => Font(fontPath)?.LineHeight(sizePx) ?? sizePx;

        /// <summary>From the top of the line box down to the baseline, in pixels.</summary>
        public float Ascender(string fontPath, float sizePx) => Font(fontPath)?.Ascender(sizePx) ?? sizePx * 0.8f;

        /// <summary>Queues one line of <paramref name="text"/> with its line box's top-left at
        /// (<paramref name="x"/>, <paramref name="top"/>).</summary>
        public void Draw(string fontPath, ReadOnlySpan<char> text, float x, float top, float sizePx,
                         float r, float g, float b, float a)
        {
            if (Font(fontPath) is not { } font || sizePx <= 0)
                return;
            var (batch, scale) = font.Get(sizePx);
            batch.Add(text, x, top + font.Ascender(sizePx), r, g, b, a, scale);
        }

        /// <summary>Draws everything queued onto a <paramref name="width"/> x <paramref name="height"/>
        /// pixel target. Leaves blending as it found it — the caller enables alpha blending.</summary>
        public void Flush(float width, float height)
        {
            foreach (var font in _fonts.Values)
                font?.Flush(width, height);
        }

        public void Dispose()
        {
            foreach (var font in _fonts.Values)
                font?.Dispose();
            _fonts.Clear();
        }

        private PaperFontSet? Font(string fontPath)
        {
            if (_fonts.TryGetValue(fontPath, out var font))
                return font;
            try
            {
                font = File.Exists(fontPath) ? new PaperFontSet(PaperFontLoader.LoadSet(_gl, fontPath), _gl) : null;
            }
            catch (Exception)
            {
                font = null;
            }
            _fonts[fontPath] = font;
            return font;
        }
    }
}
