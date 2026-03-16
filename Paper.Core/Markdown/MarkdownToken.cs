namespace Paper.Core.Markdown
{
    /// <summary>
    /// A typed span within a markdown source string.
    /// Start/End are character offsets into the original source — they never shift,
    /// so caret positions map 1:1 to plain-text indices.
    /// </summary>
    public readonly struct MarkdownToken
    {
        public MarkdownTokenType Type  { get; init; }
        public int               Start { get; init; }  // inclusive
        public int               End   { get; init; }  // exclusive
        public string            Text  { get; init; }  // source[Start..End]

        public int Length => End - Start;

        public override string ToString() => $"{Type}[{Start}..{End}] \"{Text}\"";
    }
}
