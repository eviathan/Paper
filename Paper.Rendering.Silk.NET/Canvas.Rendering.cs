using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Paper.Rendering.Silk.NET.Utilities;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Paper.Rendering.Silk.NET
{
    public sealed partial class Canvas
    {
        private void OnRender(double dt)
        {
            if (_gl == null || _rects == null || _viewports == null ||
                _reconciler == null || _layout == null) return;

            PreRender?.Invoke(dt);

            bool requested = _renderState.ExternalRenderRequested;
            if (requested)
                _renderState.ExternalRenderRequested = false;
            if (requested || _reconciler.NeedsUpdate())
            {
                _reconciler.Update(_rootFactory!(), forceReconcile: requested);
                _renderState.LayoutDirty = true;
                _renderState.NeedsLayout = true;

                // AutoFocus: if a newly-mounted input has autoFocus=true, focus it.
                var autoFocusFiber = FindAutoFocus(_reconciler.Root);
                if (autoFocusFiber != null)
                {
                    var autoFocusPath = FiberTreeUtility.GetPathString(autoFocusFiber);
                    if (autoFocusPath != _inputState.FocusedPath)
                        SetFocus(autoFocusFiber);
                }
            }
            var root = _reconciler.Root;
            if (root == null) return;
            LayoutAndDraw();
            _renderState.LayoutDirty = false;
            if (_renderer?.HasActiveTransitions == true)
                MarkDirty(animationSeconds: 0.1);
        }

        private void LayoutAndDraw()
        {
            var root = _reconciler!.Root!;

            // Re-bind focus to the current tree so Input/Textarea get correct Props.Text after re-render.
            if (_inputState.FocusedPath != null)
            {
                var liveFocused = FiberTreeUtility.GetFiberByPath(root, _inputState.FocusedPath);
                if (liveFocused != null && HitTestUtility.IsFocusable(liveFocused))
                {
                    _inputState.Focused = liveFocused;
                    if (liveFocused.Type is string liveType && InputTextUtility.IsTextInput(liveType))
                    {
                        // Use ??= so user-typed text is never overwritten by a stale Props.Text
                        // from a reconcile cycle that hasn't yet picked up the mutable model change.
                        // SetFocus() initialises InputText to null→Props.Text when focus is first set,
                        // so the first render after focus-in still gets the correct seed value.
                        _inputState.InputText ??= liveFocused.Props.Text ?? "";
                        InputTextUtility.ClampInputIndices(_inputState.InputText.Length, ref _inputState.InputCaret, ref _inputState.InputSelStart, ref _inputState.InputSelEnd);
                    }
                }
                else
                    _inputState.Focused = null;
            }

            // Re-bind hovered to the current tree after reconcile (fiber objects are replaced each reconcile).
            if (_uiState.HoveredPath != null)
                _uiState.Hovered = FiberTreeUtility.GetFiberByPath(root, _uiState.HoveredPath);

            // Re-bind pointer-down and drag fibers so stale references don't break pointer-move / drag dispatch.
            if (_pointerDownFiberPath != null)
                _pointerDownFiber = FiberTreeUtility.GetFiberByPath(root, _pointerDownFiberPath) ?? _pointerDownFiber;
            if (_uiState.DragSourcePath != null)
                _uiState.DragSource = FiberTreeUtility.GetFiberByPath(root, _uiState.DragSourcePath) ?? _uiState.DragSource;
            if (_uiState.DragOverPath != null)
                _uiState.DragOver = FiberTreeUtility.GetFiberByPath(root, _uiState.DragOverPath) ?? _uiState.DragOver;
            if (_uiState.CrossWindowDragOverPath != null)
                _uiState.CrossWindowDragOver = FiberTreeUtility.GetFiberByPath(root, _uiState.CrossWindowDragOverPath) ?? _uiState.CrossWindowDragOver;

            // If the style registry changed, mark all fibers dirty so stale cached ComputedStyles are recomputed.
            int registryVersion = Styles.Version;
            if (registryVersion != _renderState.LastStyleRegistryVersion)
            {
                PaperUtility.InvalidateStyleTree(root);
                _renderState.LastStyleRegistryVersion = registryVersion;
                _renderState.NeedsLayout = true;
            }
            ApplyComputedStyles(root);

            var framebufferSize = _window!.FramebufferSize;
            _framebufferState.LastFramebufferWidth = framebufferSize.X;
            _framebufferState.LastFramebufferHeight = framebufferSize.Y;
            int layoutWidth = _width;
            int layoutHeight = _height;

            if (_layout == null || _measurer == null) return;

            if (_renderState.NeedsLayout)
            {
                // Snapshot layout boxes before layout overwrites them — used for dirty rect.
                SavePreviousLayouts(root);
                if (_reconciler?.PortalRoots is { Count: > 0 } portalsForSnapshot)
                    foreach (var portal in portalsForSnapshot)
                        SavePreviousLayouts(portal);

                _layout.GetImageSize = path =>
                {
                    var resolved = PaperUtility.ResolveImagePath(path);
                    var dim = _imageLoader?.GetDimensions(resolved);
                    return dim.HasValue ? ((float)dim.Value.w, (float)dim.Value.h) : ((float, float)?)null;
                };
                _layout.Layout(root, layoutWidth, layoutHeight, _measurer);

                if (_reconciler?.PortalRoots is { Count: > 0 } portals)
                {
                    foreach (var portal in portals)
                        ApplyStylesAndLayout(portal, layoutWidth, layoutHeight);
                }
                _renderState.NeedsLayout = false;
            }

            // macOS GLFW implicit grab: source window keeps all pointer events during drag,
            // so this window never receives OnMouseMove. Inject a synthetic move from the
            // session's last known screen cursor position so drop zones stay highlighted.
            if (_dockSession?.IsCrossWindowDragActive == true && !_uiState.DragActive)
                SyntheticCrossWindowDragMove();

            // Update horizontal scroll for single-line input so caret stays in view.
            if (_text != null && _inputState.Focused != null && _inputState.Focused.Type is string focusedType && focusedType == ElementTypes.Input)
            {
                var focusedStyle = _inputState.Focused.ComputedStyle;
                var focusedLayout = _inputState.Focused.Layout;
                var inputPadding = focusedStyle.Padding ?? Thickness.Zero;
                float padLeft = inputPadding.Left.Resolve(focusedLayout.Width);
                float padRight = inputPadding.Right.Resolve(focusedLayout.Width);
                float contentWidth = focusedLayout.Width - padLeft - padRight;
                string inputText = _inputState.InputText ?? _inputState.Focused.Props?.Text ?? "";
                var fontAtlas = _text.Atlas;
                float baseSize = fontAtlas.BaseSize > 0 ? fontAtlas.BaseSize : 16f;
                float fontPx = focusedStyle.FontSize is { } inputFontSize && !inputFontSize.IsAuto ? inputFontSize.Resolve(baseSize) : baseSize;
                float scale = baseSize > 0 ? fontPx / baseSize : 1f;
                float fullTextWidth = _text.MeasureWidth(inputText.AsSpan()) * scale;
                int caret = Math.Clamp(_inputState.InputCaret, 0, inputText.Length);
                float caretX = caret <= 0 ? 0 : _text.MeasureWidth(inputText.AsSpan(0, caret)) * scale;
                float maxScroll = Math.Max(0, fullTextWidth - contentWidth);
                if (maxScroll <= 0)
                    _inputState.InputScrollX = 0;
                else
                {
                    const float caretMargin = 8f;
                    _inputState.InputScrollX = Math.Clamp(caretX - contentWidth + caretMargin, 0, maxScroll);
                }
            }
            else
                _inputState.InputScrollX = 0;

            _gl!.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.Viewport(0, 0, (uint)framebufferSize.X, (uint)framebufferSize.Y);

            // Reset GL state — game render may leave non-standard state active.
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.Disable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.StencilTest);
            _gl.ColorMask(true, true, true, true);

            // ── Dirty-rect optimisation ───────────────────────────────────────
            // When possible, only clear + redraw the region of the framebuffer that actually
            // changed this frame.  The existing framebuffer content (previous frame) is preserved
            // outside the dirty rect, which is valid on all major desktop GL drivers (macOS, Windows,
            // Linux) that retain the back buffer after SwapBuffers.
            //
            // Fall back to a full clear when:
            //   • this is the first frame (no previous buffer content)
            //   • CSS transitions are animating (FiberRenderer owns those dirty regions internally)
            //   • a scrollbar is fading out (same reason)
            //   • drag ghost is being drawn (always covers an arbitrary region)
            //   • dirty rect covers ≥ 80% of the framebuffer (full clear is cheaper)
            float fbW = framebufferSize.X;
            float fbH = framebufferSize.Y;
            float dpiScale = _width > 0 ? fbW / _width : 1f;

            bool dragActive = _uiState.DragActive && _uiState.DragSource != null;
            bool crossWindowDrag = _dockSession?.IsCrossWindowDragActive == true && !_uiState.DragActive;
            bool canUseDirtyRect =
                _hasFirstFrameRendered
                && !(_renderer?.HasActiveTransitions == true)
                && !HasActiveScrollbarFade()
                && !dragActive
                && !crossWindowDrag;

            (float X, float Y, float W, float H)? dirtyRect = null;
            if (canUseDirtyRect)
            {
                dirtyRect = ComputeDirtyScreenRect(root, dpiScale, dpiScale, fbW, fbH);

                // Include portal dirty regions.
                if (dirtyRect.HasValue && _reconciler?.PortalRoots is { Count: > 0 } dirtyPortals)
                {
                    foreach (var portal in dirtyPortals)
                    {
                        var pr = ComputeDirtyScreenRect(portal, dpiScale, dpiScale, fbW, fbH);
                        if (pr.HasValue)
                        {
                            if (!dirtyRect.HasValue)
                                dirtyRect = pr;
                            else
                            {
                                float x1 = Math.Min(dirtyRect.Value.X, pr.Value.X);
                                float y1 = Math.Min(dirtyRect.Value.Y, pr.Value.Y);
                                float x2 = Math.Max(dirtyRect.Value.X + dirtyRect.Value.W, pr.Value.X + pr.Value.W);
                                float y2 = Math.Max(dirtyRect.Value.Y + dirtyRect.Value.H, pr.Value.Y + pr.Value.H);
                                dirtyRect = (x1, y1, x2 - x1, y2 - y1);
                            }
                        }
                    }
                }

                // If the dirty rect covers most of the screen a full clear is simpler and no slower.
                if (dirtyRect.HasValue && dirtyRect.Value.W * dirtyRect.Value.H >= fbW * fbH * 0.8f)
                    dirtyRect = null;
            }

            _gl.ClearColor(0.07f, 0.07f, 0.12f, 1f);
            _gl.ClearStencil(0);
            if (dirtyRect.HasValue)
            {
                var dr = dirtyRect.Value;
                // GL scissor origin is bottom-left; Y must be flipped.
                int glX = (int)Math.Floor(dr.X);
                int glY = (int)Math.Floor(fbH - dr.Y - dr.H);
                int glW = Math.Max(1, (int)Math.Ceiling(dr.W));
                int glH = Math.Max(1, (int)Math.Ceiling(dr.H));
                _gl.Enable(EnableCap.ScissorTest);
                _gl.Scissor(glX, glY, (uint)glW, (uint)glH);
                _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.StencilBufferBit);
                _gl.Disable(EnableCap.ScissorTest);
            }
            else
            {
                _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.StencilBufferBit);
            }

            var renderer = _renderer!;
            renderer.SetScreenSize(fbW, fbH);
            renderer.DpiScale = dpiScale;
            renderer.ScaleX   = dpiScale;
            renderer.ScaleY   = dpiScale;
            DpiScale = dpiScale;
            renderer.FocusedInputPath = _inputState.Focused != null && InputTextUtility.IsTextInput(_inputState.Focused.Type as string)
                ? FiberTreeUtility.GetPathString(_inputState.Focused)
                : _inputState.FocusedPath;
            renderer.FocusedInputText = _inputState.InputText;
            renderer.FocusedInputType = _inputState.Focused?.Props?.InputType;
            renderer.FocusedInputCaret = _inputState.InputCaret;
            renderer.FocusedInputSelStart = _inputState.InputSelStart;
            renderer.FocusedInputSelEnd = _inputState.InputSelEnd;
            renderer.FocusedInputCaretVisible = ComputeCaretVisible();
            renderer.FocusedInputScrollX = _inputState.InputScrollX;
            renderer.HoveredPath = _uiState.Hovered != null ? FiberTreeUtility.GetPathString(_uiState.Hovered) : null;
            renderer.PortalRoots = _reconciler?.PortalRoots;
            renderer.DirtyRect   = dirtyRect;
            renderer.Render(root);

            if (dragActive)
            {
                if (_uiState.DragData is Paper.Core.Dock.DockDragPayload)
                    renderer.RenderPanelGhost(_uiState.DragCursorX, _uiState.DragCursorY);
                else
                    renderer.RenderGhost(_uiState.DragSource, _uiState.DragCursorX, _uiState.DragCursorY, 0.5f);
            }
            else if (crossWindowDrag && _window != null)
            {
                // macOS GLFW implicit grab: source window keeps all mouse events, so this window
                // never receives OnMouseMove. Read the screen cursor coords the source window wrote
                // into the session on each of its own mouse-move events, then convert to local coords.
                var screenPos = _window.Position;
                var winSize   = _window.Size;
                float localX  = _dockSession!.CrossDragCursorScreenX - screenPos.X;
                float localY  = _dockSession.CrossDragCursorScreenY  - screenPos.Y;
                if (localX >= 0 && localY >= 0 && localX <= winSize.X && localY <= winSize.Y)
                    renderer.RenderPanelGhost(localX, localY);
            }

            _rects!.Flush(fbW, fbH);
            _lines?.Flush(fbW, fbH);
            _text?.Flush(fbW, fbH);

            // Reset dirty flags for the next frame.
            ClearVisuallyDirty(root);
            if (_reconciler?.PortalRoots is { Count: > 0 } cleanPortals)
                foreach (var portal in cleanPortals)
                    ClearVisuallyDirty(portal);

            _hasFirstFrameRendered = true;
        }

        private static Fiber? FindAutoFocus(Fiber? fiber)
        {
            if (fiber == null) return null;
            if (fiber.Props.AutoFocus && HitTestUtility.IsFocusable(fiber)) return fiber;
            var found = FindAutoFocus(fiber.Child);
            if (found != null) return found;
            return FindAutoFocus(fiber.Sibling);
        }

        private void OnResize(Vector2D<int> size)
        {
            int newWidth = size.X;
            int newHeight = size.Y;
            if (MinimumWindowWidth.HasValue && newWidth < MinimumWindowWidth.Value)
                newWidth = MinimumWindowWidth.Value;
            if (MinimumWindowHeight.HasValue && newHeight < MinimumWindowHeight.Value)
                newHeight = MinimumWindowHeight.Value;
            if (newWidth != size.X || newHeight != size.Y)
            {
                try { _window!.Size = new Vector2D<int>(newWidth, newHeight); } catch { }
            }
            _width = newWidth;
            _height = newHeight;
            _renderState.LayoutDirty = true;
            _renderState.NeedsLayout = true;
            if (_gl != null && _reconciler?.Root != null)
                _window?.DoRender();
        }

        // ── Dirty-rect helpers ────────────────────────────────────────────────

        /// <summary>
        /// Snapshot each fiber's current layout box into <see cref="Fiber.PreviousLayout"/> before
        /// the layout pass runs.  Called once per frame so the dirty rect computation can union the
        /// old and new positions of elements that moved.
        /// </summary>
        private static void SavePreviousLayouts(Fiber? fiber)
        {
            while (fiber != null)
            {
                fiber.PreviousLayout = fiber.Layout;
                SavePreviousLayouts(fiber.Child);
                fiber = fiber.Sibling;
            }
        }

        /// <summary>
        /// Walk the committed tree after layout and compute the union of the screen-space bounds of
        /// every <see cref="Fiber.VisuallyDirty"/> fiber.  For fibers that moved, the union includes
        /// both the old (<see cref="Fiber.PreviousLayout"/>) and new (<see cref="Fiber.Layout"/>)
        /// positions so the vacated region is cleared as well.
        ///
        /// Also marks any fiber whose layout box changed since last frame as
        /// <see cref="Fiber.VisuallyDirty"/> — this catches elements that moved purely because a
        /// parent was resized, without going through the reconciler.
        ///
        /// Returns null when no dirty fibers were found (nothing to redraw).
        /// The returned rect is in framebuffer pixel space (origin top-left, Y increasing downward),
        /// expanded by <paramref name="padding"/> pixels to cover sub-pixel edges.
        /// </summary>
        private static (float X, float Y, float W, float H)? ComputeDirtyScreenRect(
            Fiber?  root,
            float   scaleX,
            float   scaleY,
            float   fbW,
            float   fbH,
            float   padding = 2f)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            AccumulateDirtyBounds(root, scaleX, scaleY, ref minX, ref minY, ref maxX, ref maxY);

            if (minX > maxX || minY > maxY)
                return null;

            float x = Math.Max(0f,  minX - padding);
            float y = Math.Max(0f,  minY - padding);
            float x2 = Math.Min(fbW, maxX + padding);
            float y2 = Math.Min(fbH, maxY + padding);
            return (x, y, x2 - x, y2 - y);
        }

        private static void AccumulateDirtyBounds(
            Fiber? fiber,
            float  scaleX, float scaleY,
            ref float minX, ref float minY,
            ref float maxX, ref float maxY)
        {
            while (fiber != null)
            {
                // Detect fibers that moved or resized due to layout changes (e.g. window resize).
                var cur  = fiber.Layout;
                var prev = fiber.PreviousLayout;
                if (cur.AbsoluteX != prev.AbsoluteX || cur.AbsoluteY != prev.AbsoluteY ||
                    cur.Width     != prev.Width      || cur.Height    != prev.Height)
                {
                    fiber.VisuallyDirty = true;
                }

                if (fiber.VisuallyDirty)
                {
                    // Current position
                    UnionRect(cur.AbsoluteX * scaleX, cur.AbsoluteY * scaleY,
                              cur.Width     * scaleX, cur.Height    * scaleY,
                              ref minX, ref minY, ref maxX, ref maxY);

                    // Previous position (needed to clear the vacated region when element moved)
                    if (prev.Width > 0 && prev.Height > 0)
                    {
                        UnionRect(prev.AbsoluteX * scaleX, prev.AbsoluteY * scaleY,
                                  prev.Width     * scaleX, prev.Height    * scaleY,
                                  ref minX, ref minY, ref maxX, ref maxY);
                    }
                }

                AccumulateDirtyBounds(fiber.Child, scaleX, scaleY, ref minX, ref minY, ref maxX, ref maxY);
                fiber = fiber.Sibling;
            }
        }

        private static void UnionRect(float x, float y, float w, float h,
                                      ref float minX, ref float minY,
                                      ref float maxX, ref float maxY)
        {
            if (x     < minX) minX = x;
            if (y     < minY) minY = y;
            if (x + w > maxX) maxX = x + w;
            if (y + h > maxY) maxY = y + h;
        }

        /// <summary>Reset all <see cref="Fiber.VisuallyDirty"/> flags after the frame has been drawn.</summary>
        private static void ClearVisuallyDirty(Fiber? fiber)
        {
            while (fiber != null)
            {
                fiber.VisuallyDirty = false;
                ClearVisuallyDirty(fiber.Child);
                fiber = fiber.Sibling;
            }
        }

        /// <summary>
        /// Returns true when any tracked scroll container currently has a visible (non-zero opacity)
        /// scrollbar.  While scrollbars are fading out the full frame must be redrawn because the
        /// fade affects the rendered scrollbar geometry inside the container bounds, which we do not
        /// separately track as dirty fibers.
        /// </summary>
        private bool HasActiveScrollbarFade()
        {
            if (_scrollState.ScrollbarLastActive.Count == 0) return false;
            double utcNow = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
            const double visibleSeconds = 1.2, fadeSeconds = 0.4;
            foreach (var lastActiveTime in _scrollState.ScrollbarLastActive.Values)
            {
                double elapsed = utcNow - lastActiveTime;
                if (elapsed < visibleSeconds + fadeSeconds)
                    return true;
            }
            return false;
        }

        private void ApplyComputedStyles(Fiber fiber)
        {
            if (fiber == null) return;
            var interactionState = new InteractionState(
                Hover: ReferenceEquals(fiber, _uiState.Hovered),
                Active: ReferenceEquals(fiber, _uiState.Pressed),
                Focus: ReferenceEquals(fiber, _inputState.Focused));

            if (fiber.StyleDirty || fiber.CachedInteractionState != interactionState)
            {
                fiber.ComputedStyle = StyleResolver.Resolve(fiber.Type, fiber.Props, Styles, interactionState, fiber);
                fiber.StyleDirty = false;
                fiber.CachedInteractionState = interactionState;
                // Interaction-state change means a visual change (hover highlight, focus ring, etc.)
                // even if the reconciler didn't re-render this fiber.
                fiber.VisuallyDirty = true;
            }

            // Canvas2D and Viewport elements invoke user callbacks whose output can change
            // every frame without going through reconcile.  Always include them in the dirty rect.
            if (fiber.Type is string ft &&
                (ft == ElementTypes.Canvas2D || ft == ElementTypes.Viewport))
            {
                fiber.VisuallyDirty = true;
            }

            var child = fiber.Child;
            while (child != null)
            {
                ApplyComputedStyles(child);
                child = child.Sibling;
            }
        }

        private void ApplyStylesAndLayout(Fiber fiber, int width, int height)
        {
            ApplyComputedStyles(fiber);
            _layout?.Layout(fiber, width, height, _measurer!);
        }
    }
}
