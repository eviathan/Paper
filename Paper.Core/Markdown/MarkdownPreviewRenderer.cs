using System;
using System.Collections.Generic;
using System.Linq;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Core.Markdown
{
    /// <summary>
    /// Converts a markdown string into a Paper UINode tree for rendered preview display.
    /// </summary>
    public static class MarkdownPreviewRenderer
    {
        // Colours mirror MarkdownTheme.Dark
        private static readonly PaperColour ProseCol       = new(0.88f, 0.88f, 0.94f, 1f);
        private static readonly PaperColour HeadingCol     = new(0.96f, 0.84f, 0.38f, 1f);
        private static readonly PaperColour CodeCol        = new(0.31f, 0.89f, 0.70f, 1f);
        private static readonly PaperColour QuoteCol       = new(0.50f, 0.60f, 0.85f, 1f);
        private static readonly PaperColour ListMarkCol    = new(0.55f, 0.55f, 0.75f, 1f);
        private static readonly PaperColour HrCol          = new(0.28f, 0.28f, 0.35f, 1f);
        private static readonly PaperColour CodeBgCol      = new(0.10f, 0.10f, 0.15f, 1f);
        private static readonly PaperColour InlineCodeBgCol= new(0.15f, 0.15f, 0.22f, 1f);
        private static readonly PaperColour QuoteBorderCol = new(0.38f, 0.50f, 0.75f, 0.7f);

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Renders a markdown string as a Paper UINode tree.</summary>
        public static UINode Render(string markdown, StyleSheet? outerStyle = null)
        {
            var blocks = ParseBlocks(markdown);
            var children = new List<UINode>(blocks.Count);
            for (int i = 0; i < blocks.Count; i++)
            {
                var node = RenderBlock(blocks[i]);
                if (node != null) children.Add(node);
            }

            var container = new StyleSheet
            {
                Display        = Display.Flex,
                FlexDirection  = FlexDirection.Column,
                Gap            = Length.Px(10),
                Padding        = new Thickness(Length.Px(12)),
                OverflowY      = Overflow.Auto,
                Color          = ProseCol,
            };
            if (outerStyle != null) container = container.Merge(outerStyle);
            return UI.Box(container, children.ToArray());
        }

        // ── Block rendering ───────────────────────────────────────────────────

        private static UINode? RenderBlock(Block block) => block.Kind switch
        {
            BlockKind.Empty          => null,
            BlockKind.HorizontalRule => RenderHr(),
            BlockKind.Heading        => RenderHeading(block),
            BlockKind.CodeBlock      => RenderCodeBlock(block),
            BlockKind.Blockquote     => RenderBlockquote(block),
            BlockKind.ListItem       => RenderListItem(block),
            _                        => RenderParagraph(block),
        };

        private static UINode RenderHr() =>
            UI.Box(new StyleSheet { Height = Length.Px(1), Background = HrCol,
                MarginTop = Length.Px(4), MarginBottom = Length.Px(4) });

        private static UINode RenderHeading(Block block)
        {
            float size = block.Level switch { 1 => 22f, 2 => 18f, 3 => 15f, _ => 13f };
            return UI.Box(
                new StyleSheet
                {
                    Display        = Display.Flex,
                    FlexDirection  = FlexDirection.Row,
                    FlexWrap       = FlexWrap.Wrap,
                    MarginTop      = block.Level <= 2 ? Length.Px(8) : Length.Px(2),
                },
                BuildInlineSpans(block.TextContent, new StyleSheet { Color = HeadingCol, FontSize = size, FontWeight = FontWeight.Bold }));
        }

        private static UINode RenderCodeBlock(Block block)
        {
            string code = string.Join("\n", block.Lines);
            return UI.Box(
                new StyleSheet
                {
                    Background    = CodeBgCol,
                    BorderRadius  = 4f,
                    Padding       = new Thickness(Length.Px(10), Length.Px(12)),
                    Display       = Display.Block,
                    OverflowX     = Overflow.Scroll,
                },
                UI.Text(code, new StyleSheet { Color = CodeCol, WhiteSpace = WhiteSpace.PreWrap }));
        }

        private static UINode RenderBlockquote(Block block)
        {
            var inner = WrapSpans(BuildInlineSpans(block.TextContent, new StyleSheet { Color = QuoteCol }));
            return UI.Box(
                new StyleSheet
                {
                    Display       = Display.Flex,
                    FlexDirection = FlexDirection.Row,
                    BorderLeft    = new Border(3f, QuoteBorderCol),
                    PaddingLeft   = Length.Px(12),
                    PaddingTop    = Length.Px(4),
                    PaddingBottom = Length.Px(4),
                },
                inner);
        }

        private static UINode RenderListItem(Block block)
        {
            string bullet = block.Ordered ? $"{block.Number}." : "•";
            var textPart = WrapSpans(BuildInlineSpans(block.TextContent, null), flexGrow: 1);
            return UI.Box(
                new StyleSheet { Display = Display.Flex, FlexDirection = FlexDirection.Row, Gap = Length.Px(8), AlignItems = AlignItems.FlexStart },
                UI.Text(bullet, new StyleSheet { Color = ListMarkCol, MinWidth = Length.Px(16) }),
                textPart);
        }

        private static UINode RenderParagraph(Block block)
        {
            var spans = BuildInlineSpans(block.TextContent, null);
            return WrapSpans(spans);
        }

        // ── Inline span builder ───────────────────────────────────────────────

        private static UINode[] BuildInlineSpans(string text, StyleSheet? overrideStyle)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<UINode>();

            var tokens = MarkdownTokenizer.Tokenize(text);
            if (tokens.Count == 0)
                return new[] { MakeText(text, overrideStyle) };

            var result = new List<UINode>();
            foreach (var tok in tokens)
            {
                if (string.IsNullOrEmpty(tok.Text)) continue;
                switch (tok.Type)
                {
                    case MarkdownTokenType.Text:
                    case MarkdownTokenType.HeadingText:
                        result.Add(MakeText(tok.Text, overrideStyle));
                        break;
                    case MarkdownTokenType.Bold:
                        result.Add(MakeText(tok.Text, overrideStyle, bold: true));
                        break;
                    case MarkdownTokenType.Italic:
                        result.Add(MakeText(tok.Text, overrideStyle, italic: true));
                        break;
                    case MarkdownTokenType.BoldItalic:
                        result.Add(MakeText(tok.Text, overrideStyle, bold: true, italic: true));
                        break;
                    case MarkdownTokenType.InlineCode:
                        result.Add(UI.Box(
                            new StyleSheet
                            {
                                Background   = InlineCodeBgCol,
                                Padding      = new Thickness(Length.Px(1), Length.Px(5)),
                                BorderRadius = 3f,
                                Display      = Display.Flex,
                                FlexDirection= FlexDirection.Row,
                                AlignItems   = AlignItems.Center,
                            },
                            UI.Text(tok.Text, new StyleSheet { Color = CodeCol })));
                        break;
                    case MarkdownTokenType.Delimiter:
                    case MarkdownTokenType.HeadingMarker:
                    case MarkdownTokenType.BlockquoteMarker:
                    case MarkdownTokenType.ListMarker:
                    case MarkdownTokenType.HrMarker:
                    case MarkdownTokenType.CodeFenceMarker:
                    case MarkdownTokenType.CodeFenceContent:
                        // Skip source markers in preview
                        break;
                }
            }
            return result.Count > 0 ? result.ToArray() : new[] { MakeText(text, overrideStyle) };
        }

        private static UINode MakeText(string text, StyleSheet? overrideStyle, bool bold = false, bool italic = false)
        {
            var col   = overrideStyle?.Color ?? ProseCol;
            var size  = overrideStyle?.FontSize;
            var style = new StyleSheet
            {
                Color      = col,
                FontSize   = size,
                FontWeight = bold   ? FontWeight.Bold   : overrideStyle?.FontWeight,
                FontStyle  = italic ? Styles.FontStyle.Italic : overrideStyle?.FontStyle,
                WhiteSpace = WhiteSpace.Normal,
            };
            return UI.Text(text, style);
        }

        // Wraps span arrays in an inline flex-wrap row. If there's a single span, returns it directly.
        private static UINode WrapSpans(UINode[] spans, float flexGrow = 0)
        {
            if (spans.Length == 0)
                return UI.Box(new StyleSheet { Display = Display.Flex });
            if (spans.Length == 1 && flexGrow == 0)
                return spans[0];
            var style = new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.Row,
                FlexWrap      = FlexWrap.Wrap,
                AlignItems    = AlignItems.FlexStart,
                FlexGrow      = flexGrow > 0 ? flexGrow : null,
            };
            return UI.Box(style, spans);
        }

        // ── Block parser ──────────────────────────────────────────────────────

        private enum BlockKind { Empty, Heading, Paragraph, CodeBlock, Blockquote, ListItem, HorizontalRule }

        private sealed class Block
        {
            public BlockKind Kind;
            public int Level;            // heading: 1-6
            public bool Ordered;         // list item ordered?
            public int Number;           // ordered list number
            public string TextContent = "";
            public string[] Lines = Array.Empty<string>();
        }

        private static List<Block> ParseBlocks(string markdown)
        {
            var blocks    = new List<Block>();
            var lines     = markdown.Split('\n');
            bool inFence  = false;
            var fenceLines= new List<string>();
            var paraLines = new List<string>();

            void FlushPara()
            {
                if (paraLines.Count == 0) return;
                blocks.Add(new Block { Kind = BlockKind.Paragraph, TextContent = string.Join(" ", paraLines) });
                paraLines.Clear();
            }

            foreach (var line in lines)
            {
                // ── Inside a code fence ──────────────────────────────────────
                if (inFence)
                {
                    if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                    {
                        inFence = false;
                        blocks.Add(new Block { Kind = BlockKind.CodeBlock, Lines = fenceLines.ToArray() });
                        fenceLines.Clear();
                    }
                    else fenceLines.Add(line);
                    continue;
                }

                // ── Opening code fence ───────────────────────────────────────
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                { FlushPara(); inFence = true; continue; }

                // ── Blank line ───────────────────────────────────────────────
                if (string.IsNullOrWhiteSpace(line))
                { FlushPara(); continue; }

                // ── Horizontal rule ──────────────────────────────────────────
                {
                    string t = line.Replace(" ", "");
                    if (t.Length >= 3 && (t.All(c => c == '-') || t.All(c => c == '*') || t.All(c => c == '_')))
                    { FlushPara(); blocks.Add(new Block { Kind = BlockKind.HorizontalRule }); continue; }
                }

                // ── Heading ──────────────────────────────────────────────────
                if (line[0] == '#')
                {
                    int hc = 0;
                    while (hc < line.Length && line[hc] == '#') hc++;
                    if (hc <= 6 && hc < line.Length && line[hc] == ' ')
                    {
                        FlushPara();
                        blocks.Add(new Block { Kind = BlockKind.Heading, Level = hc, TextContent = line[(hc + 1)..] });
                        continue;
                    }
                }

                // ── Blockquote ───────────────────────────────────────────────
                if (line.Length >= 2 && line[0] == '>' && line[1] == ' ')
                {
                    FlushPara();
                    blocks.Add(new Block { Kind = BlockKind.Blockquote, TextContent = line[2..] });
                    continue;
                }

                // ── Unordered list item ──────────────────────────────────────
                if (line.Length >= 2 && (line[0] == '-' || line[0] == '*') && line[1] == ' ')
                {
                    FlushPara();
                    blocks.Add(new Block { Kind = BlockKind.ListItem, TextContent = line[2..] });
                    continue;
                }

                // ── Ordered list item ────────────────────────────────────────
                {
                    int j = 0;
                    while (j < line.Length && char.IsDigit(line[j])) j++;
                    if (j > 0 && j + 1 < line.Length && line[j] == '.' && line[j + 1] == ' ')
                    {
                        FlushPara();
                        blocks.Add(new Block { Kind = BlockKind.ListItem, Ordered = true, Number = int.Parse(line[..j]), TextContent = line[(j + 2)..] });
                        continue;
                    }
                }

                // ── Paragraph line ───────────────────────────────────────────
                paraLines.Add(line);
            }

            FlushPara();
            if (fenceLines.Count > 0)
                blocks.Add(new Block { Kind = BlockKind.CodeBlock, Lines = fenceLines.ToArray() });

            return blocks;
        }
    }
}
