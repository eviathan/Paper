using SkiaSharp;

namespace Paper.Icons;

/// <summary>
/// Rasterizes a react-icons <see cref="IconData"/> to an RGBA <see cref="SKBitmap"/> at any size and color.
/// </summary>
public static class IconRasterizer
{
    /// <summary>
    /// A handful of exact (whitespace-stripped, lowercased) <c>d</c> strings for invisible-placeholder
    /// variants that <see cref="IsFullCanvasRect"/>'s geometric check can't recognize — a stray
    /// sub-pixel offset (".01" instead of "0") that trips up Skia's strict <c>SKPath.IsRect</c>, and a
    /// path that redundantly repeats the same rect contour three times (so its bounds are still the
    /// full canvas, but it isn't a single-contour rect any more). Both confirmed present in the
    /// bundled Material Design data. Everything else — including arbitrary starting corner, winding
    /// direction, or command casing — is caught generically by <see cref="IsFullCanvasRect"/> instead
    /// of needing another entry here.
    /// </summary>
    private static readonly HashSet<string> InvisiblePlaceholderPathOddballs = new()
    {
        "m.010h24v24h-24v0z",
        "m240h0v24h24v0zm00h0v24h24v0zm024h24v0h0v24z",
    };

    /// <summary>
    /// True if <paramref name="d"/> is an invisible full-canvas bounding-box filler path (authored as
    /// <c>fill="none"</c> in the source SVG — Material Design's SVGs universally ship one alongside
    /// the real glyph) rather than actual icon geometry. IconData has no per-path visibility flag (the
    /// "none" fill only survives when it lands in a colored icon's per-path F[] array — see the
    /// "fc"-set check at the call site), so for uncolored icons the only way to tell these apart from
    /// real content is recognizing the shape itself: a path whose geometry is exactly the icon's own
    /// viewBox rectangle can only ever be this filler, never a real glyph. Only called when there's at
    /// least one other path in the icon — confirmed no icon in the bundled sets has one of these
    /// patterns as its *only* path, so this never blanks out a legitimately single-path icon.
    /// </summary>
    private static bool IsInvisiblePlaceholder(string d, SKPath path, float vw, float vh)
    {
        if (InvisiblePlaceholderPathOddballs.Contains(d.Replace(" ", "").ToLowerInvariant()))
            return true;

        if (!path.IsRect) return false;
        const float eps = 0.5f; // absorbs the odd sub-pixel authoring noise (e.g. ".01" vs "0")
        var r = path.GetRect();
        return MathF.Abs(r.Left) <= eps && MathF.Abs(r.Top) <= eps &&
               MathF.Abs(r.Right - vw) <= eps && MathF.Abs(r.Bottom - vh) <= eps;
    }

    /// <summary>
    /// Renders <paramref name="icon"/> at <paramref name="sizePx"/> × <paramref name="sizePx"/> pixels.
    /// For tintable icons (no per-path fills), every path is filled with <paramref name="color"/>.
    /// For colored icons, per-path fills are used and <paramref name="color"/> is ignored.
    /// Returns null if no paths could be parsed.
    /// </summary>
    public static SKBitmap? Rasterize(IconData icon, int sizePx, SKColor color)
    {
        if (sizePx <= 0 || icon.P.Length == 0) return null;

        float vw = icon.W > 0 ? icon.W : 24f;
        float vh = icon.H > 0 ? icon.H : 24f;

        // Unpremul, not Premul: IconTextureCache uploads these bytes to the GPU as-is, and the
        // renderer composites every icon with a straight-alpha blend (SrcAlpha, OneMinusSrcAlpha —
        // see TexturedQuadRenderer.DrawWithUVBlended). Premultiplied storage double-applies alpha
        // at every anti-aliased edge pixel under that blend mode, making every icon's edges render
        // measurably too dark. Unpremul matches what the rest of the pipeline (SpriteTextureCache,
        // ImageTextureLoader) already assumes.
        var bitmap  = new SKBitmap(sizePx, sizePx, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        float sx = sizePx / vw;
        float sy = sizePx / vh;
        var scale = SKMatrix.CreateScale(sx, sy);

        bool isColored = icon.IsColored;

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        bool anyDrawn = false;
        for (int i = 0; i < icon.P.Length; i++)
        {
            if (string.IsNullOrEmpty(icon.P[i])) continue;

            // "fc"-set style: an explicit fill="none" survived into F[] for this path — it's masking
            // geometry (e.g. a cutout), not a shape to paint. Skip it rather than parsing "none" as a
            // color, which used to silently fall back to the tint color and paint it solid.
            if (isColored && icon.F != null && i < icon.F.Length &&
                icon.F[i] is { } noneCheck && noneCheck.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
                continue;

            using var path = SKPath.ParseSvgPathData(icon.P[i]);
            if (path == null) continue;

            // Material Design (and a few Tabler) icons style: an invisible full-canvas bounding path
            // with no F[] entry at all to flag it (uncolored icon, no per-path fill data survives).
            // Only recognized when there's at least one other path — see IsInvisiblePlaceholder's doc.
            if (icon.P.Length > 1 && IsInvisiblePlaceholder(icon.P[i], path, vw, vh))
                continue;

            path.Transform(scale);

            if (isColored && icon.F != null && i < icon.F.Length && icon.F[i] is { } fillStr)
                paint.Color = ParseCssColor(fillStr, color);
            else
                paint.Color = color;

            canvas.DrawPath(path, paint);
            anyDrawn = true;
        }

        if (!anyDrawn) { bitmap.Dispose(); return null; }
        return bitmap;
    }

    private static SKColor ParseCssColor(string css, SKColor fallback)
    {
        try
        {
            if (css.StartsWith('#'))
                return SKColor.Parse(css);

            if (css.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                int paren = css.IndexOf('(');
                int close = css.IndexOf(')');
                if (paren < 0 || close < 0) return fallback;
                var parts = css[(paren + 1)..close].Split(',');
                if (parts.Length < 3) return fallback;
                byte r = (byte)ParseColorComponent(parts[0].Trim());
                byte g = (byte)ParseColorComponent(parts[1].Trim());
                byte b = (byte)ParseColorComponent(parts[2].Trim());
                byte a = parts.Length >= 4 ? (byte)(float.Parse(parts[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) * 255) : (byte)255;
                return new SKColor(r, g, b, a);
            }

            return SKColor.Parse(css);
        }
        catch { return fallback; }
    }

    private static int ParseColorComponent(string s) =>
        s.EndsWith('%') ? (int)(float.Parse(s[..^1], System.Globalization.CultureInfo.InvariantCulture) / 100f * 255f)
                        : int.Parse(s);
}
