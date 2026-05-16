using Paper.Core.Markdown;
using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Paper.Layout;
using Paper.Rendering.Silk.NET.Text;
using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET
{
    internal sealed partial class FiberRenderer
    {
        private void Render(Fiber? fiber, float inheritedOpacity, string parentPath, int indexInParent, float scrollX, float scrollY)
        {
            if (fiber == null) return;

            var style = fiber.ComputedStyle;

            if ((style.Visibility ?? Visibility.Visible) == Visibility.Hidden ||
                (style.Display ?? Display.Block) == Display.None)
            {
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            string path = string.IsNullOrEmpty(parentPath) ? indexInParent.ToString() : parentPath + "." + indexInParent;

            // Defer z-indexed fibers: collect and render after normal tree traversal
            if (_zIndexed != null && (style.ZIndex ?? 0) > 0)
            {
                _zIndexed.Add(new ZIndexedFiber(fiber, inheritedOpacity, path, scrollX, scrollY));
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            // ── CSS Transitions: animate background + opacity ─────────────────
            float ownOpacity = style.Opacity ?? 1f;
            PaperColour? animBg = null;
            if (style.Transition != null)
            {
                var transitionDurations = ParseTransitionDurations(style.Transition);
                if (!_transitions.TryGetValue(path, out var transitionState))
                    _transitions[path] = transitionState = new TransitionState();

                var bgTarget = style.Background ?? default;
                if (!transitionState.Initialized)
                {
                    transitionState.BgCurrent = bgTarget;
                    transitionState.BgTarget = bgTarget;
                    transitionState.OpacityCurrent = ownOpacity;
                    transitionState.OpacityTarget = ownOpacity;
                    transitionState.Initialized = true;
                }
                else if (_frameDt > 0f)
                {
                    float bgDur = GetTransitionDuration(transitionDurations, "background");
                    if (bgDur > 0f)
                    {
                        transitionState.BgTarget = bgTarget;
                        float interpolationFactor = 1f - MathF.Exp(-_frameDt * (4f / bgDur));
                        transitionState.BgCurrent = transitionState.BgCurrent.Lerp(transitionState.BgTarget, interpolationFactor);
                        if (style.Background.HasValue || transitionState.BgCurrent.A > 0.004f)
                            animBg = transitionState.BgCurrent;
                    }
                    float opDur = GetTransitionDuration(transitionDurations, "opacity");
                    if (opDur > 0f)
                    {
                        transitionState.OpacityTarget = ownOpacity;
                        float interpolationFactor = 1f - MathF.Exp(-_frameDt * (4f / opDur));
                        transitionState.OpacityCurrent += (transitionState.OpacityTarget - transitionState.OpacityCurrent) * interpolationFactor;
                        ownOpacity = transitionState.OpacityCurrent;
                    }
                }
            }

            float opacity = inheritedOpacity * ownOpacity;
            var (rTL, rTR, rBR, rBL) = ResolveCornerRadii(style, ScaleX);
            var layoutBox = fiber.Layout;
            // position:fixed elements live in viewport space — don't offset by accumulated scroll.
            var pos = style.Position ?? Position.Static;
            if (pos == Position.Fixed) { scrollX = 0f; scrollY = 0f; }
            float drawX = (layoutBox.AbsoluteX + _ghostOffsetX - scrollX) * ScaleX;
            float drawY = (layoutBox.AbsoluteY + _ghostOffsetY - scrollY) * ScaleY;
            float drawWidth = layoutBox.Width * ScaleX;
            float drawHeight = layoutBox.Height * ScaleY;

            // ── CSS transforms ────────────────────────────────────────────────
            if (style.TranslateX is { } tx) drawX += tx * ScaleX;
            if (style.TranslateY is { } ty) drawY += ty * ScaleY;
            if (style.ScaleX is { } sx && sx != 1f)
            {
                float centerX = drawX + drawWidth * 0.5f;
                drawWidth *= sx;
                drawX = centerX - drawWidth * 0.5f;
            }
            if (style.ScaleY is { } sy && sy != 1f)
            {
                float centerY = drawY + drawHeight * 0.5f;
                drawHeight *= sy;
                drawY = centerY - drawHeight * 0.5f;
            }
            float rotation = style.Rotate ?? 0f;

            // Empty Box: use Props.Style or ComputedStyle dimensions when layout gave 0 so colored boxes render.
            if (fiber.Type is string boxElementType && boxElementType == ElementTypes.Box && fiber.Child == null && (drawWidth <= 0 || drawHeight <= 0))
            {
                var propsStyle = fiber.Props.Style;
                if (drawWidth <= 0)
                {
                    if (propsStyle?.Width != null && !propsStyle.Width.Value.IsAuto)
                        drawWidth = Math.Max(0, propsStyle.Width.Value.Resolve(_screenW) * ScaleX);
                    else if (propsStyle?.MinWidth != null && !propsStyle.MinWidth.Value.IsAuto)
                        drawWidth = Math.Max(0, propsStyle.MinWidth.Value.Resolve(_screenW) * ScaleX);
                    else if (style.Width != null && !style.Width.Value.IsAuto)
                        drawWidth = Math.Max(0, style.Width.Value.Resolve(_screenW) * ScaleX);
                    else if (style.MinWidth != null && !style.MinWidth.Value.IsAuto)
                        drawWidth = Math.Max(0, style.MinWidth.Value.Resolve(_screenW) * ScaleX);
                }
                if (drawHeight <= 0)
                {
                    if (propsStyle?.Height != null && !propsStyle.Height.Value.IsAuto)
                        drawHeight = Math.Max(0, propsStyle.Height.Value.Resolve(_screenH) * ScaleY);
                    else if (propsStyle?.MinHeight != null && !propsStyle.MinHeight.Value.IsAuto)
                        drawHeight = Math.Max(0, propsStyle.MinHeight.Value.Resolve(_screenH) * ScaleY);
                    else if (style.Height != null && !style.Height.Value.IsAuto)
                        drawHeight = Math.Max(0, style.Height.Value.Resolve(_screenH) * ScaleY);
                    else if (style.MinHeight != null && !style.MinHeight.Value.IsAuto)
                        drawHeight = Math.Max(0, style.MinHeight.Value.Resolve(_screenH) * ScaleY);
                }
            }

            // ── Viewport culling ──────────────────────────────────────────────
            {
                bool inView = pos == Position.Fixed
                    ? drawX + drawWidth > 0 && drawX < _screenW && drawY + drawHeight > 0 && drawY < _screenH
                    : drawX + drawWidth > _cullRect.X && drawX < _cullRect.X + _cullRect.W &&
                      drawY + drawHeight > _cullRect.Y && drawY < _cullRect.Y + _cullRect.H;
                if (!inView)
                {
                    var ovfXc = style.OverflowX ?? Overflow.Visible;
                    var ovfYc = style.OverflowY ?? Overflow.Visible;
                    bool isClipContainer =
                        ovfXc is Overflow.Scroll or Overflow.Auto or Overflow.Hidden ||
                        ovfYc is Overflow.Scroll or Overflow.Auto or Overflow.Hidden;
                    if (isClipContainer)
                    {
                        Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                        return;
                    }
                    else
                    {
                        RenderFiberChildren(fiber, style, drawX, drawY, drawWidth, drawHeight, layoutBox, opacity, path, scrollX, scrollY, rTL, rTR, rBR, rBL);
                        Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                        return;
                    }
                }
            }

            // ── Image element ─────────────────────────────────────────────────
            if (fiber.Type is string typeImg && typeImg == ElementTypes.Image)
            {
                _rects.Flush(_screenW, _screenH);
                var objFit = style.ObjectFit ?? ObjectFit.Fill;
                (uint texHandle, int imageWidth, int imageHeight) = GetImageResult != null
                    ? GetImageResult(fiber.Props.Src)
                    : (GetImageTexture != null ? GetImageTexture(fiber.Props.Src) : 0u, 0, 0);
                if (texHandle != 0)
                {
                    if (imageWidth > 0 && imageHeight > 0 && objFit != ObjectFit.Fill)
                        DrawImageWithFit(drawX, drawY, drawWidth, drawHeight, texHandle, imageWidth, imageHeight, objFit);
                    else
                        _viewports.Draw(drawX, drawY, drawWidth, drawHeight, texHandle, _screenW, _screenH);
                }
                else
                {
                    DrawRect(drawX, drawY, drawWidth, drawHeight, 0.35f, 0.35f, 0.4f, 1f * opacity, 0, 0, 0, 0, 0, 0);
                }
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            // ── Checkbox element ──────────────────────────────────────────────
            if (fiber.Type is string typeCb && typeCb == ElementTypes.Checkbox)
            {
                bool cbHover = path == HoveredPath;
                const float boxSize = 16f;
                float boxWidth = boxSize * ScaleX;
                float boxHeight = boxSize * ScaleY;
                float cbBoxOffY = (drawHeight - boxHeight) / 2f;
                float cbBg = cbHover ? 0.22f : 0.15f;
                DrawRect(drawX, drawY + cbBoxOffY, boxWidth, boxHeight, cbBg, cbBg, cbBg + 0.05f, 1f * opacity, 0.5f, 0.5f, 0.6f, 1f * opacity, 1f * ScaleX, 2f * ScaleX);
                if (fiber.Props.Checked)
                {
                    float inset = 4f * ScaleX;
                    DrawRect(drawX + inset, drawY + cbBoxOffY + inset, boxWidth - 2 * inset, boxHeight - 2 * inset, 0.4f, 0.6f, 1f, 1f * opacity, 0, 0, 0, 0, 0, 0);
                }
                if (_fonts != null && fiber.Props.Text is { Length: > 0 } cbLabel)
                {
                    float cbFontPx = SilkTextMeasurer.ResolveFontPx(style);
                    float cbLineH = _fonts.LineHeight(cbFontPx);
                    var (cbPadTop, _, _, _) = BoxModel.PaddingPixels(style, layoutBox.Width, layoutBox.Height);
                    float cbCenterOffY = layoutBox.Height / 2f - cbPadTop - cbLineH * 0.5f;
                    var labelBox = layoutBox;
                    labelBox.AbsoluteX += 20;
                    labelBox.X += 20;
                    labelBox.Width = Math.Max(0, labelBox.Width - 20);
                    labelBox.AbsoluteY += cbCenterOffY;
                    labelBox.Y += cbCenterOffY;
                    var col = style.Color ?? new PaperColour(1f, 1f, 1f, 1f);
                    DrawText(cbLabel, labelBox, style, col, opacity, scrollX, scrollY);
                }
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            // ── Radio option element ──────────────────────────────────────────
            if (fiber.Type is string typeRo && typeRo == ElementTypes.RadioOption)
            {
                bool roHover = path == HoveredPath;
                const float circleSize = 14f;
                float circleWidth = circleSize * ScaleX;
                float circleHeight = circleSize * ScaleY;
                float circleRadius = (circleSize / 2f) * ScaleX;
                float roBoxOffY = (drawHeight - circleHeight) / 2f;
                float roBg = roHover ? 0.22f : 0.15f;
                DrawRect(drawX, drawY + roBoxOffY, circleWidth, circleHeight, roBg, roBg, roBg + 0.05f, 1f * opacity, 0.5f, 0.5f, 0.6f, 1f * opacity, 1f * ScaleX, circleRadius);
                if (fiber.Props.RadioChecked)
                {
                    float inset = 3f * ScaleX;
                    DrawRect(drawX + inset, drawY + roBoxOffY + inset, circleWidth - 2 * inset, circleHeight - 2 * inset, 0.4f, 0.6f, 1f, 1f * opacity, 0, 0, 0, 0, 0, circleRadius - inset);
                }
                if (_fonts != null && fiber.Props.Text is { Length: > 0 } roLabel)
                {
                    float roFontPx = SilkTextMeasurer.ResolveFontPx(style);
                    float roLineH = _fonts.LineHeight(roFontPx);
                    var (roPadTop, _, _, _) = BoxModel.PaddingPixels(style, layoutBox.Width, layoutBox.Height);
                    float roCenterOffY = layoutBox.Height / 2f - roPadTop - roLineH * 0.5f;
                    var labelBox = layoutBox;
                    labelBox.AbsoluteX += 20;
                    labelBox.X += 20;
                    labelBox.Width = Math.Max(0, labelBox.Width - 20);
                    labelBox.AbsoluteY += roCenterOffY;
                    labelBox.Y += roCenterOffY;
                    var col = style.Color ?? new PaperColour(1f, 1f, 1f, 1f);
                    DrawText(roLabel, labelBox, style, col, opacity, scrollX, scrollY);
                }
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            // ── Viewport element ──────────────────────────────────────────────
            if (fiber.Type is string typeViewport && typeViewport == ElementTypes.Viewport)
            {
                _rects.Flush(_screenW, _screenH);
                fiber.Props.OnViewportSize?.Invoke((int)layoutBox.Width, (int)layoutBox.Height);
                uint texHandle = fiber.Props.TextureHandle;
                if (texHandle != 0)
                {
                    _viewports.Draw(drawX, drawY, drawWidth, drawHeight, texHandle, _screenW, _screenH);
                }
                else if (style.Background.HasValue && style.Background.Value.A > 0)
                {
                    var viewportFill = style.Background.Value;
                    DrawRect(drawX, drawY, drawWidth, drawHeight,
                        viewportFill.R, viewportFill.G, viewportFill.B, viewportFill.A * opacity,
                        0, 0, 0, 0, 0, style.BorderRadius * ScaleX);
                }
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            // ── Canvas2D element ──────────────────────────────────────────────
            if (fiber.Type is string typeCanvas && typeCanvas == ElementTypes.Canvas2D)
            {
                var drawCb = fiber.Props.Canvas2DDraw;
                if (drawCb != null && _lines != null)
                {
                    // Flush pending rects so drawing order is correct
                    _rects.Flush(_screenW, _screenH);
                    var ctx = new Canvas2DContext(_lines, _rects, _text, drawX, drawY, drawWidth, drawHeight, ScaleX, ScaleY);
                    drawCb(ctx);
                    _lines.Flush(_screenW, _screenH);
                    _text?.Flush(_screenW, _screenH);
                }
                Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
                return;
            }

            // ── Box shadow ────────────────────────────────────────────────────
            if (style.BoxShadow is { Length: > 0 } shadows)
            {
                float boxRadius = style.BorderRadius * ScaleX;
                foreach (var shadow in shadows)
                {
                    if (shadow.Inset) continue;
                    float spread = shadow.SpreadRadius * ScaleX;
                    float blur = shadow.BlurRadius * ScaleX;
                    float shadowX = drawX + shadow.OffsetX * ScaleX - spread;
                    float shadowY = drawY + shadow.OffsetY * ScaleY - spread;
                    float shadowWidth = drawWidth + spread * 2;
                    float shadowHeight = drawHeight + spread * 2;
                    float shadowRadius = boxRadius + spread;
                    float baseAlpha = shadow.Colour.A * opacity;

                    if (blur <= 0)
                    {
                        DrawRect(shadowX, shadowY, shadowWidth, shadowHeight,
                            shadow.Colour.R, shadow.Colour.G, shadow.Colour.B, baseAlpha,
                            0, 0, 0, 0, 0, shadowRadius);
                    }
                    else
                    {
                        int passes = Math.Max(1, Math.Min(6, (int)(blur / 6) + 2));
                        for (int passIndex = 0; passIndex < passes; passIndex++)
                        {
                            float passFraction = (passIndex + 0.5f) / passes;
                            float expand = blur * passFraction;
                            float passAlpha = baseAlpha * (1f - passFraction * 0.6f) / passes;
                            DrawRect(shadowX - expand * 0.5f, shadowY - expand * 0.5f,
                                shadowWidth + expand, shadowHeight + expand,
                                shadow.Colour.R, shadow.Colour.G, shadow.Colour.B, passAlpha,
                                0, 0, 0, 0, 0, shadowRadius + expand * 0.5f);
                        }
                        DrawRect(shadowX, shadowY, shadowWidth, shadowHeight,
                            shadow.Colour.R, shadow.Colour.G, shadow.Colour.B, baseAlpha * 0.5f,
                            0, 0, 0, 0, 0, shadowRadius);
                    }
                }
            }

            // ── Background image (Box) ────────────────────────────────────────
            if (style.BackgroundImage is { } bgImgPath && GetImageResult != null)
            {
                var (bgHandle, bgW, bgH) = GetImageResult(bgImgPath);
                if (bgHandle != 0 && bgW > 0 && bgH > 0)
                {
                    _rects.Flush(_screenW, _screenH);
                    _fonts?.Flush(_screenW, _screenH);
                    DrawImageWithFit(drawX, drawY, drawWidth, drawHeight, bgHandle, bgW, bgH, style.BackgroundSize ?? ObjectFit.Cover);
                }
            }

            // ── Background ────────────────────────────────────────────────────
            var background = animBg.HasValue ? animBg : style.Background;
            if (background.HasValue && background.Value.A > 0)
            {
                var colour = background.Value;
                DrawRectCorners(drawX, drawY, drawWidth, drawHeight,
                    colour.R, colour.G, colour.B, colour.A * opacity,
                    0, 0, 0, 0, 0, rTL, rTR, rBR, rBL, rotation);
            }

            // ── Border ────────────────────────────────────────────────────────
            if (style.Border != null)
            {
                var edge = style.Border.Top;
                if (edge.Style != BorderStyle.None && edge.Width > 0)
                {
                    if (background.HasValue)
                        DrawRectCorners(drawX, drawY, drawWidth, drawHeight,
                            background.Value.R, background.Value.G, background.Value.B, background.Value.A * opacity,
                            edge.Colour.R, edge.Colour.G, edge.Colour.B, edge.Colour.A * opacity,
                            edge.Width * ScaleX, rTL, rTR, rBR, rBL, rotation);
                    else
                        DrawRectCorners(drawX, drawY, drawWidth, drawHeight,
                            0, 0, 0, 0,
                            edge.Colour.R, edge.Colour.G, edge.Colour.B, edge.Colour.A * opacity,
                            edge.Width * ScaleX, rTL, rTR, rBR, rBL, rotation);
                }
            }

            // ── Individual border sides ────────────────────────────────────────
            if (style.BorderTop is { } btop && btop.Style != BorderStyle.None && btop.Width > 0)
            {
                float borderThickness = btop.Width * ScaleY;
                DrawRect(drawX, drawY, drawWidth, borderThickness, btop.Colour.R, btop.Colour.G, btop.Colour.B, btop.Colour.A * opacity, 0, 0, 0, 0, 0, 0);
            }
            if (style.BorderBottom is { } bbot && bbot.Style != BorderStyle.None && bbot.Width > 0)
            {
                float borderThickness = bbot.Width * ScaleY;
                DrawRect(drawX, drawY + drawHeight - borderThickness, drawWidth, borderThickness, bbot.Colour.R, bbot.Colour.G, bbot.Colour.B, bbot.Colour.A * opacity, 0, 0, 0, 0, 0, 0);
            }
            if (style.BorderLeft is { } bleft && bleft.Style != BorderStyle.None && bleft.Width > 0)
            {
                float borderThickness = bleft.Width * ScaleX;
                DrawRect(drawX, drawY, borderThickness, drawHeight, bleft.Colour.R, bleft.Colour.G, bleft.Colour.B, bleft.Colour.A * opacity, 0, 0, 0, 0, 0, 0);
            }
            if (style.BorderRight is { } bright && bright.Style != BorderStyle.None && bright.Width > 0)
            {
                float borderThickness = bright.Width * ScaleX;
                DrawRect(drawX + drawWidth - borderThickness, drawY, borderThickness, drawHeight, bright.Colour.R, bright.Colour.G, bright.Colour.B, bright.Colour.A * opacity, 0, 0, 0, 0, 0, 0);
            }

            // ── Text label (text + button elements have Props.Text) ───────────
            if (_text != null && fiber.Props?.Text is { } labelNotNull)
            {
                bool isFocusedInput = path == FocusedInputPath && !string.IsNullOrEmpty(FocusedInputPath) &&
                    (fiber.Type is string tIn && (tIn == ElementTypes.Input || tIn == ElementTypes.Textarea || tIn == ElementTypes.MarkdownEditor));
                string label = (path == FocusedInputPath && FocusedInputText != null) ? FocusedInputText : labelNotNull;
                if (isFocusedInput && FocusedInputType == "password")
                    label = new string('●', label.Length);
                var col = style.Color ?? new PaperColour(1f, 1f, 1f, 1f);

                if (fiber.Type is string tta && tta == ElementTypes.Textarea)
                {
                    if (_gl != null) BeginTextScissor(drawX, drawY, drawWidth, drawHeight);
                    float fontPx = SilkTextMeasurer.ResolveFontPx(style);
                    float atlasLineH = _fonts!.LineHeight(fontPx);
                    float textH = atlasLineH * Math.Max(0.5f, style.LineHeight ?? 1.4f);
                    var logicalLines = label.Split('\n');
                    var (padTop, padRight, _, padLeft) = BoxModel.PaddingPixels(style, layoutBox.Width, layoutBox.Height);
                    float contentWidth = layoutBox.Width - padLeft - padRight;
                    int idx = 0;
                    int row = 0;
                    for (int li = 0; li < logicalLines.Length; li++)
                    {
                        var logLine = logicalLines[li];
                        var wrappedSegments = WrapTextLine(logLine, idx, contentWidth, fontPx);
                        foreach (var seg in wrappedSegments)
                        {
                            int lineStart = seg.Start;
                            int lineEnd = seg.End;
                            var lineBox = layoutBox;
                            lineBox.AbsoluteY = layoutBox.AbsoluteY + padTop + row * textH;
                            lineBox.Y = layoutBox.Y + padTop + row * textH;
                            lineBox.Height = textH;
                            if (isFocusedInput)
                                DrawSelectionForLine(seg.Text, lineStart, lineEnd, layoutBox, lineBox, style, scrollX, scrollY);
                            if (seg.Text.Length > 0)
                                DrawText(seg.Text, lineBox, style, col, opacity, scrollX, scrollY);
                            if (isFocusedInput)
                                DrawCaretForLine(seg.Text, lineStart, lineEnd, layoutBox, lineBox, style, col, opacity, scrollX, scrollY);
                            row++;
                        }
                        idx += logLine.Length + 1;
                    }
                    if (_gl != null) EndTextScissor();
                }
                else if (fiber.Type is string mde && mde == ElementTypes.MarkdownEditor)
                {
                    if (_gl != null) BeginTextScissor(drawX, drawY, drawWidth, drawHeight);
                    var mdTheme = MarkdownTheme.Dark;
                    float fontPx = SilkTextMeasurer.ResolveFontPx(style);
                    float atlasLineH = _fonts!.LineHeight(fontPx);
                    float textH = atlasLineH * Math.Max(0.5f, style.LineHeight ?? 1.4f);
                    var (padTop, padRight, _, padLeft) = BoxModel.PaddingPixels(style, layoutBox.Width, layoutBox.Height);
                    float contentWidth = layoutBox.Width - padLeft - padRight;
                    string? fam = style.FontFamily;
                    var fontWeight = style.FontWeight;
                    var fontStyle = style.FontStyle;

                    int textHash = label.GetHashCode();
                    int widthInt = (int)contentWidth;
                    int fontPxInt = (int)(fontPx * 10);
                    if (!_mdCache.TryGetValue(path, out var mdCached) ||
                        mdCached.TextHash != textHash || mdCached.TextLen != label.Length ||
                        mdCached.WidthInt != widthInt || mdCached.FontPxInt != fontPxInt)
                    {
                        var tokens = MarkdownTokenizer.Tokenize(label);
                        var mdLogicalLines = label.Split('\n');
                        var rows = new List<(string, int, int)>();
                        int idx = 0;
                        for (int li = 0; li < mdLogicalLines.Length; li++)
                        {
                            var logLine = mdLogicalLines[li];
                            foreach (var seg in WrapTextLine(logLine, idx, contentWidth, fontPx))
                                rows.Add(seg);
                            idx += mdLogicalLines[li].Length + 1;
                        }
                        var rowArr = rows.ToArray();
                        var rowSegs = new MdSegment[rowArr.Length][];
                        for (int ri = 0; ri < rowArr.Length; ri++)
                        {
                            var (lineText, lineStart, lineEnd) = rowArr[ri];
                            rowSegs[ri] = BuildRowSegments(lineText, lineStart, lineEnd, tokens, mdTheme, col, fontPx, fam, fontWeight, fontStyle);
                        }
                        mdCached = new MdCache
                        {
                            TextHash = textHash,
                            TextLen = label.Length,
                            WidthInt = widthInt,
                            FontPxInt = fontPxInt,
                            Rows = rowArr,
                            RowSegments = rowSegs,
                        };
                        _mdCache[path] = mdCached;
                    }

                    var (mdBatch, mdScale) = _fonts!.Get(fontPx * DpiScale, fam, fontWeight, fontStyle);
                    var (padTop2, _, padBottom2, _) = BoxModel.PaddingPixels(style, layoutBox.Width, layoutBox.Height);
                    float contentH2 = textH - padTop2 - padBottom2;
                    float baselineBase = contentH2 >= atlasLineH * 1.4f
                        ? padTop2 + (contentH2 - atlasLineH) / 2f + atlasLineH * 0.8f
                        : padTop2 + atlasLineH * 0.8f;
                    float xOrigin = layoutBox.AbsoluteX + padLeft;

                    for (int row = 0; row < mdCached.Rows.Length; row++)
                    {
                        var seg = mdCached.Rows[row];
                        int lineStart = seg.Start;
                        int lineEnd = seg.End;
                        var lineBox = layoutBox;
                        lineBox.AbsoluteY = layoutBox.AbsoluteY + padTop + row * textH;
                        lineBox.Y = layoutBox.Y + padTop + row * textH;
                        lineBox.Height = textH;
                        if (isFocusedInput)
                            DrawSelectionForLine(seg.Text, lineStart, lineEnd, layoutBox, lineBox, style, scrollX, scrollY);
                        float rowBaseline = lineBox.AbsoluteY + baselineBase;
                        float rowDrawY = (rowBaseline - scrollY) * ScaleY;
                        foreach (var ms in mdCached.RowSegments[row])
                            mdBatch.Add(ms.Text.AsSpan(), (xOrigin + ms.XOffset - scrollX) * ScaleX, rowDrawY,
                                ms.R, ms.G, ms.B, ms.A * opacity, mdScale);
                        if (isFocusedInput)
                            DrawCaretForLine(seg.Text, lineStart, lineEnd, layoutBox, lineBox, style, col, opacity, scrollX, scrollY);
                    }
                    if (_gl != null) EndTextScissor();
                }
                else
                {
                    var singleLine = label ?? "";
                    float inputScrollX = (isFocusedInput && fiber.Type is string inp && inp == ElementTypes.Input) ? FocusedInputScrollX : 0f;
                    if (_gl != null) BeginTextScissor(drawX, drawY, drawWidth, drawHeight);
                    if (isFocusedInput)
                        DrawSelectionForLine(singleLine, 0, singleLine.Length, layoutBox, layoutBox, style, scrollX, scrollY, inputScrollX);
                    if (singleLine.Length > 0)
                        DrawText(singleLine, layoutBox, style, col, opacity, scrollX, scrollY, inputScrollX);
                    else if (!isFocusedInput && fiber.Props.Placeholder is string placeholder && placeholder.Length > 0)
                    {
                        var placeholderCol = new PaperColour(0.6f, 0.6f, 0.6f, col.A * opacity);
                        DrawText(placeholder, layoutBox, style, placeholderCol, opacity, scrollX, scrollY, inputScrollX);
                    }
                    if (isFocusedInput)
                        DrawCaretForLine(singleLine, 0, singleLine.Length, layoutBox, layoutBox, style, col, opacity, scrollX, scrollY, inputScrollX);
                    if (_gl != null) EndTextScissor();
                }
            }

            RenderFiberChildren(fiber, style, drawX, drawY, drawWidth, drawHeight, layoutBox, opacity, path, scrollX, scrollY, rTL, rTR, rBR, rBL);
            Render(fiber.Sibling, inheritedOpacity, parentPath, indexInParent + 1, scrollX, scrollY);
        }

        private void RenderFiberChildren(
            Fiber fiber, StyleSheet style,
            float drawX, float drawY, float drawWidth, float drawHeight, LayoutBox layoutBox,
            float opacity, string path, float scrollX, float scrollY,
            float rTL, float rTR, float rBR, float rBL)
        {
            var overflowX = style.OverflowX ?? Overflow.Visible;
            var overflowY = style.OverflowY ?? Overflow.Visible;
            float radius = Math.Max(Math.Max(rTL, rTR), Math.Max(rBR, rBL));
            bool scrollClip = _gl != null && (overflowY == Overflow.Scroll || overflowY == Overflow.Auto ||
                                              overflowX == Overflow.Scroll || overflowX == Overflow.Auto);
            bool hiddenClip = _gl != null && !scrollClip && fiber.Child != null && radius == 0f &&
                               (overflowX == Overflow.Hidden || overflowY == Overflow.Hidden);
            bool roundedClip = _gl != null && !scrollClip && !hiddenClip && fiber.Child != null &&
                                (overflowX == Overflow.Hidden || overflowY == Overflow.Hidden) && radius > 0f;

            float childScrollX = scrollX;
            float childScrollY = scrollY;
            if ((scrollClip || roundedClip) && GetScrollOffset != null)
            {
                var (ox, oy) = GetScrollOffset(path);
                childScrollX += ox;
                childScrollY += oy;
            }

            if (scrollClip)
            {
                _rects.Flush(_screenW, _screenH);
                _fonts?.Flush(_screenW, _screenH);
                int scissorX = (int)Math.Floor(drawX);
                int scissorY = (int)Math.Floor(_screenH - (drawY + drawHeight));
                int scissorWidth = Math.Max(0, (int)Math.Ceiling(drawX + drawWidth) - scissorX);
                int scissorHeight = Math.Max(0, (int)Math.Ceiling(_screenH - drawY) - scissorY);
                _gl!.Enable(EnableCap.ScissorTest);
                _gl.Scissor(scissorX, scissorY, (uint)scissorWidth, (uint)scissorHeight);
                var prevCullRect = _cullRect;
                float cx0 = Math.Max(_cullRect.X, drawX);
                float cy0 = Math.Max(_cullRect.Y, drawY);
                float cx1 = Math.Min(_cullRect.X + _cullRect.W, drawX + drawWidth);
                float cy1 = Math.Min(_cullRect.Y + _cullRect.H, drawY + drawHeight);
                _cullRect = (cx0, cy0, Math.Max(0, cx1 - cx0), Math.Max(0, cy1 - cy0));
                RenderChildren(fiber.Child, opacity, path, childScrollX, childScrollY);
                _cullRect = prevCullRect;
                _rects.Flush(_screenW, _screenH);
                _fonts?.Flush(_screenW, _screenH);
                _gl.Disable(EnableCap.ScissorTest);
                float rawScrollY = GetScrollOffset != null ? GetScrollOffset(path).scrollY : 0f;
                float rawScrollX = GetScrollOffset != null ? GetScrollOffset(path).scrollX : 0f;
                var (_, padRightPx, padBottomPx, _) = BoxModel.PaddingPixels(style, layoutBox.Width, layoutBox.Height);
                float contentH = (ComputeChildrenContentHeight(fiber.Child) + padBottomPx) * ScaleY;
                float contentW = (ComputeChildrenContentWidth(fiber.Child) + padRightPx) * ScaleX;
                float sbOpacity = GetScrollbarOpacity != null ? GetScrollbarOpacity(path) : 0f;
                if (sbOpacity > 0f)
                {
                    _rects.Flush(_screenW, _screenH);
                    DrawScrollbar(path, drawX, drawY, drawWidth, drawHeight, rawScrollY * ScaleY, contentH, rawScrollX * ScaleX, contentW, sbOpacity * opacity);
                }
                else
                {
                    RecordScrollbarGeometry(path, drawX, drawY, drawWidth, drawHeight, rawScrollY * ScaleY, contentH, rawScrollX * ScaleX, contentW);
                }
            }
            else if (hiddenClip)
            {
                _rects.Flush(_screenW, _screenH);
                _fonts?.Flush(_screenW, _screenH);
                int hiddenScissorX = (int)Math.Floor(drawX);
                int hiddenScissorY = (int)Math.Floor(_screenH - (drawY + drawHeight));
                int hiddenScissorWidth = Math.Max(0, (int)Math.Ceiling(drawX + drawWidth) - hiddenScissorX);
                int hiddenScissorHeight = Math.Max(0, (int)Math.Ceiling(_screenH - drawY) - hiddenScissorY);
                _gl!.Enable(EnableCap.ScissorTest);
                _gl.Scissor(hiddenScissorX, hiddenScissorY, (uint)hiddenScissorWidth, (uint)hiddenScissorHeight);
                var prevCullRectH = _cullRect;
                float hcx0 = Math.Max(_cullRect.X, drawX);
                float hcy0 = Math.Max(_cullRect.Y, drawY);
                float hcx1 = Math.Min(_cullRect.X + _cullRect.W, drawX + drawWidth);
                float hcy1 = Math.Min(_cullRect.Y + _cullRect.H, drawY + drawHeight);
                _cullRect = (hcx0, hcy0, Math.Max(0, hcx1 - hcx0), Math.Max(0, hcy1 - hcy0));
                RenderChildren(fiber.Child, opacity, path, childScrollX, childScrollY);
                _cullRect = prevCullRectH;
                _rects.Flush(_screenW, _screenH);
                _fonts?.Flush(_screenW, _screenH);
                _gl.Disable(EnableCap.ScissorTest);
            }
            else if (roundedClip)
            {
                PushRoundedClip(drawX, drawY, drawWidth, drawHeight, radius);
                var prevCullRectR = _cullRect;
                float rx0 = Math.Max(_cullRect.X, drawX); float ry0 = Math.Max(_cullRect.Y, drawY);
                float rx1 = Math.Min(_cullRect.X + _cullRect.W, drawX + drawWidth); float ry1 = Math.Min(_cullRect.Y + _cullRect.H, drawY + drawHeight);
                _cullRect = (rx0, ry0, Math.Max(0, rx1 - rx0), Math.Max(0, ry1 - ry0));
                RenderChildren(fiber.Child, opacity, path, childScrollX, childScrollY);
                _cullRect = prevCullRectR;
                PopRoundedClip(drawX, drawY, drawWidth, drawHeight, radius);
            }
            else
            {
                RenderChildren(fiber.Child, opacity, path, childScrollX, childScrollY);
            }
        }

        private void RenderChildren(Fiber? child, float opacity, string parentPath, float scrollX, float scrollY)
        {
            Render(child, opacity, parentPath, 0, scrollX, scrollY);
        }

        private void BeginTextScissor(float drawX, float drawY, float drawWidth, float drawHeight)
        {
            if (_gl == null) return;
            _rects.Flush(_screenW, _screenH);
            _fonts?.Flush(_screenW, _screenH);
            float left = Math.Max(drawX, _cullRect.X);
            float top = Math.Max(drawY, _cullRect.Y);
            float right = Math.Min(drawX + drawWidth, _cullRect.X + _cullRect.W);
            float bottom = Math.Min(drawY + drawHeight, _cullRect.Y + _cullRect.H);
            int x = (int)Math.Floor(left);
            int w = Math.Max(0, (int)Math.Ceiling(right) - x);
            int y = (int)Math.Floor(_screenH - bottom);
            int h = Math.Max(0, (int)Math.Ceiling(_screenH - top) - y);
            _gl.Enable(EnableCap.ScissorTest);
            _gl.Scissor(x, y, (uint)w, (uint)h);
        }

        private void EndTextScissor()
        {
            if (_gl == null) return;
            _rects.Flush(_screenW, _screenH);
            _fonts?.Flush(_screenW, _screenH);
            if (_cullRect.X <= 0 && _cullRect.Y <= 0 && _cullRect.W >= _screenW && _cullRect.H >= _screenH)
            {
                _gl.Disable(EnableCap.ScissorTest);
            }
            else
            {
                int x = (int)Math.Floor(_cullRect.X);
                int w = Math.Max(0, (int)Math.Ceiling(_cullRect.X + _cullRect.W) - x);
                int y = (int)Math.Floor(_screenH - (_cullRect.Y + _cullRect.H));
                int h = Math.Max(0, (int)Math.Ceiling(_screenH - _cullRect.Y) - y);
                _gl.Enable(EnableCap.ScissorTest);
                _gl.Scissor(x, y, (uint)w, (uint)h);
            }
        }

        /// <summary>Render a single fiber (and its subtree) used for z-indexed deferred pass.</summary>
        private void RenderFiber(Fiber fiber, float opacity, string path, float scrollX, float scrollY)
        {
            var saved = _zIndexed;
            _zIndexed = null;
            int lastDot = path.LastIndexOf('.');
            string parentPath = lastDot >= 0 ? path[..lastDot] : "";
            int index = int.TryParse(lastDot >= 0 ? path[(lastDot + 1)..] : path, out int idx) ? idx : 0;
            Render(fiber, opacity, parentPath, index, scrollX, scrollY);
            _zIndexed = saved;
        }
    }
}
