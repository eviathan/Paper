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
                        _inputState.InputText = liveFocused.Props.Text ?? "";
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
            _gl.ClearColor(0.07f, 0.07f, 0.12f, 1f);
            _gl.ClearStencil(0);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.StencilBufferBit);

            // Reset blend state — game render (TickFrame) may leave additive or other
            // non-standard blending active (particles, lighting post-process, etc.).
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.Disable(EnableCap.DepthTest);

            var renderer = _renderer!;
            renderer.SetScreenSize(framebufferSize.X, framebufferSize.Y);
            renderer.DpiScale = _width > 0 ? framebufferSize.X / (float)_width : 1f;
            renderer.ScaleX = renderer.DpiScale;
            renderer.ScaleY = renderer.DpiScale;
            DpiScale = renderer.DpiScale;
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
            renderer.Render(root);

            if (_uiState.DragActive && _uiState.DragSource != null)
            {
                if (_uiState.DragData is Paper.Core.Dock.DockDragPayload)
                    renderer.RenderPanelGhost(_uiState.DragCursorX, _uiState.DragCursorY);
                else
                    renderer.RenderGhost(_uiState.DragSource, _uiState.DragCursorX, _uiState.DragCursorY, 0.5f);
            }
            else if (_dockSession?.IsCrossWindowDragActive == true && !_uiState.DragActive && _window != null)
            {
                // macOS GLFW implicit grab: source window keeps all mouse events, so this window
                // never receives OnMouseMove. Read the screen cursor coords the source window wrote
                // into the session on each of its own mouse-move events, then convert to local coords.
                var screenPos = _window.Position;
                var winSize   = _window.Size;
                float localX  = _dockSession.CrossDragCursorScreenX - screenPos.X;
                float localY  = _dockSession.CrossDragCursorScreenY - screenPos.Y;
                if (localX >= 0 && localY >= 0 && localX <= winSize.X && localY <= winSize.Y)
                    renderer.RenderPanelGhost(localX, localY);
            }

            _rects!.Flush(framebufferSize.X, framebufferSize.Y);
            _lines?.Flush(framebufferSize.X, framebufferSize.Y);
            _text?.Flush(framebufferSize.X, framebufferSize.Y);
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
