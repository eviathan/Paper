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

        /// <summary>
        /// Kept in sync with the owning renderer's own DpiScale (see Canvas.Rendering.cs /
        /// PaperEmbeddedSurface's per-frame DpiScale assignment). Layout runs in logical units and
        /// has no render context of its own, but the actual glyph rendering in
        /// FiberRenderer.Text.cs's DrawText selects its font atlas by fontPx * DpiScale — a
        /// *different* pre-baked atlas than a plain fontPx lookup would pick whenever DpiScale != 1
        /// (e.g. any non-1x display). Different-sized bitmap atlases baked from the same font don't
        /// have perfectly proportional per-glyph advances (integer-pixel rounding at each bake
        /// size), so measuring here without the same DPI-aware atlas selection systematically
        /// under-measures text width — invisibly for short strings, growing with length — which is
        /// exactly what let flex children (e.g. a right-aligned stat value) get allocated a layout
        /// box a few px too narrow for what DrawText, using the correct atlas, actually renders.
        /// </summary>
        public float DpiScale { get; set; } = 1f;

        public SilkTextMeasurer(FontRegistry fonts) => _fonts = fonts;

        /// <summary>Measures at the same (fontPx * DpiScale)-selected atlas DrawText renders with,
        /// then divides back down to logical units — mirrors DrawText's own MeasureLogical.</summary>
        private float MeasureWidthDpiAware(ReadOnlySpan<char> text, float fontPx, string? fam, FontWeight? weight, FontStyle? fontStyle)
        {
            float dpi = DpiScale > 0f ? DpiScale : 1f;
            return _fonts.MeasureWidth(text, fontPx * dpi, fam, weight, fontStyle) / dpi;
        }

        public (float width, float height) MeasureText(string text, StyleSheet style, float? maxWidth = null)
        {
            float fontPx   = ResolveFontPx(style);
            string? fam    = style.FontFamily;
            var weight     = style.FontWeight;
            var fontStyle  = style.FontStyle;

            // _fonts.LineHeight(fontPx, ...) is already an absolute line height in px (the font
            // atlas's own natural metric), not a bare font size — so an unset style.LineHeight
            // should use it as-is. It used to be multiplied by another ~1.4x default on top of
            // that (i.e. by a factor meant for a *font-size* multiplier — see the correct usage a
            // few lines below and in FlexLayout.cs's own text-height fallbacks), inflating every
            // text node's measured height ~40% past the font's real line height. That oversized
            // measurement was what threw off vertical centering everywhere a Text sat inside a
            // fixed-height flex container (e.g. Controls.Btn) — see FlexLayout.PositionLine's own
            // remarks for the second half of that bug. Only an *explicit* style.LineHeight still
            // multiplies fontPx, matching CSS's own numeric line-height convention.
            float lineHPx = _fonts.LineHeight(fontPx, fam, weight, fontStyle);
            float lineH = style.LineHeight is { } lh && lh > 0 ? fontPx * lh : lineHPx;
            if (lineH <= 0) lineH = fontPx * DefaultLineHeightFactor;

            if (string.IsNullOrEmpty(text))
                return (0f, lineH);

            bool doWrap = style.WhiteSpace == WhiteSpace.Normal && maxWidth is > 0;
            float spaceW = 0f;
            if (doWrap)
            {
                spaceW = MeasureWidthDpiAware(" ".AsSpan(), fontPx, fam, weight, fontStyle);
                if (spaceW <= 0) spaceW = fontPx * 0.3f;
            }

            // Split on hard newlines first, then word-wrap each logical line.
            var logicalLines = text.Split('\n');
            int totalVisualLines = 0;
            float maxLineW = 0f;

            foreach (var logLine in logicalLines)
            {
                if (logLine.Length == 0) { totalVisualLines++; continue; }

                float lineW = MeasureWidthDpiAware(logLine.AsSpan(), fontPx, fam, weight, fontStyle);
                if (lineW <= 0) lineW = logLine.Length * fontPx * ApproxCharWidthFactor;

                if (doWrap && lineW > maxWidth!.Value)
                {
                    int subLines = 1;
                    float curW = 0f;
                    foreach (var word in logLine.Split(' '))
                    {
                        float wordW = MeasureWidthDpiAware(word.AsSpan(), fontPx, fam, weight, fontStyle);
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
