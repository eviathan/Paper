using SkiaSharp;

namespace Paper.Icons;

/// <summary>
/// Rasterizes a react-icons <see cref="IconData"/> to an RGBA <see cref="SKBitmap"/> at any size and color.
/// </summary>
public static class IconRasterizer
{
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

        var bitmap  = new SKBitmap(sizePx, sizePx, SKColorType.Rgba8888, SKAlphaType.Premul);
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

            using var path = SKPath.ParseSvgPathData(icon.P[i]);
            if (path == null) continue;

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
