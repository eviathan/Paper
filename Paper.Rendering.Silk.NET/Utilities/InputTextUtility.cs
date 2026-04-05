using Paper.Core.Reconciler;
using Paper.Core.VirtualDom;

namespace Paper.Rendering.Silk.NET.Utilities
{
    /// <summary>Text input helpers: caret positioning, word navigation, selection clamping.</summary>
    public static class InputTextUtility
    {
        public static bool IsTextInput(string? elementType) =>
            elementType == ElementTypes.Input || elementType == ElementTypes.Textarea || elementType == ElementTypes.MarkdownEditor;

        public static bool IsMultiLineInput(string? elementType) =>
            elementType == ElementTypes.Textarea || elementType == ElementTypes.MarkdownEditor;

        /// <summary>Returns the Input, Textarea, or MarkdownEditor that contains this fiber (self or ancestor), or null.</summary>
        public static Fiber? GetInputAncestorOrSelf(Fiber? target)
        {
            for (var fiber = target; fiber != null; fiber = fiber.Parent)
                if (IsTextInput(fiber.Type as string))
                    return fiber;

            return null;
        }

        /// <summary>Word boundaries for macOS-style double-click:
        /// returns (start, end) of the word containing <paramref name="index"/>, or (0, 0) if none.</summary>
        public static (int start, int end) GetWordBounds(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index < 0 || index > text.Length)
                return (0, 0);

            index = Math.Clamp(index, 0, text.Length);

            bool IsWordCharacter(int charIndex)
            {
                if (charIndex < 0 || charIndex >= text.Length)
                    return false;

                char character = text[charIndex];

                return char.IsLetterOrDigit(character) || character == '_';
            }

            if (index == text.Length && index > 0)
                index--;

            int start = index;
            int end = index;

            if (IsWordCharacter(index))
            {
                while (start > 0 && IsWordCharacter(start - 1))
                    start--;

                while (end < text.Length && IsWordCharacter(end))
                    end++;

                return (start, end);
            }

            // On whitespace/punctuation: select the previous word (macOS-style)
            int scanPosition = index;

            while (scanPosition > 0 && !IsWordCharacter(scanPosition - 1))
                scanPosition--;

            while (scanPosition > 0 && IsWordCharacter(scanPosition - 1))
                scanPosition--;

            start = scanPosition;

            while (scanPosition < text.Length && IsWordCharacter(scanPosition))
                scanPosition++;

            end = scanPosition;

            return (start, end);
        }

        /// <summary>Moves caret up one line in a textarea, preserving column position.</summary>
        public static int CaretUpLine(string text, int caret)
        {
            int lineStart = caret > 0
                ? text.LastIndexOf('\n', caret - 1) + 1
                : 0;

            int column = caret - lineStart;
            if (lineStart == 0)
                return 0; // Already on first line

            int previousLineEnd = lineStart - 1; // the '\n' before current line
            int previousLineStart = previousLineEnd > 0
                ? text.LastIndexOf('\n', previousLineEnd - 1) + 1
                : 0;

            int previousLineLength = previousLineEnd - previousLineStart;

            return previousLineStart + Math.Min(column, previousLineLength);
        }

        /// <summary>Moves caret down one line in a textarea, preserving column position.</summary>
        public static int CaretDownLine(string text, int caret)
        {
            int lineStart = caret > 0
                ? text.LastIndexOf('\n', caret - 1) + 1
                : 0;

            int column = caret - lineStart;
            int nextNewlineIndex = text.IndexOf('\n', caret);

            if (nextNewlineIndex < 0)
                return text.Length; // Already on last line

            int nextLineStart = nextNewlineIndex + 1;
            int nextLineEnd = text.IndexOf('\n', nextLineStart);

            int nextLineLength = nextLineEnd < 0
                ? text.Length - nextLineStart
                : nextLineEnd - nextLineStart;

            return nextLineStart + Math.Min(column, nextLineLength);
        }

        /// <summary>Returns the index of the start of the word before <paramref name="position"/> (Option+Left on macOS).</summary>
        public static int WordStartBefore(string text, int position)
        {
            if (position <= 0)
                return 0;

            int index = position - 1;

            // Skip trailing spaces
            while (index > 0 && text[index] == ' ')
                index--;

            // Skip word characters
            while (index > 0 && text[index - 1] != ' ')
                index--;

            return index;
        }

        /// <summary>Returns the index just after the end of the word after <paramref name="position"/> (Option+Right on macOS).</summary>
        public static int WordEndAfter(string text, int position)
        {
            int textLength = text.Length;

            if (position >= textLength)
                return textLength;

            int index = position;

            while (index < textLength && text[index] == ' ')
                index++;

            // Skip word characters
            while (index < textLength && text[index] != ' ')
                index++;

            return index;
        }

        /// <summary>Clamps caret and selection indices to valid range [0, <paramref name="length"/>].</summary>
        public static void ClampInputIndices(int length, ref int caret, ref int selectionStart, ref int selectionEnd)
        {
            length = Math.Max(0, length);
            caret = Math.Clamp(caret, 0, length);
            selectionStart = Math.Clamp(selectionStart, 0, length);
            selectionEnd = Math.Clamp(selectionEnd, 0, length);
        }
    }
}
