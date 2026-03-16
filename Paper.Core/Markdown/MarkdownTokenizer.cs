namespace Paper.Core.Markdown
{
    /// <summary>
    /// Converts a markdown source string into a flat, ordered list of <see cref="MarkdownToken"/>s.
    /// Every source character belongs to exactly one token — no characters are removed or shifted —
    /// so caret positions are identical to those of a plain textarea.
    /// </summary>
    public static class MarkdownTokenizer
    {
        public static IReadOnlyList<MarkdownToken> Tokenize(string source)
        {
            if (string.IsNullOrEmpty(source))
                return Array.Empty<MarkdownToken>();

            var tokens = new List<MarkdownToken>(source.Length / 4);
            var lines  = source.Split('\n');
            int offset = 0;
            bool inCodeFence = false;

            foreach (var line in lines)
            {
                if (inCodeFence)
                {
                    if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                    {
                        Add(tokens, MarkdownTokenType.CodeFenceMarker, offset, line);
                        inCodeFence = false;
                    }
                    else
                    {
                        Add(tokens, MarkdownTokenType.CodeFenceContent, offset, line);
                    }
                }
                else if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    Add(tokens, MarkdownTokenType.CodeFenceMarker, offset, line);
                    inCodeFence = true;
                }
                else
                {
                    TokenizeLine(tokens, source, offset, line);
                }

                offset += line.Length + 1; // +1 for the \n
            }

            return tokens;
        }

        // ── Line-level dispatch ───────────────────────────────────────────────

        private static void TokenizeLine(List<MarkdownToken> tokens, string source, int offset, string line)
        {
            int len = line.Length;

            // Blank line
            if (len == 0) return;

            // Horizontal rule: "---", "***", "___" (3+ chars, all same, optional spaces)
            if (IsHorizontalRule(line))
            {
                Add(tokens, MarkdownTokenType.HrMarker, offset, line);
                return;
            }

            // Heading: leading # chars
            if (line[0] == '#')
            {
                int hashes = 0;
                while (hashes < len && line[hashes] == '#') hashes++;
                if (hashes <= 6 && hashes < len && line[hashes] == ' ')
                {
                    // Marker = "### "
                    int markerLen = hashes + 1;
                    Add(tokens, MarkdownTokenType.HeadingMarker, offset, line[..markerLen]);
                    // Remaining text tokenised as heading text (no inline emphasis in source mode)
                    string rest = line[markerLen..];
                    Add(tokens, MarkdownTokenType.HeadingText, offset + markerLen, rest);
                    return;
                }
            }

            // Blockquote: "> "
            if (len >= 2 && line[0] == '>' && line[1] == ' ')
            {
                Add(tokens, MarkdownTokenType.BlockquoteMarker, offset, "> ");
                TokenizeInline(tokens, line[2..], offset + 2);
                return;
            }

            // Unordered list: "- " or "* "
            if (len >= 2 && (line[0] == '-' || line[0] == '*') && line[1] == ' ')
            {
                Add(tokens, MarkdownTokenType.ListMarker, offset, line[..2]);
                TokenizeInline(tokens, line[2..], offset + 2);
                return;
            }

            // Ordered list: "1. " (any number followed by ". ")
            {
                int i = 0;
                while (i < len && char.IsDigit(line[i])) i++;
                if (i > 0 && i + 1 < len && line[i] == '.' && line[i + 1] == ' ')
                {
                    int markerLen = i + 2;
                    Add(tokens, MarkdownTokenType.ListMarker, offset, line[..markerLen]);
                    TokenizeInline(tokens, line[markerLen..], offset + markerLen);
                    return;
                }
            }

            // Plain prose line — tokenise inline
            TokenizeInline(tokens, line, offset);
        }

        // ── Inline tokeniser ─────────────────────────────────────────────────

        private static void TokenizeInline(List<MarkdownToken> tokens, string text, int baseOffset)
        {
            // Build a list of (startInText, endInText, type) regions for styled content,
            // then fill the gaps with Text tokens.

            var regions = new List<(int Start, int End, MarkdownTokenType ContentType)>();
            FindInlineRegions(text, regions);

            // Sort by start position
            regions.Sort((a, b) => a.Start.CompareTo(b.Start));

            // Remove overlapping regions (keep first one)
            for (int i = regions.Count - 1; i > 0; i--)
                if (regions[i].Start < regions[i - 1].End)
                    regions.RemoveAt(i);

            int cursor = 0;
            foreach (var (start, end, type) in regions)
            {
                // Text before this region
                if (cursor < start)
                    Add(tokens, MarkdownTokenType.Text, baseOffset + cursor, text[cursor..start]);

                // Determine delimiter width
                int delimWidth = DelimiterWidth(type);

                // Opening delimiter
                if (delimWidth > 0 && start + delimWidth <= end)
                    Add(tokens, MarkdownTokenType.Delimiter, baseOffset + start, text[start..(start + delimWidth)]);

                // Content
                int contentStart = start + delimWidth;
                int contentEnd   = end   - delimWidth;
                if (contentEnd > contentStart)
                    Add(tokens, type, baseOffset + contentStart, text[contentStart..contentEnd]);

                // Closing delimiter
                if (delimWidth > 0 && contentEnd >= start + delimWidth)
                    Add(tokens, MarkdownTokenType.Delimiter, baseOffset + contentEnd, text[contentEnd..end]);

                cursor = end;
            }

            // Trailing plain text
            if (cursor < text.Length)
                Add(tokens, MarkdownTokenType.Text, baseOffset + cursor, text[cursor..]);
        }

        // ── Region finder: locates bold/italic/code spans ────────────────────

        private static void FindInlineRegions(string text, List<(int, int, MarkdownTokenType)> regions)
        {
            int i = 0;
            int len = text.Length;

            while (i < len)
            {
                // Inline code: highest priority, suppresses other parsing
                if (text[i] == '`')
                {
                    int close = text.IndexOf('`', i + 1);
                    if (close > i)
                    {
                        regions.Add((i, close + 1, MarkdownTokenType.InlineCode));
                        i = close + 1;
                        continue;
                    }
                }

                // Bold+italic: ***text*** or ___text___
                if (i + 2 < len && IsDelimChar(text[i]) && text[i] == text[i + 1] && text[i] == text[i + 2])
                {
                    char dc = text[i];
                    int close = FindClose(text, i + 3, len, dc, 3);
                    if (close >= 0)
                    {
                        regions.Add((i, close + 3, MarkdownTokenType.BoldItalic));
                        i = close + 3;
                    }
                    else i += 3; // unmatched — skip all 3 chars so they emit as plain text
                    continue;
                }

                // Bold: **text** or __text__
                if (i + 1 < len && IsDelimChar(text[i]) && text[i] == text[i + 1])
                {
                    char dc = text[i];
                    int close = FindClose(text, i + 2, len, dc, 2);
                    if (close >= 0)
                    {
                        regions.Add((i, close + 2, MarkdownTokenType.Bold));
                        i = close + 2;
                    }
                    else i += 2; // unmatched — skip both chars
                    continue;
                }

                // Italic: *text* or _text_
                if (IsDelimChar(text[i]))
                {
                    char dc = text[i];
                    int close = FindClose(text, i + 1, len, dc, 1);
                    if (close >= 0)
                    {
                        regions.Add((i, close + 1, MarkdownTokenType.Italic));
                        i = close + 1;
                    }
                    else i++;
                    continue;
                }

                i++;
            }
        }

        // Find the position of a closing delimiter of `count` dc chars, starting at `from`.
        // Returns the index of the FIRST closing dc char (i.e. the region end - count).
        private static int FindClose(string text, int from, int len, char dc, int count)
        {
            for (int i = from; i <= len - count; i++)
            {
                if (text[i] == dc)
                {
                    bool match = true;
                    for (int j = 1; j < count; j++)
                        if (text[i + j] != dc) { match = false; break; }
                    // Make sure it's not immediately followed by more of the same char
                    if (match && (i + count >= len || text[i + count] != dc))
                        return i;
                }
            }
            return -1;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool IsDelimChar(char c) => c == '*' || c == '_';

        private static int DelimiterWidth(MarkdownTokenType type) => type switch
        {
            MarkdownTokenType.Bold        => 2,
            MarkdownTokenType.Italic      => 1,
            MarkdownTokenType.BoldItalic  => 3,
            MarkdownTokenType.InlineCode  => 1,
            _                             => 0,
        };

        private static bool IsHorizontalRule(string line)
        {
            // Must be 3+ of the same char (-, *, _) with optional spaces, nothing else
            string trimmed = line.Replace(" ", "");
            if (trimmed.Length < 3) return false;
            char c = trimmed[0];
            if (c != '-' && c != '*' && c != '_') return false;
            foreach (char ch in trimmed)
                if (ch != c) return false;
            return true;
        }

        private static void Add(List<MarkdownToken> tokens, MarkdownTokenType type, int offset, string text)
        {
            if (text.Length == 0) return;
            tokens.Add(new MarkdownToken { Type = type, Start = offset, End = offset + text.Length, Text = text });
        }
    }
}
