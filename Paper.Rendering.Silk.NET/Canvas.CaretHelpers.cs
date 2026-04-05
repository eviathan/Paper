using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Paper.Rendering.Silk.NET.Utilities;

namespace Paper.Rendering.Silk.NET
{
    public sealed partial class Canvas
    {
        /// <summary>Character index in the input's text for the given layout x/y (e.g. from mouse click).</summary>
        private int GetCaretIndexFromX(Fiber fiber, float mouseX, float mouseY, float scrollX = 0f, float scrollY = 0f, float inputScrollX = 0f)
        {
            if (_text == null) return 0;
            var style = fiber.ComputedStyle;
            var layoutBox = fiber.Layout;
            var padding = style.Padding ?? Thickness.Zero;
            float padLeft = padding.Left.Resolve(layoutBox.Width);
            float padTop = padding.Top.Resolve(layoutBox.Height);
            float contentLeft = layoutBox.AbsoluteX - scrollX + padLeft - inputScrollX;
            float contentTop = layoutBox.AbsoluteY - scrollY + padTop;
            float contentX = mouseX - contentLeft;
            string text = fiber.Props?.Text ?? "";
            if (text.Length == 0) return 0;

            var atlas = _text.Atlas;
            float baseSize = atlas.BaseSize > 0 ? atlas.BaseSize : 16f;
            float fontPx = style.FontSize is { } fontSize && !fontSize.IsAuto ? fontSize.Resolve(baseSize) : baseSize;
            float scale = baseSize > 0 ? fontPx / baseSize : 1f;
            float lineHeight = (_fontSet?.LineHeight(fontPx) ?? baseSize) * Math.Max(0.5f, style.LineHeight ?? 1.4f);
            float contentWidth = layoutBox.Width - padLeft - padding.Right.Resolve(layoutBox.Width);
            if (contentWidth <= 0) contentWidth = layoutBox.Width;

            string? elementType = fiber.Type as string;
            bool isMultiline = elementType == ElementTypes.Textarea || elementType == ElementTypes.MarkdownEditor;

            if (isMultiline)
                return GetCaretIndexInMultilineText(text, mouseY, contentTop, contentX, contentWidth, fontPx, scale, lineHeight);

            return GetCaretIndexInSingleLineText(text, contentX, scale);
        }

        private int GetCaretIndexInMultilineText(string text, float mouseY, float contentTop, float contentX, float contentWidth, float fontPx, float scale, float lineHeight)
        {
            float contentY = mouseY - contentTop;
            if (contentY < 0) return 0;

            var logicalLines = text.Split('\n');
            int charOffset = 0;
            float runningLineY = 0f;

            for (int lineIndex = 0; lineIndex < logicalLines.Length; lineIndex++)
            {
                var logLine = logicalLines[lineIndex];
                var wrappedSegments = WrapTextLineForCaret(logLine, charOffset, contentWidth, fontPx);

                foreach (var seg in wrappedSegments)
                {
                    float lineBottom = runningLineY + lineHeight;
                    if (contentY >= runningLineY && contentY < lineBottom)
                        return GetCaretIndexInSegment(seg.Text, seg.Start, seg.End, contentX, scale);
                    runningLineY += lineHeight;
                }

                charOffset += logLine.Length + 1;
            }

            return text.Length;
        }

        private int GetCaretIndexInSegment(string segmentText, int segmentStart, int segmentEnd, float contentX, float scale)
        {
            if (contentX <= 0) return segmentStart;
            float segmentWidth = _text!.MeasureWidth(segmentText.AsSpan()) * scale;
            if (contentX >= segmentWidth) return segmentEnd;

            for (int charIndex = 0; charIndex < segmentText.Length; charIndex++)
            {
                float widthUpToCurrent = _text.MeasureWidth(segmentText.AsSpan(0, charIndex + 1)) * scale;
                if (contentX < widthUpToCurrent)
                {
                    float widthUpToPrevious = charIndex > 0 ? _text.MeasureWidth(segmentText.AsSpan(0, charIndex)) * scale : 0;
                    return segmentStart + (contentX < (widthUpToPrevious + widthUpToCurrent) / 2f ? charIndex : charIndex + 1);
                }
            }

            return segmentEnd;
        }

        private int GetCaretIndexInSingleLineText(string text, float contentX, float scale)
        {
            if (contentX <= 0) return 0;
            float totalWidth = _text!.MeasureWidth(text.AsSpan()) * scale;
            if (contentX >= totalWidth) return text.Length;

            for (int charIndex = 0; charIndex < text.Length; charIndex++)
            {
                float widthUpToCurrent = _text.MeasureWidth(text.AsSpan(0, charIndex + 1)) * scale;
                if (contentX < widthUpToCurrent)
                {
                    float widthUpToPrevious = charIndex > 0 ? _text.MeasureWidth(text.AsSpan(0, charIndex)) * scale : 0;
                    return contentX < (widthUpToPrevious + widthUpToCurrent) / 2f ? charIndex : charIndex + 1;
                }
            }

            return text.Length;
        }

        /// <summary>Word-wrap a single line of text for caret calculation. Returns sub-lines with char offsets.</summary>
        private List<(string Text, int Start, int End)> WrapTextLineForCaret(string line, int offset, float maxWidth, float fontPx = 16f)
        {
            var result = new List<(string, int, int)>();

            if (_fontSet == null || maxWidth <= 0 || line.Length == 0)
            {
                result.Add((line, offset, offset + line.Length));
                return result;
            }

            int start = 0;
            while (start < line.Length)
            {
                float lineWidth = 0f;
                int end = start;
                int lastSpace = -1;

                while (end < line.Length)
                {
                    char character = line[end];
                    float charWidth = _fontSet.MeasureWidth(line.AsSpan(end, 1), fontPx);
                    if (lineWidth + charWidth > maxWidth && end > start) break;
                    if (character == ' ') lastSpace = end;
                    lineWidth += charWidth;
                    end++;
                }

                if (end == line.Length)
                {
                    result.Add((line[start..], offset + start, offset + line.Length));
                    break;
                }

                int wrapAt = lastSpace > start ? lastSpace + 1 : end;
                result.Add((line[start..wrapAt].TrimEnd(), offset + start, offset + wrapAt));
                start = wrapAt;
            }

            if (result.Count == 0)
                result.Add(("", offset, offset));

            return result;
        }
    }
}
