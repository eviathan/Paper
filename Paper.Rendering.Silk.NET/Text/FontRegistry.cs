using Paper.Core.Styles;

namespace Paper.Rendering.Silk.NET.Text
{
    /// <summary>
    /// Manages multiple font families (regular + bold variants) and provides the right
    /// <see cref="TextBatch"/> for any (family, weight, size) combination.
    /// Falls back to the default family when a requested family is not registered.
    /// </summary>
    internal sealed class FontRegistry : IDisposable
    {
        private readonly record struct FontVariants(PaperFontSet Regular, PaperFontSet? Bold, PaperFontSet? Italic, PaperFontSet? BoldItalic);

        private readonly Dictionary<string, FontVariants> _families = new(StringComparer.OrdinalIgnoreCase);
        private FontVariants _default;
        private bool _hasDefault;

        /// <summary>The default 16px batch — used for caret/selection math that has no per-element style.</summary>
        public TextBatch? Default => _hasDefault ? _default.Regular.Default : null;

        /// <summary>
        /// Register a font family. The first family registered becomes the default fallback.
        /// All variants are optional; when null the regular set is used as fallback.
        /// </summary>
        public void Register(string name, PaperFontSet regular, PaperFontSet? bold = null,
                             PaperFontSet? italic = null, PaperFontSet? boldItalic = null)
        {
            var variants = new FontVariants(regular, bold, italic, boldItalic);
            _families[name] = variants;
            if (!_hasDefault)
            {
                _default    = variants;
                _hasDefault = true;
            }
        }

        // ── Backward-compatible single-family API (uses default family) ────────

        public (TextBatch batch, float scale) Get(float targetPx)
            => ResolveSet(null, null, null).Get(targetPx);

        public float MeasureWidth(ReadOnlySpan<char> text, float targetPx)
            => ResolveSet(null, null, null).MeasureWidth(text, targetPx);

        public float LineHeight(float targetPx)
            => ResolveSet(null, null, null).LineHeight(targetPx);

        // ── Extended API with family + weight ─────────────────────────────────

        public (TextBatch batch, float scale) Get(float targetPx, string? family, FontWeight? weight, Paper.Core.Styles.FontStyle? fontStyle = null)
            => ResolveSet(family, weight, fontStyle).Get(targetPx);

        public float MeasureWidth(ReadOnlySpan<char> text, float targetPx, string? family, FontWeight? weight, Paper.Core.Styles.FontStyle? fontStyle = null)
            => ResolveSet(family, weight, fontStyle).MeasureWidth(text, targetPx);

        public float LineHeight(float targetPx, string? family, FontWeight? weight, Paper.Core.Styles.FontStyle? fontStyle = null)
            => ResolveSet(family, weight, fontStyle).LineHeight(targetPx);

        // ── Internal resolution ───────────────────────────────────────────────

        private PaperFontSet ResolveSet(string? family, FontWeight? weight, Paper.Core.Styles.FontStyle? fontStyle)
        {
            if (!_hasDefault)
                throw new InvalidOperationException("No fonts registered in FontRegistry.");

            var variants = _default;
            if (family != null && _families.TryGetValue(family, out var fv))
                variants = fv;

            bool bold   = weight.HasValue && (int)weight.Value >= (int)FontWeight.SemiBold;
            bool italic = fontStyle == Paper.Core.Styles.FontStyle.Italic;

            if (bold && italic && variants.BoldItalic != null) return variants.BoldItalic;
            if (bold  && variants.Bold   != null) return variants.Bold;
            if (italic && variants.Italic != null) return variants.Italic;
            return variants.Regular;
        }

        public void Flush(float screenW, float screenH)
        {
            var seen = new HashSet<PaperFontSet>();
            foreach (var (_, v) in _families)
            {
                if (seen.Add(v.Regular))    v.Regular.Flush(screenW, screenH);
                if (v.Bold       != null && seen.Add(v.Bold))       v.Bold.Flush(screenW, screenH);
                if (v.Italic     != null && seen.Add(v.Italic))     v.Italic.Flush(screenW, screenH);
                if (v.BoldItalic != null && seen.Add(v.BoldItalic)) v.BoldItalic.Flush(screenW, screenH);
            }
        }

        public void Dispose()
        {
            var seen = new HashSet<PaperFontSet>();
            foreach (var (_, v) in _families)
            {
                if (seen.Add(v.Regular))    v.Regular.Dispose();
                if (v.Bold       != null && seen.Add(v.Bold))       v.Bold.Dispose();
                if (v.Italic     != null && seen.Add(v.Italic))     v.Italic.Dispose();
                if (v.BoldItalic != null && seen.Add(v.BoldItalic)) v.BoldItalic.Dispose();
            }
        }
    }
}
