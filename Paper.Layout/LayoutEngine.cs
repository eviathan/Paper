using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Layout
{
    /// <summary>
    /// Walks a committed fiber tree and fills in <see cref="LayoutBox"/> for every fiber.
    /// Call <see cref="Layout"/> once per frame (after reconciler commits) to produce
    /// the layout pass that the renderer reads.
    /// </summary>
    public sealed class LayoutEngine
    {
        /// <summary>
        /// Optional: returns (width, height) in pixels for an image path. When set, Image elements
        /// with only width or only height get the other dimension from aspect ratio.
        /// </summary>
        public Func<string?, (float w, float h)?>? GetImageSize { get; set; }

        // Viewport dimensions captured at Layout() time so position:fixed elements can use them.
        private static float _viewportW;
        private static float _viewportH;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Lay out the entire fiber tree rooted at <paramref name="root"/>
        /// within a surface of <paramref name="width"/> × <paramref name="height"/> pixels.
        /// </summary>
        public void Layout(Fiber root, float width, float height, ILayoutMeasurer? measurer = null)
        {
            _viewportW = width;
            _viewportH = height;
            LayoutNode(root, 0f, 0f, width, height, measurer, GetImageSize);
            // Propagate absolute positions from root (parent 0,0,0,0) so renderer/hit-testing get surface-space coords.
            SetAbsolutePositions(root, 0f, 0f, 0f, 0f, width, height);
        }

        // ── Node layout ───────────────────────────────────────────────────────

        private static void LayoutNode(
            Fiber fiber,
            float x, float y,
            float containerWidth, float containerHeight,
            ILayoutMeasurer? measurer,
            Func<string?, (float w, float h)?>? getImageSize)
        {
            var style = fiber.ComputedStyle;

            if ((style.Display ?? Display.Block) == Display.None)
            {
                fiber.Layout = new LayoutBox { X = x, Y = y };
                return;
            }

            // Compute this node's outer size
            float outerW, outerH;
            var (rawW, rawH) = BoxModel.OuterSize(style, containerWidth, containerHeight);

            // Image: infer missing dimension from aspect ratio when only width or only height is set
            if (fiber.Type is string imgType && imgType == ElementTypes.Image && fiber.Props.Src is { } src && getImageSize != null)
            {
                var dim = getImageSize(src);
                if (dim.HasValue && dim.Value.w > 0 && dim.Value.h > 0)
                {
                    float aspect = dim.Value.w / dim.Value.h;
                    if (float.IsNaN(rawW) && !float.IsNaN(rawH))
                        rawW = rawH * aspect;
                    else if (!float.IsNaN(rawW) && float.IsNaN(rawH))
                        rawH = rawW / aspect;
                }
            }

            // Default block behaviour: full container width if width not specified
            outerW = float.IsNaN(rawW) ? containerWidth  : rawW;
            outerH = float.IsNaN(rawH) ? containerHeight : rawH;

            // Apply margin
            var (mt, mr, mb, ml) = BoxModel.MarginPixels(style, containerWidth, containerHeight);
            float layoutX = x + ml;
            float layoutY = y + mt;
            // Auto-size fills container minus margins; explicit size is the border-box (margins don't shrink it)
            float layoutW = float.IsNaN(rawW) ? Math.Max(0, outerW - ml - mr) : outerW;
            float layoutH = float.IsNaN(rawH) ? Math.Max(0, outerH - mt - mb) : outerH;

            fiber.Layout = new LayoutBox
            {
                X      = layoutX,
                Y      = layoutY,
                Width  = layoutW,
                Height = layoutH,
            };

            // Content area (inside padding + border)
            var (cx, cy) = BoxModel.ContentOrigin(style, layoutW, layoutH);
            var (cw, ch) = BoxModel.ContentSize(layoutW, layoutH, style, containerWidth, containerHeight);

            LayoutChildren(fiber, style, layoutX + cx, layoutY + cy, cw, ch, measurer, getImageSize);
        }

        // ── Children dispatch ─────────────────────────────────────────────────

        /// <summary>
        /// Internal: lay out all children of <paramref name="fiber"/> within the given content area.
        /// Called by FlexLayout and GridLayout as well (hence internal, not private).
        /// </summary>
        internal static void LayoutChildren(
            Fiber      fiber,
            StyleSheet style,
            float      contentX,
            float      contentY,
            float      contentWidth,
            float      contentHeight,
            ILayoutMeasurer? measurer,
            Func<string?, (float w, float h)?>? getImageSize)
        {
            switch (style.Display ?? Display.Block)
            {
                case Display.Flex:
                case Display.InlineFlex:
                    FlexLayout.Layout(fiber, style, contentX, contentY,
                        contentWidth, contentHeight, measurer, getImageSize);
                    break;

                case Display.Grid:
                    GridLayout.Layout(fiber, style, contentX, contentY,
                        contentWidth, contentHeight, getImageSize);
                    break;

                default:
                    // Block layout — children stack vertically
                    BlockLayout(fiber, style, contentX, contentY, contentWidth, contentHeight, measurer, getImageSize);
                    break;
            }

            // Absolute / fixed children are positioned relative to this fiber
            LayoutAbsoluteChildren(fiber, fiber.Layout.X, fiber.Layout.Y,
                fiber.Layout.Width, fiber.Layout.Height, contentX, contentY, contentWidth, contentHeight, measurer, getImageSize);
        }

        // ── Block layout ──────────────────────────────────────────────────────

        private static void BlockLayout(
            Fiber      container,
            StyleSheet style,
            float      contentX, float contentY,
            float      contentWidth, float contentHeight,
            ILayoutMeasurer? measurer,
            Func<string?, (float w, float h)?>? getImageSize)
        {
            float cursor = contentY;
            var child = container.Child;

            while (child != null)
            {
                var childStyle = child.ComputedStyle;
                if ((childStyle.Display ?? Display.Block) == Display.None || (childStyle.Position ?? Position.Static) == Position.Absolute || (childStyle.Position ?? Position.Static) == Position.Fixed)
                {
                    child = child.Sibling;
                    continue;
                }

                // In block layout children get the full content width;
                // height shrinks to fit (NaN → content sized → resolve to 0 initially then grow)
                var (rawW, rawH) = BoxModel.OuterSize(childStyle, contentWidth, contentHeight);
                // Image: infer missing dimension from aspect ratio
                if (child.Type is string imgType && imgType == ElementTypes.Image && child.Props.Src is { } src && getImageSize != null)
                {
                    var dim = getImageSize(src);
                    if (dim.HasValue && dim.Value.w > 0 && dim.Value.h > 0)
                    {
                        float aspect = dim.Value.w / dim.Value.h;
                        if (float.IsNaN(rawW) && !float.IsNaN(rawH))
                            rawW = rawH * aspect;
                        else if (!float.IsNaN(rawW) && float.IsNaN(rawH))
                            rawH = rawW / aspect;
                    }
                }
                // FlexGrow > 0 in a block parent: fill the container width (mirrors GetFlexBasis behaviour).
                // This handles component-wrapper fibers whose inner box has FlexGrow=1 but also an
                // explicit pixel Width merged in from user style (e.g. Slider track).
                bool childGrowsHoriz = (childStyle.FlexGrow ?? 0f) > 0f;
                float w = (float.IsNaN(rawW) || childGrowsHoriz) ? contentWidth : rawW;
                float h = float.IsNaN(rawH) ? 0f          : rawH;
                // When height is auto, use at least MinHeight so e.g. table rows get a usable height
                if (h <= 0.0001f && childStyle.MinHeight != null)
                    h = Math.Max(h, childStyle.MinHeight.Value.Resolve(contentHeight));

                // Intrinsic sizing for text-like nodes when height is auto/unspecified.
                if (h <= 0.0001f && measurer != null && child.Props.Text is { Length: > 0 } t)
                {
                    var (tw, th) = measurer.MeasureText(t, childStyle, w);
                    var pad = childStyle.Padding ?? Thickness.Zero;
                    float padW = pad.Left.Resolve(w) + pad.Right.Resolve(w);
                    float padH = pad.Top.Resolve(contentHeight) + pad.Bottom.Resolve(contentHeight);
                    // Inline elements shrink to text width; Block elements fill the container.
                    bool isInline = (childStyle.Display ?? Display.Block) == Display.Inline;
                    w = float.IsNaN(rawW) && isInline ? Math.Min(contentWidth, tw + padW) : w;
                    h = th + padH;
                }
                // Checkbox / RadioOption: intrinsic height so the 16px visual box isn't clipped.
                if (h <= 0.0001f && child.Type is string cbTypeStr &&
                    (cbTypeStr == ElementTypes.Checkbox || cbTypeStr == ElementTypes.RadioOption))
                    h = 20f;

                // Textarea / MarkdownEditor: measure the actual text content height so the box
                // auto-grows as lines are added. rows prop sets the minimum floor.
                if (measurer != null && child.Type is string typeStr &&
                    (typeStr == ElementTypes.Textarea || typeStr == ElementTypes.MarkdownEditor))
                {
                    var (_, lineH) = measurer.MeasureText("A", childStyle, 10);
                    var pad = childStyle.Padding ?? Thickness.Zero;
                    float padH = pad.Top.Resolve(contentHeight) + pad.Bottom.Resolve(contentHeight);
                    int minRows = child.Props.Rows is int r && r > 0 ? r : 2;
                    float minH = lineH * minRows + padH;
                    // Measure the actual content (respects \n line breaks + word wrap).
                    if (child.Props.Text is { Length: > 0 } ta)
                    {
                        var (_, th) = measurer.MeasureText(ta, childStyle, w);
                        minH = Math.Max(minH, th + padH);
                    }
                    h = Math.Max(h, minH);
                }

                var (mt, mr, mb, ml) = BoxModel.MarginPixels(childStyle, w, h);
                child.Layout = new LayoutBox
                {
                    X      = contentX + ml,
                    Y      = cursor   + mt,
                    // Auto-width fills container minus margins; explicit-width is the border-box size (unchanged by margins)
                    Width  = float.IsNaN(rawW) ? Math.Max(0, w - ml - mr) : w,
                    // Height is the border-box size; margins are purely position offsets, not size reductions
                    Height = h,
                };

                var (cx, cy) = BoxModel.ContentOrigin(childStyle, child.Layout.Width, child.Layout.Height);
                var (cw, ch) = BoxModel.ContentSize(child.Layout.Width, child.Layout.Height, childStyle, contentWidth, contentHeight);
                LayoutChildren(child, childStyle,
                    child.Layout.X + cx, child.Layout.Y + cy, cw, ch, measurer, getImageSize);

                cursor += child.Layout.Height + mt + mb;
                child = child.Sibling;
            }
        }

        // ── Absolute / Fixed positioning ──────────────────────────────────────

        private static void LayoutAbsoluteChildren(
            Fiber container,
            float containerX, float containerY,
            float containerW, float containerH,
            float contentX,   float contentY,
            float contentW,   float contentH,
            ILayoutMeasurer? measurer,
            Func<string?, (float w, float h)?>? getImageSize)
        {
            var child = container.Child;
            while (child != null)
            {
                var computedStyle = child.ComputedStyle;
                var pos = computedStyle.Position ?? Position.Static;
                if (pos == Position.Absolute)
                {
                    LayoutAbsoluteNode(child, computedStyle, containerX, containerY, containerW, containerH, measurer, getImageSize);
                }
                else if (pos == Position.Fixed)
                {
                    // position:fixed is relative to the viewport, not the containing block.
                    LayoutAbsoluteNode(child, computedStyle, 0f, 0f, _viewportW, _viewportH, measurer, getImageSize);
                }
                child = child.Sibling;
            }
        }

        private static void LayoutAbsoluteNode(
            Fiber      fiber,
            StyleSheet style,
            float      containingX, float containingY,
            float      containingW, float containingH,
            ILayoutMeasurer? measurer,
            Func<string?, (float w, float h)?>? getImageSize)
        {
            bool hasLeft   = style.Left   != null && !style.Left.Value.IsAuto;
            bool hasRight  = style.Right  != null && !style.Right.Value.IsAuto;
            bool hasTop    = style.Top    != null && !style.Top.Value.IsAuto;
            bool hasBottom = style.Bottom != null && !style.Bottom.Value.IsAuto;

            // Auto-width: when no explicit Width is set, use containingW for child layout
            // then shrink to content afterwards (like CSS shrink-to-fit for absolute elements).
            // Exception: both left+right anchors → stretch to fill the gap.
            bool wAuto = style.Width == null || style.Width.Value.IsAuto;
            float w;
            if (!wAuto)
                w = BoxModel.ResolveLength(style.Width, containingW, containingW);
            else if (hasLeft && hasRight)
                w = Math.Max(0, containingW
                    - style.Left!.Value.Resolve(containingW)
                    - style.Right!.Value.Resolve(containingW));
            else
                w = containingW;   // use full containing width for child layout; shrink below

            bool hAuto = style.Height == null || style.Height.Value.IsAuto;
            float h;
            if (!hAuto)
                h = BoxModel.ResolveLength(style.Height, containingH, 0f);
            else if (hasTop && hasBottom)
                h = Math.Max(0, containingH
                    - style.Top!.Value.Resolve(containingH)
                    - style.Bottom!.Value.Resolve(containingH));
            else
                h = 0f;

            float x = containingX;
            float y = containingY;

            if (hasLeft)
                x = containingX + style.Left!.Value.Resolve(containingW);
            else if (hasRight)
                x = containingX + containingW - w - style.Right!.Value.Resolve(containingW);

            if (hasTop)
                y = containingY + style.Top!.Value.Resolve(containingH);
            else if (hasBottom && !hAuto)
                y = containingY + containingH - h - style.Bottom!.Value.Resolve(containingH);
            // Bottom + hAuto: y is corrected after content height is known

            fiber.Layout = new LayoutBox { X = x, Y = y, Width = w, Height = h };

            var (cx, cy) = BoxModel.ContentOrigin(style, w, h);
            var (cw, ch) = BoxModel.ContentSize(w, h, style, containingW, containingH);
            LayoutChildren(fiber, style, x + cx, y + cy, cw, ch, measurer, getImageSize);

            // Auto-width shrink-to-fit: compute actual width from child extents.
            if (wAuto && !(hasLeft && hasRight))
            {
                float contentLeftX = x + cx;
                float maxRight = contentLeftX;
                var c = fiber.Child;
                while (c != null)
                {
                    var cp = c.ComputedStyle.Position ?? Position.Static;
                    if (cp != Position.Absolute && cp != Position.Fixed)
                    {
                        var (_, cmr, _, _) = BoxModel.MarginPixels(c.ComputedStyle, c.Layout.Width, c.Layout.Height);
                        float right = c.Layout.X + c.Layout.Width + cmr;
                        if (right > maxRight) maxRight = right;
                    }
                    c = c.Sibling;
                }
                float contentUsed = Math.Max(0, maxRight - contentLeftX);
                var (_, brr, _, bll) = BoxModel.BorderWidths(style);
                var (_, prr, _, pll) = BoxModel.PaddingPixels(style, w, 0f);
                float computedW = contentUsed + pll + prr + bll + brr;
                float minW = BoxModel.ResolveLength(style.MinWidth, containingW, 0f);
                float maxW = BoxModel.ResolveLength(style.MaxWidth, containingW, float.MaxValue);
                computedW = Math.Clamp(computedW, minW, maxW < float.MaxValue ? maxW : float.MaxValue);

                // Re-anchor right-anchored elements now that true width is known
                if (!hasLeft && hasRight)
                    x = containingX + containingW - computedW - style.Right!.Value.Resolve(containingW);

                var lbw = fiber.Layout;
                lbw.X = x;
                lbw.Width = computedW;
                fiber.Layout = lbw;
            }

            if (hAuto)
            {
                // FlexLayout may have already grown the height via its shrink-to-content pass.
                h = fiber.Layout.Height;

                // If layout didn't grow it (e.g. Block children), compute from child extents.
                if (h <= 0.001f)
                {
                    float contentTopY = y + cy;
                    float maxBot = contentTopY;
                    var c = fiber.Child;
                    while (c != null)
                    {
                        var cp = c.ComputedStyle.Position ?? Position.Static;
                        if (cp != Position.Absolute && cp != Position.Fixed)
                        {
                            var (_, _, cmb, _) = BoxModel.MarginPixels(c.ComputedStyle, c.Layout.Width, c.Layout.Height);
                            float bot = c.Layout.Y + c.Layout.Height + cmb;
                            if (bot > maxBot) maxBot = bot;
                        }
                        c = c.Sibling;
                    }
                    float contentUsed = maxBot - contentTopY;
                    var (_, _, bb, _) = BoxModel.BorderWidths(style);
                    var (_, _, pb, _) = BoxModel.PaddingPixels(style, w, 0f);
                    h = contentUsed + pb + bb;
                }

                // For bottom-anchored auto-height elements, recompute y now that h is known.
                if (hasBottom && !hasTop)
                    y = containingY + containingH - h - style.Bottom!.Value.Resolve(containingH);

                var layoutBox = fiber.Layout;
                layoutBox.Y = y;
                layoutBox.Height = h;
                fiber.Layout = layoutBox;
            }
        }

        // ── Absolute coordinates pass ─────────────────────────────────────────
        // Layout.X/Y are in parent's content space; propagate so Absolute is in root/surface space.

        private static void SetAbsolutePositions(Fiber fiber, float parentAbsX, float parentAbsY, float parentLayoutX, float parentLayoutY, float parentW = 0f, float parentH = 0f)
        {
            var layoutBox = fiber.Layout;
            var computedStyle = fiber.ComputedStyle;
            var pos = computedStyle.Position ?? Position.Static;

            if (pos == Position.Fixed)
            {
                // position:fixed is in viewport space — layoutBox.X/Y already computed relative to (0,0).
                layoutBox.AbsoluteX = layoutBox.X;
                layoutBox.AbsoluteY = layoutBox.Y;
            }
            else
            {
                layoutBox.AbsoluteX = parentAbsX + layoutBox.X - parentLayoutX;
                layoutBox.AbsoluteY = parentAbsY + layoutBox.Y - parentLayoutY;
            }

            // position: relative — apply Top/Left/Right/Bottom as purely visual offsets (no flow change)
            if (pos == Position.Relative)
            {
                if (computedStyle.Left != null && !computedStyle.Left.Value.IsAuto)
                    layoutBox.AbsoluteX += computedStyle.Left.Value.Resolve(parentW);
                else if (computedStyle.Right != null && !computedStyle.Right.Value.IsAuto)
                    layoutBox.AbsoluteX -= computedStyle.Right.Value.Resolve(parentW);

                if (computedStyle.Top != null && !computedStyle.Top.Value.IsAuto)
                    layoutBox.AbsoluteY += computedStyle.Top.Value.Resolve(parentH);
                else if (computedStyle.Bottom != null && !computedStyle.Bottom.Value.IsAuto)
                    layoutBox.AbsoluteY -= computedStyle.Bottom.Value.Resolve(parentH);
            }

            fiber.Layout = layoutBox;

            var child = fiber.Child;
            while (child != null)
            {
                SetAbsolutePositions(child, layoutBox.AbsoluteX, layoutBox.AbsoluteY, layoutBox.X, layoutBox.Y, layoutBox.Width, layoutBox.Height);
                child = child.Sibling;
            }
        }
    }
}
