using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Rendering.Silk.NET.Utilities
{
    /// <summary>Spatial hit-testing and focus/interaction target resolution.</summary>
    public static class HitTestUtility
    {
        /// <summary>Walk the fiber tree depth-first and return the deepest fiber at (<paramref name="x"/>, <paramref name="y"/>).
        /// Uses path and scroll offset so hit-testing matches visible (scrolled) positions.</summary>
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

            // Recurse into children — last child wins (later siblings paint on top in painter's order).
            Fiber? childHit = null;
            int childIndex = 0;
            for (var childFiber = fiber.Child; childFiber != null; childFiber = childFiber.Sibling, childIndex++)
            {
                var hit = HitTest(childFiber, x, y, path, childIndex, childScrollX, childScrollY, getScrollOffset);

                if (hit != null)
                    childHit = hit;
            }

            if (childHit != null)
                return childHit;

            // Check this fiber (in visible coords: layout minus scroll)
            var layout = fiber.Layout;
            float visibleX = layout.AbsoluteX - scrollX;
            float visibleY = layout.AbsoluteY - scrollY;

            bool contains = x >= visibleX && x < visibleX + layout.Width && y >= visibleY && y < visibleY + layout.Height;

            if (contains && fiber.ComputedStyle.PointerEvents != PointerEvents.None)
                return fiber;

            return null;
        }

        /// <summary>Returns the fiber that should receive the click: the first (deepest) ancestor that has
        /// an OnClick, OnDoubleClick, or OnCheckedChange handler.</summary>
        public static Fiber? GetClickTarget(Fiber? target)
        {
            for (var fiber = target; fiber != null; fiber = fiber.Parent)
                if (fiber.Props.OnClick != null || fiber.Props.OnDoubleClick != null || fiber.Props.OnCheckedChange != null)
                    return fiber;

            return null;
        }

        /// <summary>Returns true if <paramref name="fiber"/> should participate in tab-focus order.</summary>
        public static bool IsFocusable(Fiber? fiber)
        {
            if (fiber == null)
                return false;

            var tabIndex = fiber.Props.TabIndex;

            if (tabIndex == -1)
                return true;

            if (InputTextUtility.IsTextInput(fiber.Type as string))
                return !fiber.Props.Disabled;

            var props = fiber.Props;

            return props.OnKeyDownEvent != null
                || props.OnKeyUpEvent != null
                || props.OnKeyChar != null
                || props.OnChange != null
                || tabIndex != null;
        }

        /// <summary>Collects all focusable fibers from the tree in depth-first order.</summary>
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
