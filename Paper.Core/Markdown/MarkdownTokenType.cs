namespace Paper.Core.Markdown
{
    public enum MarkdownTokenType
    {
        // ── Prose ─────────────────────────────────────────────────────────────
        Text,            // plain prose content

        // ── Inline styled content ─────────────────────────────────────────────
        Bold,            // content between ** or __
        Italic,          // content between * or _
        BoldItalic,      // content between *** or ___
        InlineCode,      // content between backticks

        // ── Syntax delimiters (shown dim) ─────────────────────────────────────
        Delimiter,       // **, *, __, _, ***, ___, `, [ ], ( )

        // ── Block-level markers ───────────────────────────────────────────────
        HeadingMarker,   // leading # chars + space (e.g. "## ")
        HeadingText,     // text of the heading line
        BlockquoteMarker,// leading "> "
        ListMarker,      // leading "- ", "* ", "1. " etc.
        HrMarker,        // --- on its own line
        CodeFenceMarker, // opening/closing ```
        CodeFenceContent,// lines inside a code fence
    }
}
