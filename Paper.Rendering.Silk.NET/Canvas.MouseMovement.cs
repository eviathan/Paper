using Paper.Core.Events;
using Paper.Core.Reconciler;
using Paper.Rendering.Silk.NET.Utilities;
using Silk.NET.Input;
using System.Numerics;

namespace Paper.Rendering.Silk.NET
{
    public sealed partial class Canvas
    {
        private void OnMouseMove(IMouse mouse, Vector2 position)
        {
            if (_reconciler?.Root == null) return;

            if (_scrollState.ScrollbarDragPath != null && mouse.IsButtonPressed(MouseButton.Left))
            {
                HandleScrollbarThumbDrag(position);
                return;
            }

            var (layoutCoordsX, layoutCoordsY) = PaperUtility.ToLayoutCoords(position);
            var target = HitTestAll(layoutCoordsX, layoutCoordsY);

            if (!_uiState.DragActive && !ReferenceEquals(target, _uiState.Hovered))
            {
                if (_uiState.Hovered != null)
                {
                    DispatchPointer(_uiState.Hovered, new PointerEvent { Type = PointerEventType.Leave, X = layoutCoordsX, Y = layoutCoordsY, Button = -1 });
                    _uiState.Hovered.Props.OnMouseLeave?.Invoke();
                }
                if (target != null)
                {
                    DispatchPointer(target, new PointerEvent { Type = PointerEventType.Enter, X = layoutCoordsX, Y = layoutCoordsY, Button = -1 });
                    target.Props.OnMouseEnter?.Invoke();
                }
                _uiState.Hovered = target;
                _uiState.HoveredPath = target != null ? FiberTreeUtility.GetPathString(target) : null;
                ApplyGlfwCursor(target?.ComputedStyle.Cursor ?? Paper.Core.Styles.Cursor.Default);
                MarkDirty();
            }
            else if (_uiState.DragActive)
            {
                ApplyGlfwCursor(target?.ComputedStyle.Cursor ?? Paper.Core.Styles.Cursor.Default);
                MarkDirty();
            }

            if (mouse.IsButtonPressed(MouseButton.Left) && _pointerDownFiber != null)
            {
                var layoutBox = _pointerDownFiber.Layout;
                DispatchPointer(_pointerDownFiber, new PointerEvent
                {
                    Type = PointerEventType.Move,
                    X = layoutCoordsX,
                    Y = layoutCoordsY,
                    Button = 0,
                    LocalX = layoutCoordsX - layoutBox.AbsoluteX,
                    LocalY = layoutCoordsY - layoutBox.AbsoluteY,
                });
            }
            else if (target != null)
            {
                DispatchPointer(target, new PointerEvent { Type = PointerEventType.Move, X = layoutCoordsX, Y = layoutCoordsY, Button = -1 });
            }

            HandleDragAndDropMove(target, mouse, layoutCoordsX, layoutCoordsY);
            HandleMouseSelectionDrag(target, mouse, layoutCoordsX, layoutCoordsY);
        }

        private void HandleScrollbarThumbDrag(Vector2 position)
        {
            if (_renderer == null || _scrollState.ScrollbarDragPath == null) return;
            if (!_renderer.RenderedScrollbars.TryGetValue(_scrollState.ScrollbarDragPath, out var scrollbar)) return;

            var (_, mouseY) = PaperUtility.ToLayoutCoords(position);
            float dragDelta = mouseY - _scrollState.ScrollbarDragAnchorY;
            float usableTrackHeight = scrollbar.TrackH - scrollbar.ThumbH;
            float scrollDelta = usableTrackHeight > 0 ? dragDelta * scrollbar.MaxScroll / usableTrackHeight : 0f;
            float newScrollY = Math.Max(0f, Math.Min(scrollbar.MaxScroll, _scrollState.ScrollbarDragAnchorScroll + scrollDelta));
            var (currentScrollX, _) = _scrollState.ScrollOffsets.TryGetValue(_scrollState.ScrollbarDragPath, out var currentOffsets) ? currentOffsets : (0f, 0f);
            _scrollState.ScrollOffsets[_scrollState.ScrollbarDragPath] = (currentScrollX, newScrollY);
            _scrollState.ScrollbarLastActive[_scrollState.ScrollbarDragPath] = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
            MarkDirty(animationSeconds: 2.0);
        }

        private void HandleDragAndDropMove(Fiber? target, IMouse mouse, float layoutCoordsX, float layoutCoordsY)
        {
            if (_uiState.DragSource == null || !mouse.IsButtonPressed(MouseButton.Left)) return;

            const float dragThreshold = 4f;
            float deltaX = layoutCoordsX - _uiState.DragStartX;
            float deltaY = layoutCoordsY - _uiState.DragStartY;

            if (!_uiState.DragActive && (deltaX * deltaX + deltaY * deltaY) >= dragThreshold * dragThreshold)
            {
                _uiState.DragActive = true;
                var startEvent = new DragEvent { Type = DragEventType.DragStart, X = layoutCoordsX, Y = layoutCoordsY };
                DispatchDrag(_uiState.DragSource, startEvent);
                _uiState.DragData = startEvent.Data;
            }

            if (!_uiState.DragActive) return;

            _uiState.DragCursorX = layoutCoordsX;
            _uiState.DragCursorY = layoutCoordsY;

            if (_uiState.DragSource!.Props.OnDrag != null)
                DispatchDrag(_uiState.DragSource, new DragEvent { Type = DragEventType.Drag, X = layoutCoordsX, Y = layoutCoordsY, Data = _uiState.DragData });

            if (!ReferenceEquals(target, _uiState.DragOver))
            {
                if (_uiState.DragOver != null)
                    DispatchDrag(_uiState.DragOver, new DragEvent { Type = DragEventType.DragLeave, X = layoutCoordsX, Y = layoutCoordsY, Data = _uiState.DragData,
                        LocalX = layoutCoordsX - _uiState.DragOver.Layout.AbsoluteX, LocalY = layoutCoordsY - _uiState.DragOver.Layout.AbsoluteY,
                        TargetWidth = _uiState.DragOver.Layout.Width, TargetHeight = _uiState.DragOver.Layout.Height });
                if (target != null)
                    DispatchDrag(target, new DragEvent { Type = DragEventType.DragEnter, X = layoutCoordsX, Y = layoutCoordsY, Data = _uiState.DragData,
                        LocalX = layoutCoordsX - target.Layout.AbsoluteX, LocalY = layoutCoordsY - target.Layout.AbsoluteY,
                        TargetWidth = target.Layout.Width, TargetHeight = target.Layout.Height });
                _uiState.DragOver     = target;
                _uiState.DragOverPath = target != null ? FiberTreeUtility.GetPathString(target) : null;
            }
            else if (target != null)
            {
                DispatchDrag(target, new DragEvent { Type = DragEventType.DragOver, X = layoutCoordsX, Y = layoutCoordsY, Data = _uiState.DragData,
                    LocalX = layoutCoordsX - target.Layout.AbsoluteX, LocalY = layoutCoordsY - target.Layout.AbsoluteY,
                    TargetWidth = target.Layout.Width, TargetHeight = target.Layout.Height });
            }
        }

        private void HandleMouseSelectionDrag(Fiber? target, IMouse mouse, float layoutCoordsX, float layoutCoordsY)
        {
            if (!_inputState.InputSelecting || _inputState.FocusedPath == null) return;
            if (!mouse.IsButtonPressed(MouseButton.Left)) return;
            if (_inputState.Focused == null || !InputTextUtility.IsTextInput(_inputState.Focused.Type as string)) return;

            var (scrollX, scrollY) = GetTotalScrollForPath(_inputState.FocusedPath);
            var focusedLayout = _inputState.Focused.Layout;
            float inputLeft = focusedLayout.AbsoluteX - scrollX;
            float inputRight = inputLeft + focusedLayout.Width;
            float inputTop = focusedLayout.AbsoluteY - scrollY;
            float inputBottom = inputTop + focusedLayout.Height;

            float clampedMouseX = Math.Clamp(layoutCoordsX, inputLeft, inputRight);
            float clampedMouseY = Math.Clamp(layoutCoordsY, inputTop, inputBottom);

            float inputScroll = _inputState.InputScrollX;
            int newCaretIndex = GetCaretIndexFromX(_inputState.Focused, clampedMouseX, clampedMouseY, scrollX, scrollY, inputScroll);

            _inputState.InputCaret = newCaretIndex;
            _inputState.InputSelStart = Math.Min(_inputState.InputSelAnchor, newCaretIndex);
            _inputState.InputSelEnd = Math.Max(_inputState.InputSelAnchor, newCaretIndex);

            RequestRender();
        }

        private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
        {
            if (_reconciler?.Root == null) return;

            var (mouseX, mouseY) = PaperUtility.ToLayoutCoords(mouse.Position);
            var target = HitTestAll(mouseX, mouseY);
            if (target == null) return;

            var wheelEvent = new PointerEvent
            {
                Type = PointerEventType.Wheel,
                X = mouseX,
                Y = mouseY,
                WheelDeltaX = wheel.X,
                WheelDeltaY = wheel.Y,
            };

            var pathToRoot = FiberTreeUtility.PathToRoot(target);
            for (int pathIndex = pathToRoot.Count - 1; pathIndex >= 0; pathIndex--)
            {
                var node = pathToRoot[pathIndex];

                if (node.Props?.OnWheel != null)
                {
                    node.Props.OnWheel(wheelEvent);
                    return;
                }

                var style = node.ComputedStyle;
                if (style.OverflowY == Core.Styles.Overflow.Scroll || style.OverflowY == Core.Styles.Overflow.Auto ||
                    style.OverflowX == Core.Styles.Overflow.Scroll || style.OverflowX == Core.Styles.Overflow.Auto)
                {
                    string containerPath = string.Join(".", pathToRoot.Take(pathIndex + 1).Select(fiber => fiber.Index));
                    var (currentScrollX, currentScrollY) = _scrollState.ScrollOffsets.TryGetValue(containerPath, out var currentOffsets) ? currentOffsets : (0f, 0f);

                    const float scrollStep = 24f;
                    float newScrollX = Math.Max(0, currentScrollX - (float)wheel.X * scrollStep);
                    float newScrollY = Math.Max(0, currentScrollY - (float)wheel.Y * scrollStep);

                    if (_renderer != null && _renderer.RenderedScrollbars.TryGetValue(containerPath, out var scrollbarGeometry))
                    {
                        newScrollY = Math.Min(newScrollY, scrollbarGeometry.MaxScroll);
                        newScrollX = Math.Min(newScrollX, scrollbarGeometry.MaxScrollX);
                    }

                    _scrollState.ScrollOffsets[containerPath] = (newScrollX, newScrollY);
                    _scrollState.ScrollbarLastActive[containerPath] = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
                    MarkDirty(animationSeconds: 2.0);
                    break;
                }
            }
        }

        /// <summary>Total scroll offset affecting a node (sum of ancestor path scrolls).</summary>
        private (float scrollX, float scrollY) GetTotalScrollForPath(string path)
        {
            float accumulatedScrollX = 0f, accumulatedScrollY = 0f;
            if (string.IsNullOrEmpty(path)) return (accumulatedScrollX, accumulatedScrollY);
            var parts = path.Split('.');
            for (int partIndex = 1; partIndex < parts.Length; partIndex++)
            {
                var prefix = string.Join(".", parts, 0, partIndex);
                if (_scrollState.ScrollOffsets.TryGetValue(prefix, out var offsets))
                {
                    accumulatedScrollX += offsets.scrollX;
                    accumulatedScrollY += offsets.scrollY;
                }
            }
            return (accumulatedScrollX, accumulatedScrollY);
        }
    }
}
