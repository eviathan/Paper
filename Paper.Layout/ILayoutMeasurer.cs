using Paper.Core.Styles;

namespace Paper.Layout
{
    /// <summary>
    /// Pluggable measurement for layout. Implementations can measure text and other intrinsic content.
    /// Layout uses this to resolve <c>auto</c> sizes more accurately than heuristics.
    /// </summary>
    public interface ILayoutMeasurer
    {
        /// <summary>
        /// Measure a single-line text run in pixels.
        /// </summary>
        /// <param name="text">Text to measure.</param>
        /// <param name="style">Resolved style for the node (font size, padding, etc.).</param>
        /// <param name="maxWidth">Optional max width constraint (for future wrapping).</param>
        (float width, float height) MeasureText(string text, StyleSheet style, float? maxWidth = null);
    }
}

