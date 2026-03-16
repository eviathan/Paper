using Paper.Core.Styles;

namespace Paper.Core.Markdown
{
    /// <summary>
    /// Colour scheme used when rendering a MarkdownEditor in source mode.
    /// </summary>
    public sealed class MarkdownTheme
    {
        public PaperColour ProseColor          { get; init; } = new(0.88f, 0.88f, 0.94f, 1f);  // #e0e0f0
        public PaperColour DelimiterColor      { get; init; } = new(0.38f, 0.38f, 0.45f, 1f);  // dim #606073
        public PaperColour HeadingColor        { get; init; } = new(0.96f, 0.84f, 0.38f, 1f);  // #f5d660 warm yellow
        public PaperColour HeadingMarkerColor  { get; init; } = new(0.55f, 0.50f, 0.22f, 1f);  // dim gold
        public PaperColour InlineCodeColor     { get; init; } = new(0.31f, 0.89f, 0.70f, 1f);  // #50e3b2 teal
        public PaperColour CodeFenceColor      { get; init; } = new(0.31f, 0.89f, 0.70f, 1f);
        public PaperColour BlockquoteColor     { get; init; } = new(0.38f, 0.50f, 0.75f, 1f);  // #6080bf
        public PaperColour ListMarkerColor     { get; init; } = new(0.55f, 0.55f, 0.75f, 1f);  // #8c8cbf
        public PaperColour HrColor             { get; init; } = new(0.28f, 0.28f, 0.35f, 1f);  // dim

        /// <summary>Default dark theme matching the Paper playground.</summary>
        public static readonly MarkdownTheme Dark = new();
    }
}
