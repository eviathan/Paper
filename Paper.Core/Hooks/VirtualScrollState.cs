using Paper.Core.Events;

namespace Paper.Core.Hooks
{
    /// <summary>
    /// Result of <see cref="Hooks.UseVirtualScroll{T}"/>.
    /// Contains the visible item slice, spacer heights, and a wheel handler to wire up.
    /// </summary>
    public sealed class VirtualScrollState<T>
    {
        /// <summary>The items currently visible (plus overscan), each paired with its original index.</summary>
        public IReadOnlyList<(int index, T item)> VisibleItems { get; }

        /// <summary>Height of the top spacer box in pixels (represents all items above the visible window).</summary>
        public float PaddingTop { get; }

        /// <summary>Height of the bottom spacer box in pixels (represents all items below the visible window).</summary>
        public float PaddingBottom { get; }

        /// <summary>Total height of all items combined — use as the height of the outer container box.</summary>
        public float TotalHeight { get; }

        /// <summary>
        /// Wire this to the scroll container's <c>onWheel</c> prop.
        /// It updates the internal scroll position and requests a re-render.
        /// </summary>
        public Action<PointerEvent> OnWheel { get; }

        internal VirtualScrollState(
            IReadOnlyList<(int, T)> visibleItems,
            float paddingTop,
            float paddingBottom,
            float totalHeight,
            Action<PointerEvent> onWheel)
        {
            VisibleItems   = visibleItems;
            PaddingTop     = paddingTop;
            PaddingBottom  = paddingBottom;
            TotalHeight    = totalHeight;
            OnWheel        = onWheel;
        }
    }
}
