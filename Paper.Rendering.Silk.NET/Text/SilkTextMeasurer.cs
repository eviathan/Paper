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
            float fontPx   = ResolveFontPx(style);
            string? fam    = style.FontFamily;
            var weight     = style.FontWeight;
            var fontStyle  = style.FontStyle;

            float lineHPx = _fonts.LineHeight(fontPx, fam, weight, fontStyle);
            float lineH   = lineHPx * Math.Max(0.5f, style.LineHeight ?? 1.4f);
            if (lineH <= 0) lineH = fontPx * DefaultLineHeightFactor;

            if (string.IsNullOrEmpty(text))
                return (0f, lineH);

            bool doWrap = style.WhiteSpace == WhiteSpace.Normal && maxWidth is > 0;
            float spaceW = 0f;
            if (doWrap)
            {
                spaceW = _fonts.MeasureWidth(" ".AsSpan(), fontPx, fam, weight, fontStyle);
                if (spaceW <= 0) spaceW = fontPx * 0.3f;
            }

            // Split on hard newlines first, then word-wrap each logical line.
            var logicalLines = text.Split('\n');
            int totalVisualLines = 0;
            float maxLineW = 0f;

            foreach (var logLine in logicalLines)
            {
                if (logLine.Length == 0) { totalVisualLines++; continue; }

                float lineW = _fonts.MeasureWidth(logLine.AsSpan(), fontPx, fam, weight, fontStyle);
                if (lineW <= 0) lineW = logLine.Length * fontPx * ApproxCharWidthFactor;

                if (doWrap && lineW > maxWidth!.Value)
                {
                    int subLines = 1;
                    float curW = 0f;
                    foreach (var word in logLine.Split(' '))
                    {
                        float wordW = _fonts.MeasureWidth(word.AsSpan(), fontPx, fam, weight, fontStyle);
                        if (wordW <= 0) wordW = word.Length * fontPx * ApproxCharWidthFactor;
                        if (curW > 0 && curW + spaceW + wordW > maxWidth!.Value) { subLines++; curW = wordW; }
                        else curW += (curW > 0 ? spaceW : 0) + wordW;
                        maxLineW = Math.Max(maxLineW, Math.Min(curW, maxWidth!.Value));
                    }
                    totalVisualLines += subLines;
                }
                else
                {
                    totalVisualLines++;
                    maxLineW = Math.Max(maxLineW, lineW);
                }
            }

            if (maxWidth is > 0 && maxLineW > maxWidth.Value) maxLineW = maxWidth.Value;
            return (maxLineW, lineH * totalVisualLines);
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
