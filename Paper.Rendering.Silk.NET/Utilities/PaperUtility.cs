using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Paper.Core.Reconciler;
using Paper.Core.VirtualDom;
using Paper.Core.Events;
using Paper.Core.Styles;
using Paper.Core.Hooks;
using Paper.Layout;
using Paper.Rendering.Silk.NET.Text;
using Silk.NET.Input;
using MouseButton = Silk.NET.Input.MouseButton;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;
using Silk.NET.GLFW;


namespace Paper.Rendering.Silk.NET.Utilities
{
    public static class PaperUtility
    {
        /// <summary>Convert input position (window coords) to layout space (logical pixels).</summary>
        public static (float x, float y) ToLayoutCoords(Vector2 position) =>
            (position.X, position.Y);

        public static List<Fiber> PathToRoot(Fiber target)
        {
            var path = new List<Fiber>();

            Fiber? current = target;
            while (current != null)
            {
                path.Add(current);
                current = current.Parent;
            }

            path.Reverse();

            return path;
        }

        /// <summary>Stable path string (indices from root) so we can match the same control after the tree is replaced.</summary>
        public static string GetPathString(Fiber? fiber)
        {
            if (fiber == null)
                return "";

            var path = PathToRoot(fiber);

            return string.Join(".", path.Select(fiber => fiber.Index));
        }

        /// <summary>Word boundaries for macOS-style double-click: (start, end) of the word containing index, or (idx, idx) if none.</summary>
        public static (int start, int end) GetWordBounds(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index < 0 || index > text.Length)
                return (0, 0);

            index = Math.Clamp(index, 0, text.Length);

            bool isWordChar(int charIndex)
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

            if (isWordChar(index))
            {
                while (start > 0 && isWordChar(start - 1))
                    start--;

                while (end < text.Length && isWordChar(end))
                    end++;

                return (start, end);
            }

            // On whitespace/punctuation: select the previous word (macOS-style)
            int p = index;

            while (p > 0 && !isWordChar(p - 1))
                p--;

            while (p > 0 && isWordChar(p - 1))
                p--;
            
            start = p;

            while (p < text.Length && isWordChar(p))
                p++;

            end = p;

            return (start, end);
        }

        /// <summary>True if a is the same as b or one is a descendant of the other (same "control" for click).</summary>
        public static bool IsSameControl(Fiber? a, Fiber? b)
        {
            if (a == null || b == null)
                return false;

            if (ReferenceEquals(a, b))
                return true;

            return IsDescendantOf(a, b) || IsDescendantOf(b, a);
        }

        public static bool IsDescendantOf(Fiber? node, Fiber? ancestor)
        {
            for (var parent = node?.Parent; parent != null; parent = parent.Parent)
                if (ReferenceEquals(parent, ancestor)) return true;

            return false;
        }

        public static bool IsTextInput(string? type) =>
            type == ElementTypes.Input || type == ElementTypes.Textarea || type == ElementTypes.MarkdownEditor;

        public static bool IsMultiLineInput(string? type) =>
            type == ElementTypes.Textarea || type == ElementTypes.MarkdownEditor;

        /// <summary>Returns the Input, Textarea, or MarkdownEditor that contains this fiber (self or ancestor), or null.</summary>
        public static Fiber? GetInputAncestorOrSelf(Fiber? target)
        {
            for (var fiber = target; fiber != null; fiber = fiber.Parent)
                if (IsTextInput(fiber.Type as string))
                    return fiber;

            return null;
        }

        /// <summary>Returns the fiber that should receive the click: the first (deepest) that has an OnClick, OnDoubleClick, or OnCheckedChange handler.</summary>
        public static Fiber? GetClickTarget(Fiber? target)
        {
            for (var fiber = target; fiber != null; fiber = fiber.Parent)
                if (fiber.Props.OnClick != null || fiber.Props.OnDoubleClick != null || fiber.Props.OnCheckedChange != null)
                    return fiber;

            return null;
        }

        /// <summary>Resolve relative image paths (e.g. "Assets/test.png") relative to the app base directory so they load from output.</summary>
        public static string? ResolveImagePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (Path.IsPathRooted(path))
                return path;

            return Path.Combine(AppContext.BaseDirectory, path);
        }

        public static void InvalidateStyleTree(Fiber? fiber)
        {
            while (fiber != null)
            {
                fiber.StyleDirty = true;
                InvalidateStyleTree(fiber.Child);
                fiber = fiber.Sibling;
            }
        }

        /// <summary>Moves caret up one line in a textarea, preserving column position.</summary>
        public static int CaretUpLine(string text, int caret)
        {
            // Find start of current line
            int lineStart = caret > 0 
                ? text.LastIndexOf('\n', caret - 1) + 1
                : 0;

            int col = caret - lineStart;
            if (lineStart == 0)
                return 0; // Already on first line

            // Find start of previous line
            int prevLineEnd = lineStart - 1; // The '\n' before current line
            int prevLineStart = prevLineEnd > 0 
                ? text.LastIndexOf('\n', prevLineEnd - 1) + 1
                : 0;

            int prevLineLen = prevLineEnd - prevLineStart;

            return prevLineStart + Math.Min(col, prevLineLen);
        }

        /// <summary>Moves caret down one line in a textarea, preserving column position.</summary>
        public static int CaretDownLine(string text, int caret)
        {
            int lineStart = caret > 0
                ? text.LastIndexOf('\n', caret - 1) + 1
                : 0;

            int col = caret - lineStart;
            int nextNewline = text.IndexOf('\n', caret);

            if (nextNewline < 0)
                return text.Length; // Already on last line

            int nextLineStart = nextNewline + 1;
            int nextNewline2 = text.IndexOf('\n', nextLineStart);

            int nextLineLen = nextNewline2 < 0
                ? text.Length - nextLineStart
                : nextNewline2 - nextLineStart;

            return nextLineStart + Math.Min(col, nextLineLen);
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

            int i = position;
            
            while (i < textLength && text[i] == ' ')
                i++;

            // Skip word characters
            while (i < textLength && text[i] != ' ')
                i++;

            return i;
        }

        public static void ClampInputIndices(int length, ref int caret, ref int selectionStart, ref int selectionEnd)
        {
            length = Math.Max(0, length);
            caret = Math.Clamp(caret, 0, length);
            selectionStart = Math.Clamp(selectionStart, 0, length);
            selectionEnd = Math.Clamp(selectionEnd, 0, length);
        }

        public static void InvokePointerHandlers(Fiber fiber, PointerEvent pointerEvent, bool capture)
        {
            var props = fiber.Props;
            switch (pointerEvent.Type)
            {
                case PointerEventType.Move:
                    (capture ? props.OnPointerMoveCapture : props.OnPointerMove)?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Down:
                    (capture ? props.OnPointerDownCapture : props.OnPointerDown)?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Up:
                    (capture ? props.OnPointerUpCapture : props.OnPointerUp)?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Click:
                    (capture ? props.OnPointerClickCapture : props.OnPointerClick)?.Invoke(pointerEvent);
                    break;
                case PointerEventType.DoubleClick:
                    (capture ? props.OnPointerClickCapture : props.OnPointerClick)?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Enter:
                    props.OnPointerEnter?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Leave:
                    props.OnPointerLeave?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Wheel:
                    props.OnWheel?.Invoke(pointerEvent);
                    break;
            }
        }

        /// <summary>Find the fiber at the given path (e.g. "0.2.0.1") in the current tree so focus points at the live fiber after re-render. Path is from PathToRoot (root first, then child indices).</summary>
        public static Fiber? GetFiberByPath(Fiber? root, string? path)
        {
            if (root == null || string.IsNullOrEmpty(path))
                return null;

            var parts = path.Split('.');

            Fiber? current = root;

            // First part is root's index; descend using the rest (child index from root, then grandchild index, ...).
            for (int p = 1; p < parts.Length; p++)
            {
                if (current == null || !int.TryParse(parts[p], out int index))
                    return null;
                    
                var child = current.Child;
                for (int i = 0; child != null && i < index; i++)
                    child = child.Sibling;

                current = child;
            }

            return current;
        }

        /// <summary>
        /// Walk the fiber tree depth-first and return the deepest fiber at (x,y).
        /// Uses path and scroll offset so hit-testing matches visible (scrolled) positions.
        /// </summary>
        public static Fiber? HitTest(
            Fiber? fiber,
            float x,
            float y,
            string parentPath,
            int indexInParent,
            float scrollX,
            float scrollY,
            Func<string, (float scrollOffsetX, float scrollOffsetY)> getScrollOffset)
        {
            if (fiber == null)
                return null;

            string path = string.IsNullOrEmpty(parentPath)
                ? indexInParent.ToString()
                : parentPath + "." + indexInParent;

            var (scrollOffsetX, scrollOffsetY) = getScrollOffset(path);

            bool isScrollable = fiber.ComputedStyle.OverflowY == Overflow.Scroll
                || fiber.ComputedStyle.OverflowY == Overflow.Auto
                || fiber.ComputedStyle.OverflowX == Overflow.Scroll
                || fiber.ComputedStyle.OverflowX == Overflow.Auto;

            float childScrollX = scrollX + (isScrollable ? scrollOffsetX : 0);
            float childScrollY = scrollY + (isScrollable ? scrollOffsetY : 0);

            // position:fixed elements are in viewport space — zero out accumulated scroll so their
            // AbsoluteX/Y are compared directly against the viewport-space pointer position.
            var fiberPosition = fiber.ComputedStyle.Position ?? Position.Static;

            if (fiberPosition == Position.Fixed)
            { 
                scrollX = 0f;
                scrollY = 0f;
                childScrollX = 0f;
                childScrollY = 0f;
            }

            // Recurse into children — last child wins (later siblings paint on top in painter's order,
            // so fixed/high-ZIndex elements added later in the tree correctly win over earlier content).
            Fiber? childHit = null;
            int i = 0;
            for (var c = fiber.Child; c != null; c = c.Sibling, i++)
            {
                var hit = HitTest(c, x, y, path, i, childScrollX, childScrollY, getScrollOffset);
                
                if (hit != null)
                    childHit = hit;
            }

            if (childHit != null)
                return childHit;

            // Check this fiber (in visible coords: layout minus scroll)
            var layout = fiber.Layout;
            float vx = layout.AbsoluteX - scrollX;
            float vy = layout.AbsoluteY - scrollY;

            bool contains = x >= vx && x < vx + layout.Width && y >= vy && y < vy + layout.Height;

            if (contains && fiber.ComputedStyle.PointerEvents != PointerEvents.None)
                return fiber;

            // Siblings are handled by the parent's children loop above — no tail call needed.
            return null;
        }

        public static void InvokeKeyHandlers(Fiber fiber, KeyEvent keyEvent, bool capture)
        {
            var props = fiber.Props;
            switch (keyEvent.Type)
            {
                case KeyEventType.Down:
                    (capture 
                        ? props.OnKeyDownEventCapture
                        : props.OnKeyDownEvent
                    )?.Invoke(keyEvent);
                    break;
                case KeyEventType.Up:
                    (capture
                        ? props.OnKeyUpEventCapture
                        : props.OnKeyUpEvent
                    )?.Invoke(keyEvent);
                    break;
                case KeyEventType.Char:
                    (capture 
                        ? props.OnKeyCharCapture
                        : props.OnKeyChar
                    )?.Invoke(keyEvent);
                    break;
            }
        }

        public static bool IsFocusable(Fiber? fiber)
        {
            if (fiber == null)
                return false;

            var tabIndex = fiber.Props.TabIndex;
            
            if (tabIndex == -1)
                return true;

            if (IsTextInput(fiber.Type as string))
                return !fiber.Props.Disabled;
                
            var props = fiber.Props;

            return props.OnKeyDownEvent != null 
                || props.OnKeyUpEvent != null
                || props.OnKeyChar != null
                || props.OnChange != null
                || tabIndex != null;
        }

        /// <summary>Collects all focusable fibers from the tree in depth-first order.
        /// The caller is responsible for sorting by tabIndex if needed.</summary>
        public static void CollectFocusable(Fiber? fiber, List<Fiber> results)
        {
            if (fiber == null)
                return;

            if (IsFocusable(fiber))
                results.Add(fiber);

            CollectFocusable(fiber.Child, results);
            CollectFocusable(fiber.Sibling, results);
        }
    }
}