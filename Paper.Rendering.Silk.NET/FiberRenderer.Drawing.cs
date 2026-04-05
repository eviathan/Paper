using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Layout;
using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET
{
    internal sealed partial class FiberRenderer
    {
        private void PushRoundedClip(float drawX, float drawY, float drawWidth, float drawHeight, float radius)
        {
            if (_gl == null) return;
            _rects.Flush(_screenW, _screenH);
            _fonts?.Flush(_screenW, _screenH);
            _stencilDepth++;
            _gl.Enable(EnableCap.StencilTest);
            _gl.StencilMask(0xFF);
            _gl.StencilFunc(StencilFunction.Always, 0, 0xFF);
            _gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Incr);
            _gl.ColorMask(false, false, false, false);
            _rects.Add(drawX, drawY, drawWidth, drawHeight, 1, 1, 1, 1, 0, 0, 0, 0, 0, radius, radius, radius, radius);
            _rects.Flush(_screenW, _screenH);
            _gl.ColorMask(true, true, true, true);
            _gl.StencilFunc(StencilFunction.Equal, _stencilDepth, 0xFF);
            _gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
        }

        private void PopRoundedClip(float drawX, float drawY, float drawWidth, float drawHeight, float radius)
        {
            if (_gl == null) return;
            _rects.Flush(_screenW, _screenH);
            _fonts?.Flush(_screenW, _screenH);
            _gl.StencilMask(0xFF);
            _gl.StencilFunc(StencilFunction.Always, 0, 0xFF);
            _gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Decr);
            _gl.ColorMask(false, false, false, false);
            _rects.Add(drawX, drawY, drawWidth, drawHeight, 1, 1, 1, 1, 0, 0, 0, 0, 0, radius, radius, radius, radius);
            _rects.Flush(_screenW, _screenH);
            _gl.ColorMask(true, true, true, true);
            _stencilDepth--;
            if (_stencilDepth <= 0)
            {
                _stencilDepth = 0;
                _gl.Disable(EnableCap.StencilTest);
            }
            else
            {
                _gl.StencilFunc(StencilFunction.Equal, _stencilDepth, 0xFF);
                _gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            }
        }

        private void DrawImageWithFit(float boxX, float boxY, float boxW, float boxH,
            uint textureHandle, int imgW, int imgH, ObjectFit fit)
        {
            if (textureHandle == 0 || imgW <= 0 || imgH <= 0) return;
            float imageWidth = imgW;
            float imageHeight = imgH;
            float scale;
            float drawX, drawY, drawW, drawH;
            float u0, v0, u1, v1;
            switch (fit)
            {
                case ObjectFit.Contain:
                    scale = Math.Min(boxW / imageWidth, boxH / imageHeight);
                    drawW = imageWidth * scale;
                    drawH = imageHeight * scale;
                    drawX = boxX + (boxW - drawW) / 2f;
                    drawY = boxY + (boxH - drawH) / 2f;
                    u0 = 0f; v0 = 0f; u1 = 1f; v1 = 1f;
                    break;
                case ObjectFit.Cover:
                    scale = Math.Max(boxW / imageWidth, boxH / imageHeight);
                    float visibleW = boxW / scale;
                    float visibleH = boxH / scale;
                    u0 = (float)(0.5 - visibleW / (2 * imageWidth));
                    v0 = (float)(0.5 - visibleH / (2 * imageHeight));
                    u1 = (float)(0.5 + visibleW / (2 * imageWidth));
                    v1 = (float)(0.5 + visibleH / (2 * imageHeight));
                    drawX = boxX; drawY = boxY; drawW = boxW; drawH = boxH;
                    break;
                default: // Fill
                    drawX = boxX; drawY = boxY; drawW = boxW; drawH = boxH;
                    u0 = 0f; v0 = 0f; u1 = 1f; v1 = 1f;
                    break;
            }
            _viewports.DrawWithUV(drawX, drawY, drawW, drawH, u0, v0, u1, v1, textureHandle, _screenW, _screenH);
        }

        private static (float topLeft, float topRight, float bottomRight, float bottomLeft) ResolveCornerRadii(StyleSheet style, float scale)
        {
            float topLeft = (style.BorderTopLeftRadius ?? style.BorderRadius) * scale;
            float topRight = (style.BorderTopRightRadius ?? style.BorderRadius) * scale;
            float bottomRight = (style.BorderBottomRightRadius ?? style.BorderRadius) * scale;
            float bottomLeft = (style.BorderBottomLeftRadius ?? style.BorderRadius) * scale;
            return (topLeft, topRight, bottomRight, bottomLeft);
        }

        private void DrawRect(
            float x, float y, float w, float h,
            float r, float g, float b, float a,
            float br, float bg, float bb, float ba,
            float borderWidth, float radius, float rotation = 0f)
        {
            _rects.Add(x, y, w, h, r, g, b, a, br, bg, bb, ba, borderWidth, radius, radius, radius, radius, rotation);
        }

        private void DrawRectCorners(
            float x, float y, float w, float h,
            float r, float g, float b, float a,
            float br, float bg, float bb, float ba,
            float borderWidth,
            float radiusTL, float radiusTR, float radiusBR, float radiusBL,
            float rotation = 0f)
        {
            _rects.Add(x, y, w, h, r, g, b, a, br, bg, bb, ba, borderWidth, radiusTL, radiusTR, radiusBR, radiusBL, rotation);
        }

        /// <summary>macOS overlay-style scrollbar: semi-transparent thumb only, no track background.</summary>
        private void DrawScrollbar(string path, float drawX, float drawY, float drawWidth, float drawHeight,
                                   float scrollY, float contentH, float scrollX, float contentW, float opacity)
        {
            RecordScrollbarGeometry(path, drawX, drawY, drawWidth, drawHeight, scrollY, contentH, scrollX, contentW);
            float thumbW = 6f * DpiScale;
            float margin = 3f * DpiScale;
            if (contentH > drawHeight)
            {
                float trackX = drawX + drawWidth - thumbW - margin;
                float trackY = drawY + margin;
                float trackH = drawHeight - margin;
                if (trackH > 0f)
                {
                    float maxScroll = contentH - drawHeight;
                    float thumbRatio = Math.Clamp(drawHeight / contentH, 0.05f, 1f);
                    float thumbH = Math.Max(thumbW * 2f, trackH * thumbRatio);
                    float scrollRatio = maxScroll > 0f ? Math.Clamp(scrollY / maxScroll, 0f, 1f) : 0f;
                    float thumbY = trackY + scrollRatio * (trackH - thumbH);
                    DrawRect(trackX, thumbY, thumbW, thumbH, 0.9f, 0.9f, 0.9f, 0.55f * opacity, 0, 0, 0, 0, 0, thumbW * 0.5f);
                }
            }
            if (contentW > drawWidth)
            {
                float trackY2 = drawY + drawHeight - thumbW - margin;
                float trackX2 = drawX + margin;
                float trackW = drawWidth - margin;
                if (trackW > 0f)
                {
                    float maxScroll = contentW - drawWidth;
                    float thumbRatio = Math.Clamp(drawWidth / contentW, 0.05f, 1f);
                    float thumbW2 = Math.Max(thumbW * 2f, trackW * thumbRatio);
                    float scrollRatio = maxScroll > 0f ? Math.Clamp(scrollX / maxScroll, 0f, 1f) : 0f;
                    float thumbX = trackX2 + scrollRatio * (trackW - thumbW2);
                    DrawRect(thumbX, trackY2, thumbW2, thumbW, 0.9f, 0.9f, 0.9f, 0.55f * opacity, 0, 0, 0, 0, 0, thumbW * 0.5f);
                }
            }
        }

        private void RecordScrollbarGeometry(string path, float drawX, float drawY, float drawWidth, float drawHeight,
                                             float scrollY, float contentH, float scrollX, float contentW)
        {
            float thumbW = 6f * DpiScale;
            float margin = 3f * DpiScale;
            float trackX = drawX + drawWidth - thumbW - margin;
            float trackY = drawY + margin;
            float trackH = drawHeight - margin;
            float maxScroll = Math.Max(0f, contentH - drawHeight);
            float maxScrollX = Math.Max(0f, contentW - drawWidth);
            float thumbRatio = contentH > drawHeight ? Math.Clamp(drawHeight / contentH, 0.05f, 1f) : 1f;
            float thumbH = Math.Max(thumbW * 2f, trackH * thumbRatio);
            float scrollRatio = maxScroll > 0f ? Math.Clamp(scrollY / maxScroll, 0f, 1f) : 0f;
            float thumbY = trackY + scrollRatio * (trackH - thumbH);
            float dpi = DpiScale > 0f ? DpiScale : 1f;
            RenderedScrollbars[path] = new ScrollbarHit(trackX / dpi, trackY / dpi, trackH / dpi,
                                                        thumbY / dpi, thumbH / dpi,
                                                        maxScroll / dpi, maxScrollX / dpi);
        }

        /// <summary>Word-wrap a single line of text to fit within maxWidth. Returns sub-lines with char offsets.</summary>
        private List<(string Text, int Start, int End)> WrapTextLine(string line, int offset, float maxWidth, float fontPx = 16f)
        {
            var result = new List<(string, int, int)>();
            if (_fonts == null || maxWidth <= 0 || line.Length == 0)
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
                    float charWidth = _fonts.MeasureWidth(line.AsSpan(end, 1), fontPx);
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
            if (result.Count == 0) result.Add(("", offset, offset));
            return result;
        }

        private static float ComputeChildrenContentHeight(Fiber? child)
        {
            float maxBottom = 0f;
            var fiber = child;
            while (fiber != null)
            {
                float bottom = fiber.Layout.Y + fiber.Layout.Height;
                if (bottom > maxBottom) maxBottom = bottom;
                fiber = fiber.Sibling;
            }
            return maxBottom;
        }

        private static float ComputeChildrenContentWidth(Fiber? child)
        {
            float maxRight = 0f;
            var fiber = child;
            while (fiber != null)
            {
                float right = fiber.Layout.X + fiber.Layout.Width;
                if (right > maxRight) maxRight = right;
                fiber = fiber.Sibling;
            }
            return maxRight;
        }
    }
}
