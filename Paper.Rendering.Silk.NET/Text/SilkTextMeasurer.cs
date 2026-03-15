using Paper.Core.Styles;
using Paper.Layout;

namespace Paper.Rendering.Silk.NET.Text
{
    internal sealed class SilkTextMeasurer : ILayoutMeasurer
    {
        private readonly FontRegistry _fonts;

        private const float DefaultFontPx           = 16f;
        private const float ApproxCharWidthFactor   = 0.6f;
        private const float DefaultLineHeightFactor = 1.2f;

        public SilkTextMeasurer(FontRegistry fonts) => _fonts = fonts;

        public (float width, float height) MeasureText(string text, StyleSheet style, float? maxWidth = null)
        {
            if (string.IsNullOrEmpty(text))
                return (0f, 0f);

            float fontPx  = ResolveFontPx(style);
            string? fam   = style.FontFamily;
            var weight    = style.FontWeight;

            float w       = _fonts.MeasureWidth(text.AsSpan(), fontPx, fam, weight);
            float lineHPx = _fonts.LineHeight(fontPx, fam, weight);
            float lineH   = lineHPx * Math.Max(0.5f, style.LineHeight ?? 1.4f);

            // Heuristic fallback if atlas not ready
            if (w <= 0 || lineH <= 0)
            {
                w     = text.Length * fontPx * ApproxCharWidthFactor;
                lineH = fontPx * (style.LineHeight is > 0 ? style.LineHeight.Value : DefaultLineHeightFactor);
            }

            // Word wrap
            bool doWrap = style.WhiteSpace == WhiteSpace.Normal && maxWidth is > 0 && w > maxWidth.Value;
            if (doWrap)
            {
                float spaceW = _fonts.MeasureWidth(" ".AsSpan(), fontPx, fam, weight);
                if (spaceW <= 0) spaceW = fontPx * 0.3f;
                int numLines = 1;
                float lineW  = 0;
                foreach (var word in text.Split(' '))
                {
                    float wordW = _fonts.MeasureWidth(word.AsSpan(), fontPx, fam, weight);
                    if (lineW > 0 && lineW + spaceW + wordW > maxWidth!.Value)
                    {
                        numLines++;
                        lineW = wordW;
                    }
                    else
                    {
                        lineW += (lineW > 0 ? spaceW : 0) + wordW;
                    }
                }
                return (maxWidth!.Value, lineH * numLines);
            }

            if (maxWidth is > 0 && w > maxWidth.Value)
                w = maxWidth.Value;

            return (w, lineH);
        }

        internal static float ResolveFontPx(StyleSheet style, float defaultPx = DefaultFontPx)
        {
            if (style.FontSize is { } fs && !fs.IsAuto)
            {
                float resolved = fs.Resolve(defaultPx);
                if (resolved > 0) return resolved;
            }
            return defaultPx;
        }
    }
}
