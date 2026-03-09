using System.Text.RegularExpressions;

namespace Paper.Layout
{
    /// <summary>
    /// Parses CSS grid-template-columns / grid-template-rows strings into <see cref="GridTrack"/> lists.
    /// Supports: px, %, fr, auto, repeat(N, value).
    /// </summary>
    internal static class GridTemplateParser
    {
        private static readonly Regex TokenRe = new(@"repeat\s*\(\s*(\d+)\s*,\s*([^)]+)\s*\)|([^\s]+)", RegexOptions.Compiled);

        /// <summary>
        /// Parse a grid template string and return an ordered list of tracks.
        /// Returns a single "1fr" track on parse failure.
        /// </summary>
        public static List<GridTrack> Parse(string? template)
        {
            if (string.IsNullOrWhiteSpace(template))
                return new List<GridTrack> { new(GridTrackKind.Fr, 1) };

            var tracks = new List<GridTrack>();

            foreach (Match m in TokenRe.Matches(template))
            {
                if (m.Groups[1].Success)
                {
                    // repeat(N, value)
                    int   count     = int.Parse(m.Groups[1].Value);
                    string inner    = m.Groups[2].Value.Trim();
                    var   innerTrack = ParseSingle(inner);
                    if (innerTrack != null)
                        for (int i = 0; i < count; i++)
                            tracks.Add(innerTrack);
                }
                else if (m.Groups[3].Success)
                {
                    var track = ParseSingle(m.Groups[3].Value.Trim());
                    if (track != null) tracks.Add(track);
                }
            }

            return tracks.Count > 0 ? tracks : new List<GridTrack> { new(GridTrackKind.Fr, 1) };
        }

        private static GridTrack? ParseSingle(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            if (token.Equals("auto", StringComparison.OrdinalIgnoreCase))
                return new GridTrack(GridTrackKind.Auto, 0);

            if (token.EndsWith("fr", StringComparison.OrdinalIgnoreCase) &&
                float.TryParse(token[..^2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float fr))
                return new GridTrack(GridTrackKind.Fr, fr);

            if (token.EndsWith('%') &&
                float.TryParse(token[..^1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float pct))
                return new GridTrack(GridTrackKind.Percent, pct);

            if (token.EndsWith("px", StringComparison.OrdinalIgnoreCase) &&
                float.TryParse(token[..^2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float px))
                return new GridTrack(GridTrackKind.Px, px);

            // bare number → px
            if (float.TryParse(token, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float bare))
                return new GridTrack(GridTrackKind.Px, bare);

            return null;
        }
    }
}
