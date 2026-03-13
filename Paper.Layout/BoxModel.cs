using Paper.Core.Styles;

namespace Paper.Layout
{
    /// <summary>
    /// Resolves the CSS box model for a given element.
    /// Paper defaults to <see cref="BoxSizing.BorderBox"/> (IE model):
    /// Width/Height include padding and border.
    /// </summary>
    public static class BoxModel
    {
        /// <summary>
        /// Resolve border widths on each side. Returns (top, right, bottom, left) in pixels.
        /// </summary>
        public static (float top, float right, float bottom, float left) BorderWidths(StyleSheet style)
        {
            if (style.Border == null)
                return (0, 0, 0, 0);

            return (
                style.Border.Top.Width,
                style.Border.Right.Width,
                style.Border.Bottom.Width,
                style.Border.Left.Width
            );
        }

        /// <summary>
        /// Resolve padding on each side in pixels, given the container's dimensions.
        /// Individual PaddingTop/Right/Bottom/Left override the corresponding side from Padding when set.
        /// </summary>
        public static (float top, float right, float bottom, float left) PaddingPixels(
            StyleSheet style, float containerWidth, float containerHeight)
        {
            // Percentage padding resolves against the containing block width (CSS spec)
            var p = style.Padding ?? Thickness.Zero;
            return (
                style.PaddingTop    != null ? style.PaddingTop.Value.Resolve(containerWidth)    : p.Top.Resolve(containerWidth),
                style.PaddingRight  != null ? style.PaddingRight.Value.Resolve(containerWidth)  : p.Right.Resolve(containerWidth),
                style.PaddingBottom != null ? style.PaddingBottom.Value.Resolve(containerWidth) : p.Bottom.Resolve(containerWidth),
                style.PaddingLeft   != null ? style.PaddingLeft.Value.Resolve(containerWidth)   : p.Left.Resolve(containerWidth)
            );
        }

        /// <summary>
        /// Resolve margin on each side in pixels.
        /// Individual MarginTop/Right/Bottom/Left override the corresponding side from Margin when set.
        /// </summary>
        public static (float top, float right, float bottom, float left) MarginPixels(
            StyleSheet style, float containerWidth, float containerHeight)
        {
            var m = style.Margin ?? Thickness.Zero;
            return (
                style.MarginTop    != null ? style.MarginTop.Value.Resolve(containerWidth)    : m.Top.Resolve(containerWidth),
                style.MarginRight  != null ? style.MarginRight.Value.Resolve(containerWidth)  : m.Right.Resolve(containerWidth),
                style.MarginBottom != null ? style.MarginBottom.Value.Resolve(containerWidth) : m.Bottom.Resolve(containerWidth),
                style.MarginLeft   != null ? style.MarginLeft.Value.Resolve(containerWidth)   : m.Left.Resolve(containerWidth)
            );
        }

        /// <summary>
        /// Given an outer size (including border+padding in border-box mode) and a style,
        /// returns the content area dimensions.
        /// </summary>
        public static (float contentWidth, float contentHeight) ContentSize(
            float outerWidth, float outerHeight, StyleSheet style, float containerWidth, float containerHeight)
        {
            var (bt, br, bb, bl) = BorderWidths(style);
            var (pt, pr, pb, pl) = PaddingPixels(style, containerWidth, containerHeight);

            if ((style.BoxSizing ?? BoxSizing.BorderBox) == BoxSizing.BorderBox)
            {
                return (
                    Math.Max(0, outerWidth  - pl - pr - bl - br),
                    Math.Max(0, outerHeight - pt - pb - bt - bb)
                );
            }
            else // ContentBox
            {
                return (outerWidth, outerHeight);
            }
        }

        /// <summary>
        /// Given a style and available container space, determine the outer size of this element.
        /// Returns NaN for "auto" / shrink-to-content cases that need the flex/grid algorithm.
        /// </summary>
        public static (float outerWidth, float outerHeight) OuterSize(
            StyleSheet style, float containerWidth, float containerHeight)
        {
            float w = ResolveLength(style.Width,  containerWidth,  float.NaN);
            float h = ResolveLength(style.Height, containerHeight, float.NaN);

            // Clamp to min/max
            if (!float.IsNaN(w))
            {
                float minW = ResolveLength(style.MinWidth, containerWidth, 0f);
                float maxW = ResolveLength(style.MaxWidth, containerWidth, float.MaxValue);
                w = Math.Clamp(w, minW, maxW);
            }

            if (!float.IsNaN(h))
            {
                float minH = ResolveLength(style.MinHeight, containerHeight, 0f);
                float maxH = ResolveLength(style.MaxHeight, containerHeight, float.MaxValue);
                h = Math.Clamp(h, minH, maxH);
            }

            return (w, h);
        }

        /// <summary>
        /// Resolve a nullable Length to pixels. Returns <paramref name="fallback"/> if null or auto.
        /// </summary>
        public static float ResolveLength(Length? length, float containerSize, float fallback) =>
            length == null || length.Value.IsAuto ? fallback : length.Value.Resolve(containerSize);

        /// <summary>
        /// Total horizontal spacing consumed by padding + border.
        /// </summary>
        public static float HorizontalInsets(StyleSheet style, float containerWidth)
        {
            var (_, br, _, bl) = BorderWidths(style);
            var (_, pr, _, pl) = PaddingPixels(style, containerWidth, 0);
            return pl + pr + bl + br;
        }

        /// <summary>
        /// Total vertical spacing consumed by padding + border.
        /// </summary>
        public static float VerticalInsets(StyleSheet style, float containerHeight)
        {
            var (bt, _, bb, _) = BorderWidths(style);
            var (pt, _, pb, _) = PaddingPixels(style, 0, containerHeight);
            return pt + pb + bt + bb;
        }

        /// <summary>
        /// Returns the top-left content origin (relative to the element's own top-left) in pixels.
        /// </summary>
        public static (float x, float y) ContentOrigin(
            StyleSheet style, float containerWidth, float containerHeight)
        {
            var (bt, br, bb, bl) = BorderWidths(style);
            var (pt, pr, pb, pl) = PaddingPixels(style, containerWidth, containerHeight);
            return (pl + bl, pt + bt);
        }
    }
}
