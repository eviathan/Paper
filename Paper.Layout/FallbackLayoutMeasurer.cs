using Paper.Core.Styles;

namespace Paper.Layout
{
    /// <summary>
    /// Heuristic-based layout measurer used when no font-backed measurer is available
    /// (e.g. font file not loaded). Ensures buttons and text elements still get
    /// intrinsic sizing from text length and style.
    /// </summary>
    public sealed class FallbackLayoutMeasurer : ILayoutMeasurer
    {
        private const float DefaultFontPx = 16f;
        private const float ApproxCharWidthFactor = 0.6f;
        private const float DefaultLineHeightFactor = 1.2f;

        public (float width, float height) MeasureText(string text, StyleSheet style, float? maxWidth = null)
        {
            if (string.IsNullOrEmpty(text))
                return (0f, 0f);

            float fontPx = DefaultFontPx;
            if (style.FontSize is { } fs && !fs.IsAuto)
            {
                float resolved = fs.Resolve(0f, DefaultFontPx);
                if (resolved > 0) fontPx = resolved;
            }

            float lineHeight = (style.LineHeight ?? 0f) > 0 ? style.LineHeight!.Value : DefaultLineHeightFactor;
            float h = fontPx * lineHeight;

            float w = text.Length * fontPx * ApproxCharWidthFactor;
            if (maxWidth is > 0 && w > maxWidth.Value)
                w = maxWidth.Value;

            return (w, h);
        }
    }
}
