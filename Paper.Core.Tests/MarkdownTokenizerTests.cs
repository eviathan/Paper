using Paper.Core.Markdown;
using Xunit;

namespace Paper.Core.Tests
{
    public class MarkdownTokenizerTests
    {
        private static List<MarkdownToken> Tok(string src)
            => MarkdownTokenizer.Tokenize(src).ToList();

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void AssertSequence(List<MarkdownToken> tokens, params (MarkdownTokenType type, string text)[] expected)
        {
            Assert.Equal(expected.Length, tokens.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].type, tokens[i].Type);
                Assert.Equal(expected[i].text, tokens[i].Text);
            }
        }

        private static void AssertOffsets(string src, List<MarkdownToken> tokens)
        {
            // Every token's Text must equal source[Start..End]
            foreach (var t in tokens)
                Assert.Equal(t.Text, src[t.Start..t.End]);
        }

        // ── Basics ────────────────────────────────────────────────────────────

        [Fact]
        public void Empty_source_returns_no_tokens()
        {
            Assert.Empty(Tok(""));
        }

        [Fact]
        public void Plain_text_line_is_one_text_token()
        {
            var tokens = Tok("hello world");
            AssertSequence(tokens, (MarkdownTokenType.Text, "hello world"));
            AssertOffsets("hello world", tokens);
        }

        // ── Headings ─────────────────────────────────────────────────────────

        [Fact]
        public void H1_produces_marker_and_heading_text()
        {
            var src = "# Hello";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.HeadingMarker, "# "),
                (MarkdownTokenType.HeadingText,   "Hello"));
            AssertOffsets(src, tokens);
        }

        [Fact]
        public void H3_produces_marker_and_heading_text()
        {
            var src = "### Section";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.HeadingMarker, "### "),
                (MarkdownTokenType.HeadingText,   "Section"));
            AssertOffsets(src, tokens);
        }

        [Fact]
        public void Hash_without_space_is_plain_text()
        {
            var tokens = Tok("#nospace");
            AssertSequence(tokens, (MarkdownTokenType.Text, "#nospace"));
        }

        // ── Bold / Italic ─────────────────────────────────────────────────────

        [Fact]
        public void Bold_produces_delimiters_and_bold_content()
        {
            var src = "**bold**";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.Delimiter, "**"),
                (MarkdownTokenType.Bold,      "bold"),
                (MarkdownTokenType.Delimiter, "**"));
            AssertOffsets(src, tokens);
        }

        [Fact]
        public void Italic_produces_delimiters_and_italic_content()
        {
            var src = "*em*";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.Delimiter, "*"),
                (MarkdownTokenType.Italic,    "em"),
                (MarkdownTokenType.Delimiter, "*"));
            AssertOffsets(src, tokens);
        }

        [Fact]
        public void BoldItalic_produces_delimiters_and_bolditalic_content()
        {
            var src = "***both***";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.Delimiter,   "***"),
                (MarkdownTokenType.BoldItalic,  "both"),
                (MarkdownTokenType.Delimiter,   "***"));
            AssertOffsets(src, tokens);
        }

        [Fact]
        public void Inline_emphasis_surrounded_by_text()
        {
            var src = "hello **world** end";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.Text,      "hello "),
                (MarkdownTokenType.Delimiter, "**"),
                (MarkdownTokenType.Bold,      "world"),
                (MarkdownTokenType.Delimiter, "**"),
                (MarkdownTokenType.Text,      " end"));
            AssertOffsets(src, tokens);
        }

        [Fact]
        public void Unmatched_delimiter_is_plain_text()
        {
            var src = "hello **world";
            var tokens = Tok(src);
            AssertSequence(tokens, (MarkdownTokenType.Text, "hello **world"));
            AssertOffsets(src, tokens);
        }

        // ── Inline code ───────────────────────────────────────────────────────

        [Fact]
        public void Inline_code_produces_delimiters_and_code_content()
        {
            var src = "`code`";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.Delimiter,  "`"),
                (MarkdownTokenType.InlineCode, "code"),
                (MarkdownTokenType.Delimiter,  "`"));
            AssertOffsets(src, tokens);
        }

        [Fact]
        public void Inline_code_suppresses_bold_inside()
        {
            var src = "`**not bold**`";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.Delimiter,  "`"),
                (MarkdownTokenType.InlineCode, "**not bold**"),
                (MarkdownTokenType.Delimiter,  "`"));
            AssertOffsets(src, tokens);
        }

        // ── Block elements ────────────────────────────────────────────────────

        [Fact]
        public void Blockquote_produces_marker_then_inline()
        {
            var src = "> hello";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.BlockquoteMarker, "> "),
                (MarkdownTokenType.Text,             "hello"));
            AssertOffsets(src, tokens);
        }

        [Fact]
        public void Unordered_list_marker()
        {
            var src = "- item";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.ListMarker, "- "),
                (MarkdownTokenType.Text,       "item"));
            AssertOffsets(src, tokens);
        }

        [Fact]
        public void Ordered_list_marker()
        {
            var src = "1. item";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.ListMarker, "1. "),
                (MarkdownTokenType.Text,       "item"));
            AssertOffsets(src, tokens);
        }

        [Fact]
        public void Horizontal_rule()
        {
            var tokens = Tok("---");
            AssertSequence(tokens, (MarkdownTokenType.HrMarker, "---"));
        }

        // ── Code fence ────────────────────────────────────────────────────────

        [Fact]
        public void Code_fence_content_is_not_parsed_for_inline()
        {
            var src = "```\n**not bold**\n```";
            var tokens = Tok(src);
            AssertSequence(tokens,
                (MarkdownTokenType.CodeFenceMarker,  "```"),
                (MarkdownTokenType.CodeFenceContent, "**not bold**"),
                (MarkdownTokenType.CodeFenceMarker,  "```"));
            AssertOffsets(src, tokens);
        }

        // ── Multi-line ────────────────────────────────────────────────────────

        [Fact]
        public void Multi_line_offsets_are_correct()
        {
            var src = "line1\nline2";
            var tokens = Tok(src);
            Assert.Equal(2, tokens.Count);
            Assert.Equal(0, tokens[0].Start);
            Assert.Equal(5, tokens[0].End);
            Assert.Equal(6, tokens[1].Start);  // after the \n
            Assert.Equal(11, tokens[1].End);
            AssertOffsets(src, tokens);
        }

        [Fact]
        public void Heading_on_second_line_has_correct_offset()
        {
            var src = "intro\n## Title";
            var tokens = Tok(src);
            Assert.Equal(MarkdownTokenType.Text,          tokens[0].Type);
            Assert.Equal(MarkdownTokenType.HeadingMarker, tokens[1].Type);
            Assert.Equal(MarkdownTokenType.HeadingText,   tokens[2].Type);
            Assert.Equal(6, tokens[1].Start);   // "## " starts at offset 6
            AssertOffsets(src, tokens);
        }

        // ── Coverage: every source char accounted for ─────────────────────────

        [Fact]
        public void All_chars_covered_no_gaps_no_overlaps()
        {
            var src = "# Title\n\nSome **bold** and *italic* with `code`.\n\n- list\n\n---\n";
            var tokens = MarkdownTokenizer.Tokenize(src).ToList();

            // Check offsets match source
            AssertOffsets(src, tokens);

            // Check tokens cover all non-newline chars without overlap
            // (newlines are not tokenised as they are the line separators)
            var covered = new bool[src.Length];
            foreach (var t in tokens)
                for (int i = t.Start; i < t.End; i++)
                {
                    Assert.False(covered[i], $"Char {i} covered twice");
                    covered[i] = true;
                }

            for (int i = 0; i < src.Length; i++)
                if (src[i] != '\n')
                    Assert.True(covered[i], $"Char {i} ('{src[i]}') not covered by any token");
        }
    }
}
