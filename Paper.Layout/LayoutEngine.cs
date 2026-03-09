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

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Lay out the entire fiber tree rooted at <paramref name="root"/>
        /// within a surface of <paramref name="width"/> × <paramref name="height"/> pixels.
        /// </summary>
        public void Layout(Fiber root, float width, float height, ILayoutMeasurer? measurer = null)
        {
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
            var (mt, mr, mb, ml) = BoxModel.MarginPx(style, containerWidth, containerHeight);
            float layoutX = x + ml;
            float layoutY = y + mt;
            float layoutW = Math.Max(0, outerW - ml - mr);
            float layoutH = Math.Max(0, outerH - mt - mb);

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
                float w = float.IsNaN(rawW) ? contentWidth : rawW;
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
                    w = float.IsNaN(rawW) ? Math.Min(contentWidth, tw + padW) : w;
                    h = th + padH;
                }
                // Textarea with rows: height = rows * lineHeight + padding
                if (h <= 0.0001f && measurer != null && child.Type is string typeStr && typeStr == ElementTypes.Textarea && child.Props.Rows is int rows && rows > 0)
                {
                    var (_, lineH) = measurer.MeasureText("A", childStyle, 10);
                    var pad = childStyle.Padding ?? Thickness.Zero;
                    float padH = pad.Top.Resolve(contentHeight) + pad.Bottom.Resolve(contentHeight);
                    h = lineH * rows + padH;
                }

                var (mt, mr, mb, ml) = BoxModel.MarginPx(childStyle, w, h);
                child.Layout = new LayoutBox
                {
                    X      = contentX + ml,
                    Y      = cursor   + mt,
                    Width  = Math.Max(0, w - ml - mr),
                    Height = Math.Max(0, h - mt - mb),
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
                var s = child.ComputedStyle;
                if ((s.Position ?? Position.Static) == Position.Absolute || (s.Position ?? Position.Static) == Position.Fixed)
                {
                    LayoutAbsoluteNode(child, s, containerX, containerY, containerW, containerH, measurer, getImageSize);
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
            float w = BoxModel.ResolveLength(style.Width,  containingW, containingW);
            float h = BoxModel.ResolveLength(style.Height, containingH, containingH);

            float x = containingX;
            float y = containingY;

            bool hasLeft   = style.Left   != null && !style.Left.Value.IsAuto;
            bool hasRight  = style.Right  != null && !style.Right.Value.IsAuto;
            bool hasTop    = style.Top    != null && !style.Top.Value.IsAuto;
            bool hasBottom = style.Bottom != null && !style.Bottom.Value.IsAuto;

            if (hasLeft)
                x = containingX + style.Left!.Value.Resolve(containingW);
            else if (hasRight)
                x = containingX + containingW - w - style.Right!.Value.Resolve(containingW);

            if (hasTop)
                y = containingY + style.Top!.Value.Resolve(containingH);
            else if (hasBottom)
                y = containingY + containingH - h - style.Bottom!.Value.Resolve(containingH);

            fiber.Layout = new LayoutBox { X = x, Y = y, Width = w, Height = h };

            var (cx, cy) = BoxModel.ContentOrigin(style, w, h);
            var (cw, ch) = BoxModel.ContentSize(w, h, style, containingW, containingH);
            LayoutChildren(fiber, style, x + cx, y + cy, cw, ch, measurer, getImageSize);
        }

        // ── Absolute coordinates pass ─────────────────────────────────────────
        // Layout.X/Y are in parent's content space; propagate so Absolute is in root/surface space.

        private static void SetAbsolutePositions(Fiber fiber, float parentAbsX, float parentAbsY, float parentLayoutX, float parentLayoutY, float parentW = 0f, float parentH = 0f)
        {
            var lb = fiber.Layout;
            lb.AbsoluteX = parentAbsX + lb.X - parentLayoutX;
            lb.AbsoluteY = parentAbsY + lb.Y - parentLayoutY;

            // position: relative — apply Top/Left/Right/Bottom as purely visual offsets (no flow change)
            var s = fiber.ComputedStyle;
            if ((s.Position ?? Position.Static) == Position.Relative)
            {
                if (s.Left != null && !s.Left.Value.IsAuto)
                    lb.AbsoluteX += s.Left.Value.Resolve(parentW);
                else if (s.Right != null && !s.Right.Value.IsAuto)
                    lb.AbsoluteX -= s.Right.Value.Resolve(parentW);

                if (s.Top != null && !s.Top.Value.IsAuto)
                    lb.AbsoluteY += s.Top.Value.Resolve(parentH);
                else if (s.Bottom != null && !s.Bottom.Value.IsAuto)
                    lb.AbsoluteY -= s.Bottom.Value.Resolve(parentH);
            }

            fiber.Layout = lb;

            var child = fiber.Child;
            while (child != null)
            {
                SetAbsolutePositions(child, lb.AbsoluteX, lb.AbsoluteY, lb.X, lb.Y, lb.Width, lb.Height);
                child = child.Sibling;
            }
        }
    }
}
