using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET.Text
{
    /// <summary>
    /// Manages a family of <see cref="TextBatch"/> instances baked at different pixel sizes.
    /// Provides the best-matching batch for any requested font size plus a correction scale.
    /// </summary>
    internal sealed class PaperFontSet : IDisposable
    {
        // Sorted ascending by size for binary search.
        private readonly (int size, TextBatch batch)[] _batches;

        /// <summary>The batch closest to 16px — used as the default for cursor/caret math.</summary>
        public TextBatch Default { get; }

        public PaperFontSet(Dictionary<int, PaperFontAtlas> atlases, GL gl)
        {
            _batches = atlases
                .OrderBy(kv => kv.Key)
                .Select(kv => (kv.Key, new TextBatch(gl, kv.Value)))
                .ToArray();

            // Pick default as the batch with size nearest to 16
            Default = _batches.OrderBy(b => Math.Abs(b.size - 16)).First().batch;
        }

        /// <summary>
        /// Returns the <see cref="TextBatch"/> whose baked size is nearest to
        /// <paramref name="targetPx"/>, plus a correction <paramref name="scale"/>
        /// (usually close to 1.0) to apply to glyph dimensions and advance widths.
        /// </summary>
        public (TextBatch batch, float scale) Get(float targetPx)
        {
            if (_batches.Length == 0) return (Default, 1f);

            var best = _batches[0];
            float bestDist = Math.Abs(best.size - targetPx);
            for (int i = 1; i < _batches.Length; i++)
            {
                float dist = Math.Abs(_batches[i].size - targetPx);
                if (dist < bestDist) { bestDist = dist; best = _batches[i]; }
            }

            float scale = best.size > 0 ? targetPx / best.size : 1f;
            return (best.batch, scale);
        }

        /// <summary>Measures pixel width of <paramref name="text"/> at <paramref name="targetPx"/>.</summary>
        public float MeasureWidth(ReadOnlySpan<char> text, float targetPx)
        {
            var (batch, scale) = Get(targetPx);
            return batch.MeasureWidth(text, scale);
        }

        /// <summary>Line height in pixels for <paramref name="targetPx"/>.</summary>
        public float LineHeight(float targetPx)
        {
            var (batch, scale) = Get(targetPx);
            var atlas = batch.Atlas;
            return (atlas.LineHeight > 0 ? atlas.LineHeight : atlas.BaseSize) * scale;
        }

        public void Flush(float screenW, float screenH)
        {
            foreach (var (_, batch) in _batches)
                batch.Flush(screenW, screenH);
        }

        public void Dispose()
        {
            foreach (var (_, batch) in _batches)
                batch.Dispose();
        }
    }
}
