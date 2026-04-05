using Paper.Core.Markdown;
using Paper.Core.Styles;
using Paper.Core.Reconciler;
using Paper.Layout;
using Paper.Rendering.Silk.NET.Text;
using Direction = Paper.Core.Styles.Direction;

namespace Paper.Rendering.Silk.NET
{
    internal sealed partial class FiberRenderer
    {
        /// <summary>Draw selection highlight for one line (call before DrawText so it appears behind).</summary>
        private void DrawSelectionForLine(string line, int lineStart, int lineEnd, LayoutBox fullLb, LayoutBox lineBox, StyleSheet style, float scrollX, float scrollY, float inputScrollX = 0f)
        {
            if (_fonts == null) return;
            int selMin = Math.Min(FocusedInputSelStart, FocusedInputSelEnd);
            int selMax = Math.Max(FocusedInputSelStart, FocusedInputSelEnd);
            int lineSelStart = Math.Max(selMin, lineStart) - lineStart;
            int lineSelEnd = Math.Min(selMax, lineEnd) - lineStart;
            if (lineSelStart >= lineSelEnd || lineSelStart < 0 || lineSelEnd > line.Length) return;
            float fontPx = SilkTextMeasurer.ResolveFontPx(style);
            string? fam = style.FontFamily;
            var weight = style.FontWeight;
            float textH = _fonts.LineHeight(fontPx, fam, weight);
            var (padTop, _, padBottom, padLeft) = BoxModel.PaddingPixels(style, fullLb.Width, fullLb.Height);
            float xLayout = fullLb.AbsoluteX + padLeft - inputScrollX;
            float contentH = lineBox.Height - padTop - padBottom;
            float baseline = contentH >= textH
                ? lineBox.AbsoluteY + padTop + (contentH - textH) / 2f + (textH * 0.8f)
                : lineBox.AbsoluteY + padTop + (textH * 0.8f);
            float selectionStartWidth = _fonts.MeasureWidth(line.AsSpan(0, lineSelStart), fontPx, fam, weight);
            float selectionEndWidth = _fonts.MeasureWidth(line.AsSpan(0, lineSelEnd), fontPx, fam, weight);
            float selectionDrawX = (xLayout - scrollX + selectionStartWidth) * ScaleX;
            float selectionDrawY = (baseline - scrollY - textH * 0.8f) * ScaleY;
            float selectionWidth = (selectionEndWidth - selectionStartWidth) * ScaleX;
            float selectionHeight = textH * ScaleY;
            if (selectionWidth > 0 && selectionHeight > 0)
                DrawRect(selectionDrawX, selectionDrawY, selectionWidth, selectionHeight, 0.3f, 0.5f, 0.9f, 0.4f, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>Draw caret for one line (call after DrawText so it appears on top).</summary>
        private void DrawCaretForLine(string line, int lineStart, int lineEnd, LayoutBox fullLb, LayoutBox lineBox, StyleSheet style, PaperColour col, float opacity, float scrollX, float scrollY, float inputScrollX = 0f)
        {
            if (!FocusedInputCaretVisible || _fonts == null || FocusedInputCaret < lineStart || FocusedInputCaret > lineEnd + 1) return;
            float fontPx = SilkTextMeasurer.ResolveFontPx(style);
            string? fam = style.FontFamily;
            var weight = style.FontWeight;
            float textH = _fonts.LineHeight(fontPx, fam, weight);
            var (padTop, _, padBottom, padLeft) = BoxModel.PaddingPixels(style, fullLb.Width, fullLb.Height);
            float xLayout = fullLb.AbsoluteX + padLeft - inputScrollX;
            float contentH = lineBox.Height - padTop - padBottom;
            float baseline = contentH >= textH
                ? lineBox.AbsoluteY + padTop + (contentH - textH) / 2f + (textH * 0.8f)
                : lineBox.AbsoluteY + padTop + (textH * 0.8f);
            int caretOffset = Math.Min(FocusedInputCaret - lineStart, line.Length);
            float caretX = xLayout + (caretOffset <= 0 ? 0 : _fonts.MeasureWidth(line.AsSpan(0, caretOffset), fontPx, fam, weight));
            DrawCaretAt(caretX, baseline, 1f, textH, col, opacity, scrollX, scrollY);
        }

        private void DrawCaretAt(float xLayout, float baseline, float scale, float textH, PaperColour col, float opacity, float scrollX, float scrollY)
        {
            float caretDrawX = (xLayout - scrollX) * ScaleX;
            float caretDrawY = (baseline - scrollY - textH * 0.8f) * ScaleY;
            float caretDrawHeight = Math.Max(14f, textH * ScaleY);
            float caretWidth = Math.Max(2f, 2f * ScaleX);
            DrawRect(caretDrawX, caretDrawY, caretWidth, caretDrawHeight, col.R, col.G, col.B, col.A * opacity, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// Called once on cache miss. Computes (text, xOffset, color) for every segment in a row.
        /// The render loop then does batch.Add per segment with the pre-computed xOffset.
        /// </summary>
        private MdSegment[] BuildRowSegments(
            string lineText, int lineStart, int lineEnd,
            IReadOnlyList<MarkdownToken> tokens, MarkdownTheme theme,
            PaperColour defaultCol, float fontPx, string? fam, FontWeight? weight, FontStyle? fontStyle)
        {
            if (_fonts == null) return Array.Empty<MdSegment>();
            var result = new List<MdSegment>();
            int cursor = 0;
            float runW = 0f;
            foreach (var tok in tokens)
            {
                if (tok.End <= lineStart || tok.Start >= lineEnd) continue;
                int tokLocalStart = Math.Max(tok.Start, lineStart) - lineStart;
                int tokLocalEnd = Math.Min(tok.End, lineEnd) - lineStart;
                if (cursor < tokLocalStart)
                {
                    var span = lineText.AsSpan(cursor, tokLocalStart - cursor);
                    result.Add(new MdSegment(lineText[cursor..tokLocalStart], runW,
                        defaultCol.R, defaultCol.G, defaultCol.B, defaultCol.A));
                    runW += _fonts.MeasureWidth(span, fontPx, fam, weight, fontStyle);
                    cursor = tokLocalStart;
                }
                var tokCol = GetMarkdownTokenColor(tok.Type, theme, defaultCol);
                result.Add(new MdSegment(lineText[tokLocalStart..tokLocalEnd], runW,
                    tokCol.R, tokCol.G, tokCol.B, tokCol.A));
                runW += _fonts.MeasureWidth(lineText.AsSpan(tokLocalStart, tokLocalEnd - tokLocalStart), fontPx, fam, weight, fontStyle);
                cursor = tokLocalEnd;
            }
            if (cursor < lineText.Length)
                result.Add(new MdSegment(lineText[cursor..], runW,
                    defaultCol.R, defaultCol.G, defaultCol.B, defaultCol.A));
            return result.ToArray();
        }

        private static PaperColour GetMarkdownTokenColor(MarkdownTokenType type, MarkdownTheme theme, PaperColour fallback) => type switch
        {
            MarkdownTokenType.HeadingMarker => theme.HeadingMarkerColor,
            MarkdownTokenType.HeadingText => theme.HeadingColor,
            MarkdownTokenType.Delimiter => theme.DelimiterColor,
            MarkdownTokenType.Bold => theme.ProseColor,
            MarkdownTokenType.Italic => theme.ProseColor,
            MarkdownTokenType.BoldItalic => theme.ProseColor,
            MarkdownTokenType.InlineCode => theme.InlineCodeColor,
            MarkdownTokenType.BlockquoteMarker => theme.DelimiterColor,
            MarkdownTokenType.ListMarker => theme.ListMarkerColor,
            MarkdownTokenType.HrMarker => theme.HrColor,
            MarkdownTokenType.CodeFenceMarker => theme.DelimiterColor,
            MarkdownTokenType.CodeFenceContent => theme.CodeFenceColor,
            MarkdownTokenType.Text => theme.ProseColor,
            _ => fallback,
        };

        private void DrawText(string label, LayoutBox layoutBox, StyleSheet style,
                              PaperColour col, float opacity, float scrollX = 0f, float scrollY = 0f, float inputScrollX = 0f)
        {
            if (_fonts == null) return;

            float fontPx = SilkTextMeasurer.ResolveFontPx(style);
            string? fam = style.FontFamily;
            var weight = style.FontWeight;
            var fontStyle = style.FontStyle;

            var transform = style.TextTransform;
            if (transform != null && transform != TextTransform.None)
                label = transform switch
                {
                    TextTransform.Uppercase => label.ToUpperInvariant(),
                    TextTransform.Lowercase => label.ToLowerInvariant(),
                    TextTransform.Capitalize => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(label.ToLowerInvariant()),
                    _ => label,
                };

            var (batch, batchScale) = _fonts.Get(fontPx * DpiScale, fam, weight, fontStyle);
            float atlasLineH = _fonts.LineHeight(fontPx, fam, weight, fontStyle);

            if (_fonts.WillUseSyntheticItalic(fam, weight, fontStyle))
                batch.ItalicSkew = 0.21f;

            var (padTop, padRight, padBottom, padLeft) = BoxModel.PaddingPixels(style, layoutBox.Width, layoutBox.Height);

            float contentH = layoutBox.Height - padTop - padBottom;
            float baseline;
            if (contentH >= atlasLineH)
            {
                float centreY = layoutBox.AbsoluteY + padTop + (contentH - atlasLineH) / 2f;
                baseline = centreY + (atlasLineH * 0.8f);
            }
            else
            {
                baseline = layoutBox.AbsoluteY + padTop + (atlasLineH * 0.8f);
            }

            float textW = _fonts.MeasureWidth(label.AsSpan(), fontPx, fam, weight, fontStyle);
            float contentW = layoutBox.Width - padLeft - padRight;

            ReadOnlySpan<char> drawSpan = label.AsSpan();
            if (inputScrollX == 0f && contentW > 0 && textW > contentW &&
                (style.TextOverflow ?? TextOverflow.Clip) == TextOverflow.Ellipsis)
            {
                const string ellipsis = "…";
                float ellipsisW = _fonts.MeasureWidth(ellipsis.AsSpan(), fontPx, fam, weight, fontStyle);
                float available = contentW - ellipsisW;
                if (available > 0)
                {
                    int searchLow = 0, searchHigh = label.Length;
                    while (searchLow < searchHigh)
                    {
                        int mid = (searchLow + searchHigh + 1) / 2;
                        if (_fonts.MeasureWidth(label.AsSpan(0, mid), fontPx, fam, weight, fontStyle) <= available)
                            searchLow = mid;
                        else
                            searchHigh = mid - 1;
                    }
                    label = label[..searchLow] + ellipsis;
                    textW = _fonts.MeasureWidth(label.AsSpan(), fontPx, fam, weight, fontStyle);
                }
                else
                {
                    label = ellipsis;
                    textW = ellipsisW;
                }
                drawSpan = label.AsSpan();
            }

            bool doWrap = style.WhiteSpace == WhiteSpace.Normal && inputScrollX == 0f &&
                          contentW > 0 && textW > contentW;
            if (doWrap)
            {
                float spaceW = _fonts.MeasureWidth(" ".AsSpan(), fontPx, fam, weight, fontStyle);
                if (spaceW <= 0) spaceW = atlasLineH * 0.3f;
                float lineSpacing = atlasLineH * Math.Max(0.5f, style.LineHeight ?? 1.4f);
                float wrapBaseline = layoutBox.AbsoluteY + padTop + (atlasLineH * 0.8f);
                float xOrigin = layoutBox.AbsoluteX + padLeft;

                var words = label.Split(' ');
                var lineWords = new List<string>(words.Length);
                float lineW = 0;

                foreach (var word in words)
                {
                    float wordW = _fonts.MeasureWidth(word.AsSpan(), fontPx, fam, weight, fontStyle);
                    if (lineWords.Count > 0 && lineW + spaceW + wordW > contentW)
                    {
                        float lineDrawX = (xOrigin - scrollX) * ScaleX;
                        float lineDrawY = (wrapBaseline - scrollY) * ScaleY;
                        batch.Add(string.Join(' ', lineWords).AsSpan(), lineDrawX, lineDrawY, col.R, col.G, col.B, col.A * opacity, batchScale);
                        wrapBaseline += lineSpacing;
                        lineWords.Clear();
                        lineW = 0;
                    }
                    lineWords.Add(word);
                    lineW += (lineW > 0 ? spaceW : 0) + wordW;
                }
                if (lineWords.Count > 0)
                {
                    float lineDrawX = (xOrigin - scrollX) * ScaleX;
                    float lineDrawY = (wrapBaseline - scrollY) * ScaleY;
                    batch.Add(string.Join(' ', lineWords).AsSpan(), lineDrawX, lineDrawY, col.R, col.G, col.B, col.A * opacity, batchScale);
                }
                return;
            }

            float xLayout;
            bool isRtl = style.Direction == Direction.Rtl;
            if (inputScrollX != 0f)
                xLayout = layoutBox.AbsoluteX + padLeft - inputScrollX;
            else
            {
                var textAlign = style.TextAlign ?? TextAlign.Left;
                if (isRtl)
                {
                    textAlign = textAlign switch
                    {
                        TextAlign.Left => TextAlign.Right,
                        TextAlign.Right => TextAlign.Left,
                        _ => textAlign
                    };
                }
                xLayout = textAlign switch
                {
                    TextAlign.Center => layoutBox.AbsoluteX + padLeft + Math.Max(0, (contentW - textW) / 2f),
                    TextAlign.Right => layoutBox.AbsoluteX + layoutBox.Width - padRight - textW,
                    _ => layoutBox.AbsoluteX + padLeft,
                };
            }

            float textDrawX = (xLayout - scrollX) * ScaleX;
            float textDrawY = (baseline - scrollY) * ScaleY;
            batch.Add(drawSpan, textDrawX, textDrawY, col.R, col.G, col.B, col.A * opacity, batchScale);
        }
    }
}
