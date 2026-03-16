using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Layout
{
    /// <summary>
    /// Implements CSS Flexbox layout for a container fiber.
    /// </summary>
    internal static class FlexLayout
    {
        /// <summary>
        /// Lay out the flex items of <paramref name="container"/> within the given content area.
        /// </summary>
        public static void Layout(
            Fiber container,
            StyleSheet style,
            float contentX,
            float contentY,
            float contentWidth,
            float contentHeight,
            ILayoutMeasurer? measurer,
            Func<string?, (float w, float h)?>? getImageSize = null)
        {
            var flexDir = style.FlexDirection ?? FlexDirection.Row;
            bool isRow = flexDir is FlexDirection.Row or FlexDirection.RowReverse;
            bool reversed = flexDir is FlexDirection.RowReverse or FlexDirection.ColumnReverse;

            float mainSize = isRow ? contentWidth : contentHeight;
            float crossSize = isRow ? contentHeight : contentWidth;

            var gapLength = isRow ? style.ColumnGap : style.RowGap;
            if (gapLength == null && style.Gap != null)
                gapLength = style.Gap;
            float gap = BoxModel.ResolveLength(gapLength, mainSize, 0f);

            // Collect flex items (non-absolute, non-none children)
            var items = CollectFlexItems(container);
            if (items.Count == 0) return;

            bool doWrap = (style.FlexWrap ?? FlexWrap.NoWrap) != FlexWrap.NoWrap;

            // Overflow:scroll/auto in the main axis direction: items should not be shrunk below their
            // natural size — they should overflow and be scrolled. Disable flex-shrink for the line.
            bool mainAxisScrollable = isRow
                ? (style.OverflowX == Overflow.Scroll || style.OverflowX == Overflow.Auto)
                : (style.OverflowY == Overflow.Scroll || style.OverflowY == Overflow.Auto);

            // Intrinsic main size: when a column (or row) flex container has auto main-axis size and
            // mainSize is 0 (i.e. a block parent gave it 0 height), don't shrink items — let the
            // shrink-to-content code at the end grow the container to its natural content size.
            bool isIntrinsicMain = mainSize <= 0.001f && (
                !isRow ? (style.Height == null || style.Height.Value.IsAuto)
                       : (style.Width  == null || style.Width.Value.IsAuto));

            float usedCrossTotal;
            float usedMainTotal;

            if (!doWrap)
            {
                // Single-line layout
                var line = BuildLine(items, style, mainSize, crossSize, gap, measurer, isRow, allowShrink: !mainAxisScrollable && !isIntrinsicMain);
                usedCrossTotal = line.LineCrossSize;
                usedMainTotal = line.Items.Sum(i => i.FinalMain) + Math.Max(0, line.Items.Count - 1) * gap;
                PositionLine(line, style, contentX, contentY, contentWidth, contentHeight,
                             mainSize, crossSize, gap, isRow, reversed, measurer, getImageSize);
            }
            else
            {
                // Multi-line: pack items into lines
                var lines = BuildLines(items, style, mainSize, crossSize, gap, measurer, isRow);
                usedCrossTotal = lines.Sum(l => l.LineCrossSize) + Math.Max(0, lines.Count - 1) * gap;
                usedMainTotal = lines.Sum(l =>
                    l.Items.Sum(i => i.FinalMain) + Math.Max(0, l.Items.Count - 1) * gap)
                    + Math.Max(0, lines.Count - 1) * gap;
                PositionLines(lines, style, contentX, contentY, contentWidth, contentHeight,
                              mainSize, crossSize, gap, isRow, reversed, measurer, getImageSize);
            }

            // Recompute main size from actual children Layout (e.g. wrap row may have grown)
            usedMainTotal = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float main = isRow ? item.Layout.Width : item.Layout.Height;
                float mt = 0, mr = 0, mb = 0, ml = 0;
                if (item.ComputedStyle != null)
                {
                    (mt, mr, mb, ml) = BoxModel.MarginPixels(item.ComputedStyle, item.Layout.Width, item.Layout.Height);
                }
                float marginMain = isRow ? (ml + mr) : (mt + mb);
                usedMainTotal += (i > 0 ? gap : 0) + marginMain + main;
            }

            // Shrink-to-content: when main/cross size is auto, set container's Layout so the next sibling
            // is positioned correctly. Only expand to fit content — never shrink below parent-given size.
            // Row: main=width cross=height. Column: main=height cross=width.
            if (style == null) return;
            var lb = container.Layout;
            if (isRow)
            {
                if (style.Height == null || style.Height.Value.IsAuto)
                {
                    float vert = BoxModel.VerticalInsets(style, usedCrossTotal);
                    lb.Height = Math.Max(lb.Height, usedCrossTotal + vert);
                }
                if (style.Width == null || style.Width.Value.IsAuto)
                {
                    float horiz = BoxModel.HorizontalInsets(style, usedMainTotal);
                    lb.Width = Math.Max(lb.Width, usedMainTotal + horiz);
                }
            }
            else
            {
                // Column: main=height, cross=width
                if (style.Height == null || style.Height.Value.IsAuto)
                {
                    float vert = BoxModel.VerticalInsets(style, usedMainTotal);
                    lb.Height = Math.Max(lb.Height, usedMainTotal + vert);
                }
                if (style.Width == null || style.Width.Value.IsAuto)
                {
                    float horiz = BoxModel.HorizontalInsets(style, usedCrossTotal);
                    lb.Width = Math.Max(lb.Width, usedCrossTotal + horiz);
                }
            }
            container.Layout = lb;
        }

        // ── Item collection ───────────────────────────────────────────────────

        private static List<Fiber> CollectFlexItems(Fiber container)
        {
            var items = new List<Fiber>();
            var child = container.Child;
            while (child != null)
            {
                var s = child.ComputedStyle;
                var display = s.Display ?? Display.Block;
                var position = s.Position ?? Position.Static;
                if (display != Display.None && position != Position.Absolute && position != Position.Fixed)
                    items.Add(child);
                child = child.Sibling;
            }
            return items;
        }

        // ── Single-line build ─────────────────────────────────────────────────

        private static FlexLine BuildLine(
            List<Fiber> items, StyleSheet style, float mainSize, float crossSize,
            float gap, ILayoutMeasurer? measurer, bool isRow, bool allowShrink = true)
        {
            var line = new FlexLine();

            // Step 1: compute hypothetical main sizes
            foreach (var item in items)
            {
                var itemStyle = item.ComputedStyle;
                float base_ = GetFlexBasis(item, itemStyle, mainSize, crossSize, isRow, measurer);
                line.Items.Add(new FlexItem { Fiber = item, HypotheticalMain = base_ });
            }

            // Step 2: compute free space
            float totalGap = Math.Max(0, items.Count - 1) * gap;
            float usedMain = line.Items.Sum(i => i.HypotheticalMain) + totalGap;
            float freeSpace = mainSize - usedMain;

            // Step 3: resolve grows / shrinks
            if (freeSpace > 0.001f)
            {
                float totalGrow = line.Items.Sum(i => i.Fiber.ComputedStyle.FlexGrow ?? 0f);
                if (totalGrow > 0)
                {
                    float perUnit = freeSpace / totalGrow;
                    foreach (var fi in line.Items)
                    {
                        float grow = fi.Fiber.ComputedStyle.FlexGrow ?? 0f;
                        fi.FinalMain = fi.HypotheticalMain + grow * perUnit;
                    }
                }
                else
                {
                    foreach (var fi in line.Items) fi.FinalMain = fi.HypotheticalMain;
                }
            }
            else if (freeSpace < -0.001f && allowShrink)
            {
                float totalShrink = line.Items.Sum(i =>
                    (i.Fiber.ComputedStyle.FlexShrink ?? 1f) * i.HypotheticalMain);
                if (totalShrink > 0)
                {
                    foreach (var fi in line.Items)
                    {
                        var s = fi.Fiber.ComputedStyle;
                        float scaled = (s.FlexShrink ?? 1f) * fi.HypotheticalMain;
                        fi.FinalMain = fi.HypotheticalMain + (scaled / totalShrink) * freeSpace;
                    }
                }
                else
                {
                    foreach (var fi in line.Items) fi.FinalMain = fi.HypotheticalMain;
                }
            }
            else
            {
                foreach (var fi in line.Items) fi.FinalMain = fi.HypotheticalMain;
            }

            // Step 3b: clamp FinalMain to [MinMain, MaxMain]
            foreach (var fi in line.Items)
            {
                var s = fi.Fiber.ComputedStyle;
                float minMain = isRow
                    ? (s.MinWidth  != null && !s.MinWidth.Value.IsAuto  ? s.MinWidth.Value.Resolve(mainSize)  : 0f)
                    : (s.MinHeight != null && !s.MinHeight.Value.IsAuto ? s.MinHeight.Value.Resolve(mainSize) : 0f);
                float maxMain = isRow
                    ? (s.MaxWidth  != null && !s.MaxWidth.Value.IsAuto  ? s.MaxWidth.Value.Resolve(mainSize)  : float.MaxValue)
                    : (s.MaxHeight != null && !s.MaxHeight.Value.IsAuto ? s.MaxHeight.Value.Resolve(mainSize) : float.MaxValue);
                fi.FinalMain = Math.Max(minMain, Math.Min(maxMain, fi.FinalMain));
            }

            // Step 4: compute cross sizes — effective alignment determines whether item stretches or shrinks to content
            foreach (var fi in line.Items)
            {
                var itemStyle = fi.Fiber.ComputedStyle;
                var effectiveAlign = EffectiveAlign(itemStyle, style);
                fi.FinalCross = GetCrossSize(fi.Fiber, itemStyle, fi.FinalMain, crossSize, isRow, measurer, effectiveAlign);
                // Clamp cross size too
                float minCross = isRow
                    ? (itemStyle.MinHeight != null && !itemStyle.MinHeight.Value.IsAuto ? itemStyle.MinHeight.Value.Resolve(crossSize) : 0f)
                    : (itemStyle.MinWidth  != null && !itemStyle.MinWidth.Value.IsAuto  ? itemStyle.MinWidth.Value.Resolve(crossSize)  : 0f);
                float maxCross = isRow
                    ? (itemStyle.MaxHeight != null && !itemStyle.MaxHeight.Value.IsAuto ? itemStyle.MaxHeight.Value.Resolve(crossSize) : float.MaxValue)
                    : (itemStyle.MaxWidth  != null && !itemStyle.MaxWidth.Value.IsAuto  ? itemStyle.MaxWidth.Value.Resolve(crossSize)  : float.MaxValue);
                fi.FinalCross = Math.Max(minCross, Math.Min(maxCross, fi.FinalCross));
                line.LineCrossSize = Math.Max(line.LineCrossSize, fi.FinalCross);
            }

            return line;
        }

        private static void PositionLine(
            FlexLine line, StyleSheet style,
            float contentX, float contentY,
            float contentWidth, float contentHeight,
            float mainSize, float crossSize,
            float gap, bool isRow, bool reversed,
            ILayoutMeasurer? measurer = null,
            Func<string?, (float w, float h)?>? getImageSize = null)
        {
            float totalGap = Math.Max(0, line.Items.Count - 1) * gap;
            float usedMain = line.Items.Sum(i => i.FinalMain);
            float freeSpace = mainSize - usedMain - totalGap;

            // Justify-content positions along the main axis
            (float offset, float spacing) = JustifyOffset(style.JustifyContent ?? JustifyContent.FlexStart, line.Items.Count, freeSpace, gap);

            // For reversed, main-start is the right/bottom edge. Keep items in original order and place from the right
            // so the first item (A) gets the rightmost slot; renderer draws in tree order so we see C B A.
            // (Reversing the list would assign C to right, but then drawing A,B,C gives A left, B mid, C right = wrong.)
            float cursor = reversed && isRow ? contentX + mainSize
                        : reversed && !isRow ? contentY + mainSize
                        : (isRow ? contentX : contentY) + offset;

            foreach (var fi in line.Items)
            {
                // Use at least LineCrossSize so CENTER/FlexEnd items in auto-height containers
                // (where crossSize arrives as 0) don't receive a negative offset.
                float effectiveCross = Math.Max(crossSize, line.LineCrossSize);
                float cross = AlignCross(fi, style, effectiveCross);

                float x, y, w, h;
                if (isRow)
                {
                    x = reversed ? cursor - fi.FinalMain : cursor;
                    y = contentY + cross;
                    w = fi.FinalMain;
                    h = fi.FinalCross;
                }
                else
                {
                    x = contentX + cross;
                    y = reversed ? cursor - fi.FinalMain : cursor;
                    w = fi.FinalCross;
                    h = fi.FinalMain;
                }

                // Clamp to container bounds
                w = Math.Max(0, w);
                h = Math.Max(0, h);

                // Apply margin
                var itemStyle = fi.Fiber.ComputedStyle;
                float mt = 0, mr = 0, mb = 0, ml = 0;
                if (itemStyle != null)
                    (mt, mr, mb, ml) = BoxModel.MarginPixels(itemStyle, w, h);
                x += ml;
                y += mt;
                // For stretch items in auto-height containers (crossSize=0), effectiveCross gives the
                // actual line cross size (driven by the tallest sibling). Use that instead of a fixed fallback.
                if (h <= 0) h = effectiveCross > 0 ? effectiveCross : 40f;

                fi.Fiber.Layout = new LayoutBox { X = x, Y = y, Width = w, Height = h };

                // Recurse — lay out this item's children (may update Layout.Height/Width when child is flex with wrap)
                LayoutItemChildren(fi.Fiber, w, h, measurer, getImageSize);

                // Advance by actual laid-out main size so wrapped/growing content pushes following siblings
                float mainUsed = isRow ? fi.Fiber.Layout.Width : fi.Fiber.Layout.Height;
                float mt2 = 0, mr2 = 0, mb2 = 0, ml2 = 0;
                if (itemStyle != null)
                    (mt2, mr2, mb2, ml2) = BoxModel.MarginPixels(itemStyle, fi.Fiber.Layout.Width, fi.Fiber.Layout.Height);
                float marginMain = isRow ? (ml2 + mr2) : (mt2 + mb2);
                cursor += reversed ? -(mainUsed + marginMain + spacing) : (mainUsed + marginMain + spacing);
            }
        }

        // ── Multi-line layout ─────────────────────────────────────────────────

        private static List<FlexLine> BuildLines(
            List<Fiber> items, StyleSheet style, float mainSize, float crossSize,
            float gap, ILayoutMeasurer? measurer, bool isRow)
        {
            var lines = new List<FlexLine>();
            var current = new FlexLine();

            float usedMain = 0;

            foreach (var item in items)
            {
                var itemStyle = item.ComputedStyle;
                float itemMain = GetFlexBasis(item, itemStyle, mainSize, crossSize, isRow, measurer);
                float addGap = current.Items.Count > 0 ? gap : 0;

                if (usedMain + addGap + itemMain > mainSize && current.Items.Count > 0)
                {
                    lines.Add(current);
                    current = new FlexLine();
                    usedMain = 0;
                }

                var fi = new FlexItem { Fiber = item, HypotheticalMain = itemMain };
                fi.FinalMain = itemMain;
                current.Items.Add(fi);
                usedMain += addGap + itemMain;
            }

            if (current.Items.Count > 0)
                lines.Add(current);

            // Apply grow/shrink and cross sizes per line
            foreach (var line in lines)
            {
                float totalGap = Math.Max(0, line.Items.Count - 1) * gap;
                float usedMain2 = line.Items.Sum(i => i.HypotheticalMain) + totalGap;
                float freeSpace2 = mainSize - usedMain2;

                if (freeSpace2 > 0.001f)
                {
                    float totalGrow = line.Items.Sum(i => i.Fiber.ComputedStyle.FlexGrow ?? 0f);
                    if (totalGrow > 0)
                    {
                        float perUnit = freeSpace2 / totalGrow;
                        foreach (var fi in line.Items)
                            fi.FinalMain = fi.HypotheticalMain + (fi.Fiber.ComputedStyle.FlexGrow ?? 0f) * perUnit;
                    }
                }
                else if (freeSpace2 < -0.001f)
                {
                    float totalShrink = line.Items.Sum(i => (i.Fiber.ComputedStyle.FlexShrink ?? 1f) * i.HypotheticalMain);
                    if (totalShrink > 0)
                    {
                        foreach (var fi in line.Items)
                        {
                            var s = fi.Fiber.ComputedStyle;
                            float scaled = (s.FlexShrink ?? 1f) * fi.HypotheticalMain;
                            fi.FinalMain = fi.HypotheticalMain + (scaled / totalShrink) * freeSpace2;
                        }
                    }
                }

                foreach (var fi in line.Items)
                {
                    var itemStyle = fi.Fiber.ComputedStyle;
                    var effectiveAlign = EffectiveAlign(itemStyle, style);
                    fi.FinalCross = GetCrossSize(fi.Fiber, itemStyle, fi.FinalMain, crossSize, isRow, measurer, effectiveAlign);
                    line.LineCrossSize = Math.Max(line.LineCrossSize, fi.FinalCross);
                }
            }

            return lines;
        }

        private static void PositionLines(
            List<FlexLine> lines, StyleSheet style,
            float contentX, float contentY,
            float contentWidth, float contentHeight,
            float mainSize, float crossSize,
            float gap, bool isRow, bool reversed,
            ILayoutMeasurer? measurer = null,
            Func<string?, (float w, float h)?>? getImageSize = null)
        {
            float crossGap = gap;
            float totalLinesCross = lines.Sum(l => l.LineCrossSize);
            float totalCrossGaps = Math.Max(0, lines.Count - 1) * crossGap;
            float freeCross = crossSize - totalLinesCross - totalCrossGaps;

            (float crossOffset, float crossSpacing) =
                JustifyOffset(JustifyContent.FlexStart, lines.Count, freeCross, crossGap);

            float crossCursor = (isRow ? contentY : contentX) + crossOffset;

            foreach (var line in lines)
            {
                // For each line, treat its LineCrossSize as the cross container
                float lineContentX = isRow ? contentX : crossCursor;
                float lineContentY = isRow ? crossCursor : contentY;
                float lineMain = mainSize;
                float lineCross = line.LineCrossSize;

                PositionLine(line, style, lineContentX, lineContentY,
                    isRow ? lineMain : lineCross, isRow ? lineCross : lineMain,
                    lineMain, lineCross, gap, isRow, reversed, measurer, getImageSize);

                crossCursor += line.LineCrossSize + crossSpacing;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static float GetFlexBasis(Fiber item, StyleSheet style, float mainSize, float crossSize, bool isRow, ILayoutMeasurer? measurer)
        {
            // flex-basis takes priority, then width/height, then default to shrink-to-fit
            if (style.FlexBasis != null && !style.FlexBasis.Value.IsAuto)
                return style.FlexBasis.Value.Resolve(mainSize);

            float? explicit_ = isRow ? style.Width?.Resolve(mainSize) : style.Height?.Resolve(mainSize);
            if (explicit_.HasValue)
                return explicit_.Value;
            float? minMain = isRow ? style.MinWidth?.Resolve(mainSize) : style.MinHeight?.Resolve(mainSize);
            if (minMain.HasValue && minMain.Value > 0)
                return minMain.Value;

            // Fallback: inline style may not have been merged into ComputedStyle (e.g. runtime CSX path)
            var propsStyle = item.Props.Style;
            if (propsStyle != null)
            {
                var pw = isRow ? propsStyle.Width?.Resolve(mainSize) : propsStyle.Height?.Resolve(mainSize);
                if (pw.HasValue && pw.Value > 0) return pw.Value;
                var pm = isRow ? propsStyle.MinWidth?.Resolve(mainSize) : propsStyle.MinHeight?.Resolve(mainSize);
                if (pm.HasValue && pm.Value > 0) return pm.Value;
            }

            // For text/button elements, return main-size along the parent's axis (width for row, height for column).
            if (item.Props.Text != null)
            {
                float main;
                if (measurer != null)
                {
                    var (tw, th) = measurer.MeasureText(item.Props.Text, style, isRow ? null : crossSize);
                    if (isRow)
                    {
                        var pad = style.Padding ?? Thickness.Zero;
                        float padW = pad.Left.Resolve(mainSize) + pad.Right.Resolve(mainSize);
                        main = tw + padW;
                    }
                    else
                    {
                        var pad = style.Padding ?? Thickness.Zero;
                        float padH = pad.Top.Resolve(crossSize) + pad.Bottom.Resolve(crossSize);
                        main = th + padH;
                        // Reserve enough height so DrawText's vertical centering (centreY + 0.8*textH baseline) doesn't
                        // draw glyphs above the box: visual top = centreY - 0.2*textH requires Height >= 1.4*textH.
                        float minLineH = th > 0 ? th * 1.4f : Math.Max(18f, (style.FontSize is { } fs && !fs.IsAuto ? fs.Resolve(16f) : 16f) * 1.25f);
                        main = Math.Max(main, minLineH);
                    }
                }
                else
                {
                    main = isRow
                        ? item.Props.Text.Length * 10f + 32f
                        : 16f * 1.2f; // heuristic line height when no measurer
                }
                var (bt, br, bb, bl) = BoxModel.BorderWidths(style);
                return main + (isRow ? bl + br : bt + bb);
            }

            // When flex-grow > 0 and no explicit basis/size, use 0 so the item receives remaining space
            // (e.g. column with fixed header/footer and flexGrow middle should fill the gap).
            if ((style.FlexGrow ?? 0f) > 0)
                return 0f;

            // Component fiber see-through: function-component fibers (non-string Type) render to a
            // single child element — delegate so Slider/NumberInput etc. get realistic main sizes.
            if (item.Type is not string && item.Child != null && item.Child.Sibling == null)
            {
                var childStyle = item.Child.ComputedStyle;
                return GetFlexBasis(item.Child, childStyle, mainSize, crossSize, isRow, measurer);
            }

            // Intrinsic size: when this item is a flex *column* with no explicit main size,
            // use the sum of its flex items' bases + gaps so the parent doesn't shrink it below content.
            // Only for columns so we don't change row children (e.g. flexGrow items that had default 100f width).
            var itemFlexDir = style.FlexDirection ?? FlexDirection.Row;
            bool itemIsRow = itemFlexDir is FlexDirection.Row or FlexDirection.RowReverse;
            var itemDisplay = style.Display ?? Display.Block;
            if (!itemIsRow && (itemDisplay == Display.Flex || itemDisplay == Display.InlineFlex) && item.Child != null)
            {
                var gapLength = itemIsRow ? style.ColumnGap : style.RowGap;
                if (gapLength == null && style.Gap != null)
                    gapLength = style.Gap;
                float itemGap = BoxModel.ResolveLength(gapLength, mainSize, 0f);
                float sum = 0f;
                int n = 0;
                var ch = item.Child;
                while (ch != null)
                {
                    var cs = ch.ComputedStyle;
                    var csDisp = cs.Display ?? Display.Block;
                    var csPos = cs.Position ?? Position.Static;
                    if (csDisp != Display.None && csPos != Position.Absolute && csPos != Position.Fixed)
                    {
                        sum += GetFlexBasis(ch, cs, mainSize, crossSize, itemIsRow, measurer);
                        n++;
                    }
                    ch = ch.Sibling;
                }
                if (n > 0)
                {
                    sum += Math.Max(0, n - 1) * itemGap;
                    return sum;
                }
            }

            // Flex-row item in a column parent: estimate the row's HEIGHT (its cross-axis) from its
            // tallest child, then add the item's own vertical padding+border.
            if (!isRow && itemIsRow && (itemDisplay == Display.Flex || itemDisplay == Display.InlineFlex) && item.Child != null)
            {
                float est = EstimateIntrinsicCross(item, isRow: true, crossSize: crossSize);
                if (est > 0)
                {
                    var (bt, _, bb, _) = BoxModel.BorderWidths(style);
                    var pad = style.Padding ?? Thickness.Zero;
                    float padH = pad.Top.Resolve(crossSize) + pad.Bottom.Resolve(crossSize);
                    return est + padH + bt + bb;
                }
            }

            return 100f; // Default minimum width
        }

        private static AlignItems EffectiveAlign(StyleSheet itemStyle, StyleSheet containerStyle)
        {
            var self = itemStyle.AlignSelf ?? AlignSelf.Auto;
            if (self != AlignSelf.Auto)
                return (AlignItems)self;
            return containerStyle.AlignItems ?? AlignItems.Stretch;
        }

        private static float GetCrossSize(Fiber item, StyleSheet style, float mainSize, float crossSize, bool isRow, ILayoutMeasurer? measurer, AlignItems effectiveAlign)
        {
            // Explicit cross size always wins
            float? explicit_ = isRow ? style.Height?.Resolve(crossSize) : style.Width?.Resolve(crossSize);
            if (explicit_.HasValue)
                return explicit_.Value;

            // Fallback: inline style may not have been merged into ComputedStyle (e.g. runtime CSX path)
            var propsStyle = item.Props.Style;
            if (propsStyle != null)
            {
                var pc = isRow ? propsStyle.Height?.Resolve(crossSize) : propsStyle.Width?.Resolve(crossSize);
                if (pc.HasValue && pc.Value > 0) return pc.Value;
            }

            // Stretch → fill the full cross axis
            if (effectiveAlign == AlignItems.Stretch)
                return crossSize;

            // Non-stretch: size to content
            if (measurer != null && item.Props.Text is { Length: > 0 } t)
            {
                var (_, th) = measurer.MeasureText(t, style, isRow ? mainSize : crossSize);
                var csPad = style.Padding ?? Thickness.Zero;
                float padH = csPad.Top.Resolve(crossSize) + csPad.Bottom.Resolve(crossSize);
                var (bt, _, bb, _) = BoxModel.BorderWidths(style);
                return th + padH + bt + bb;
            }

            if (item.Props.Text is { Length: > 0 })
            {
                float fontPx = style.FontSize is { } fs && !fs.IsAuto ? Math.Max(1f, fs.Resolve(0f, 16f)) : 16f;
                float th = fontPx * (style.LineHeight ?? 1.4f);
                var hPad = style.Padding ?? Thickness.Zero;
                float padH = hPad.Top.Resolve(crossSize) + hPad.Bottom.Resolve(crossSize);
                var (bt, _, bb, _) = BoxModel.BorderWidths(style);
                return th + padH + bt + bb;
            }

            // No content info for non-stretch items: use MinHeight/MinWidth as floor,
            // then estimate from child tree (catches component fibers like Slider/NumberInput
            // whose rendered children carry explicit heights), else fall back to full cross.
            float minCross = isRow
                ? (style.MinHeight is { } mh && !mh.IsAuto ? mh.Resolve(crossSize) : 0f)
                : (style.MinWidth  is { } mw && !mw.IsAuto ? mw.Resolve(crossSize) : 0f);
            if (minCross > 0) return minCross;

            float estimated = EstimateIntrinsicCross(item, isRow, crossSize);
            return estimated > 0 ? estimated : crossSize;
        }

        /// <summary>
        /// Walk the fiber subtree looking for an explicit cross-axis size.
        /// Used to give non-stretch component fibers (Slider, NumberInput, …) a
        /// realistic height estimate even when their ComputedStyle has none.
        /// </summary>
        private static float EstimateIntrinsicCross(Fiber item, bool isRow, float crossSize)
        {
            var s = item.ComputedStyle;

            // Out-of-flow elements (absolute/fixed) don't contribute to intrinsic cross size.
            // Critically: position:fixed backdrops (e.g. from Popover) have Height=100% which
            // would otherwise inflate the parent's size estimate to the full viewport height.
            var pos = s.Position ?? Position.Static;
            if (pos == Position.Absolute || pos == Position.Fixed) return 0f;

            // Explicit height/width wins immediately
            float? exp = isRow ? s.Height?.Resolve(crossSize) : s.Width?.Resolve(crossSize);
            if (exp.HasValue && exp.Value > 0) return exp.Value;

            float minC = isRow
                ? (s.MinHeight is { } mh && !mh.IsAuto ? mh.Resolve(crossSize) : 0f)
                : (s.MinWidth  is { } mw && !mw.IsAuto ? mw.Resolve(crossSize) : 0f);
            if (minC > 0) return minC;

            if (item.Child == null) return 0f;

            // Single child: see through (component wrapper fibers)
            if (item.Child.Sibling == null)
                return EstimateIntrinsicCross(item.Child, isRow, crossSize);

            // Multiple children: return the max across in-flow siblings
            float maxH = 0f;
            var ch = item.Child;
            while (ch != null)
            {
                float h = EstimateIntrinsicCross(ch, isRow, crossSize);
                if (h > maxH) maxH = h;
                ch = ch.Sibling;
            }
            return maxH;
        }

        private static float AlignCross(FlexItem fi, StyleSheet containerStyle, float lineCrossSize)
        {
            var align = EffectiveAlign(fi.Fiber.ComputedStyle, containerStyle);
            return align switch
            {
                AlignItems.FlexEnd => lineCrossSize - fi.FinalCross,
                AlignItems.Center  => (lineCrossSize - fi.FinalCross) / 2f,
                _                  => 0f,  // FlexStart, Stretch, Baseline
            };
        }

        private static (float offset, float spacing) JustifyOffset(
            JustifyContent justify, int count, float freeSpace, float gap)
        {
            if (count <= 0) return (0, gap);

            return justify switch
            {
                JustifyContent.FlexEnd => (freeSpace, gap),
                JustifyContent.Center => (freeSpace / 2f, gap),
                JustifyContent.SpaceBetween => count > 1
                    ? (0f, gap + freeSpace / (count - 1))
                    : (0f, gap),
                JustifyContent.SpaceAround => (freeSpace / count / 2f,
                                               gap + freeSpace / count),
                JustifyContent.SpaceEvenly => (freeSpace / (count + 1),
                                               gap + freeSpace / (count + 1)),
                _ => (0f, gap),  // FlexStart
            };
        }

        private static void LayoutItemChildren(Fiber item, float width, float height, ILayoutMeasurer? measurer,
            Func<string?, (float w, float h)?>? getImageSize = null)
        {
            // Delegate back to the main layout engine logic for the item's children
            var style = item.ComputedStyle;
            var (cx, cy) = BoxModel.ContentOrigin(style, width, height);
            var (cw, ch) = BoxModel.ContentSize(width, height, style, width, height);

            LayoutEngine.LayoutChildren(item, style, item.Layout.X + cx, item.Layout.Y + cy, cw, ch, measurer, getImageSize);
        }

        // ── Data types ────────────────────────────────────────────────────────

        private sealed class FlexLine
        {
            public List<FlexItem> Items { get; } = new();
            public float LineCrossSize { get; set; }
        }

        private sealed class FlexItem
        {
            public Fiber Fiber { get; set; } = null!;
            public float HypotheticalMain { get; set; }
            public float FinalMain { get; set; }
            public float FinalCross { get; set; }
        }
    }
}
