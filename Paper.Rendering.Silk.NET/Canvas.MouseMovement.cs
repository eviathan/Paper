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

            // Clean up stale drag state if the button was released while cursor was in another window.
            if (_uiState.DragActive && !mouse.IsButtonPressed(MouseButton.Left))
            {
                // Synthesise DragEnd so component-level drag state (e.g. DockPanel dragCtx /
                // IsDraggingPanel) is cleared — without this, drop-zone overlays stay rendered
                // and block hit tests on panel headers, making panels appear un-draggable.
                var (lx, ly) = PaperUtility.ToLayoutCoords(position);
                if (_uiState.DragSource != null)
                    DispatchDrag(_uiState.DragSource, new DragEvent
                    {
                        Type = DragEventType.DragEnd,
                        X    = lx,
                        Y    = ly,
                        Data = _uiState.DragData,
                        OutsideSourceWindow = false,
                    });
                _uiState.DragActive     = false;
                _uiState.DragSource     = null;
                _uiState.DragSourcePath = null;
                _uiState.DragData       = null;
                if (_uiState.DragOver != null)
                    _uiState.DragOver = null;
                _uiState.DragOverPath = null;
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
                // During drag, update hover so drop zone fibers show their hover style.
                // Skip pointer-enter/leave — those are for normal hover, not drag-hover.
                if (!ReferenceEquals(target, _uiState.Hovered))
                {
                    _uiState.Hovered     = target;
                    _uiState.HoveredPath = target != null ? FiberTreeUtility.GetPathString(target) : null;
                }
                ApplyGlfwCursor(target?.ComputedStyle.Cursor ?? Paper.Core.Styles.Cursor.Default);
                MarkDirty();
            }

            if ((mouse.IsButtonPressed(MouseButton.Left) || mouse.IsButtonPressed(MouseButton.Middle)) && _pointerDownFiber != null)
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

            // Source window: keep session cursor position current so other windows can render the ghost.
            if (_uiState.DragActive && _dockSession?.IsCrossWindowDragActive == true && _window != null)
            {
                var winPos = _window.Position;
                _dockSession.UpdateCrossWindowCursorPosition(
                    winPos.X + (int)layoutCoordsX,
                    winPos.Y + (int)layoutCoordsY);
            }

            HandleCrossWindowDragMove(target, mouse, layoutCoordsX, layoutCoordsY);
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

        private void HandleCrossWindowDragMove(Fiber? target, IMouse mouse, float layoutCoordsX, float layoutCoordsY)
        {
            // No local drag active — check if a panel is being dragged from another OS window.
            if (_uiState.DragActive || _dockSession == null) return;

            var crossData = GetCrossWindowDragData();

            if (crossData == null)
            {
                // Session drag ended or cancelled — clean up any lingering state.
                if (_uiState.CrossWindowDragActive)
                {
                    if (_uiState.CrossWindowDragOver != null)
                    {
                        DispatchDrag(_uiState.CrossWindowDragOver, new DragEvent
                            { Type = DragEventType.DragLeave, X = layoutCoordsX, Y = layoutCoordsY, Data = _uiState.CrossWindowDragData });
                        _uiState.CrossWindowDragOver     = null;
                        _uiState.CrossWindowDragOverPath = null;
                    }
                    _uiState.CrossWindowDragActive = false;
                    _uiState.CrossWindowDragData   = null;
                    MarkDirty();
                }
                return;
            }

            if (!mouse.IsButtonPressed(MouseButton.Left))
            {
                // Button already released — ignore.
                if (_uiState.CrossWindowDragActive)
                {
                    _uiState.CrossWindowDragActive = false;
                    _uiState.CrossWindowDragData   = null;
                    _uiState.CrossWindowDragOver   = null;
                }
                return;
            }

            _uiState.CrossWindowDragData = crossData;
            _uiState.CrossWindowDragX = layoutCoordsX;
            _uiState.CrossWindowDragY = layoutCoordsY;

            if (!_uiState.CrossWindowDragActive)
            {
                _uiState.CrossWindowDragActive = true;
                if (target != null)
                {
                    DispatchDrag(target, new DragEvent
                    {
                        Type = DragEventType.DragEnter, X = layoutCoordsX, Y = layoutCoordsY, Data = crossData,
                        LocalX = layoutCoordsX - target.Layout.AbsoluteX, LocalY = layoutCoordsY - target.Layout.AbsoluteY,
                        TargetWidth = target.Layout.Width, TargetHeight = target.Layout.Height,
                    });
                    _uiState.CrossWindowDragOver     = target;
                    _uiState.CrossWindowDragOverPath = FiberTreeUtility.GetPathString(target);
                }
            }
            else if (!ReferenceEquals(target, _uiState.CrossWindowDragOver))
            {
                if (_uiState.CrossWindowDragOver != null)
                    DispatchDrag(_uiState.CrossWindowDragOver, new DragEvent
                        { Type = DragEventType.DragLeave, X = layoutCoordsX, Y = layoutCoordsY, Data = crossData,
                          LocalX = layoutCoordsX - _uiState.CrossWindowDragOver.Layout.AbsoluteX,
                          LocalY = layoutCoordsY - _uiState.CrossWindowDragOver.Layout.AbsoluteY,
                          TargetWidth = _uiState.CrossWindowDragOver.Layout.Width, TargetHeight = _uiState.CrossWindowDragOver.Layout.Height });
                if (target != null)
                    DispatchDrag(target, new DragEvent
                        { Type = DragEventType.DragEnter, X = layoutCoordsX, Y = layoutCoordsY, Data = crossData,
                          LocalX = layoutCoordsX - target.Layout.AbsoluteX, LocalY = layoutCoordsY - target.Layout.AbsoluteY,
                          TargetWidth = target.Layout.Width, TargetHeight = target.Layout.Height });
                _uiState.CrossWindowDragOver     = target;
                _uiState.CrossWindowDragOverPath = target != null ? FiberTreeUtility.GetPathString(target) : null;
            }
            else if (target != null)
            {
                DispatchDrag(target, new DragEvent
                    { Type = DragEventType.DragOver, X = layoutCoordsX, Y = layoutCoordsY, Data = crossData,
                      LocalX = layoutCoordsX - target.Layout.AbsoluteX, LocalY = layoutCoordsY - target.Layout.AbsoluteY,
                      TargetWidth = target.Layout.Width, TargetHeight = target.Layout.Height });
            }
            MarkDirty();
        }

        // Called each render frame when another window has a cross-window drag active.
        // macOS GLFW implicit grab means this window never receives OnMouseMove during
        // another window's drag, so we inject a synthetic move from the session's last
        // known screen-space cursor position to keep drop zones highlighted.
        internal void SyntheticCrossWindowDragMove()
        {
            if (_dockSession == null || !_dockSession.IsCrossWindowDragActive) return;
            if (_uiState.DragActive) return;
            if (_window == null || _reconciler?.Root == null) return;

            var screenPos = _window.Position;
            float localX = _dockSession.CrossDragCursorScreenX - screenPos.X;
            float localY = _dockSession.CrossDragCursorScreenY - screenPos.Y;

            var crossData = GetCrossWindowDragData();
            if (crossData == null) return;

            var target = HitTestAll(localX, localY);

            // Update hover state so the zone under cursor shows its hover style.
            if (!ReferenceEquals(target, _uiState.Hovered))
            {
                _uiState.Hovered     = target;
                _uiState.HoveredPath = target != null ? FiberTreeUtility.GetPathString(target) : null;
                MarkDirty();
            }

            _uiState.CrossWindowDragData = crossData;
            _uiState.CrossWindowDragX = localX;
            _uiState.CrossWindowDragY = localY;

            if (!_uiState.CrossWindowDragActive)
            {
                _uiState.CrossWindowDragActive = true;
                if (target != null)
                {
                    DispatchDrag(target, new DragEvent
                    {
                        Type = DragEventType.DragEnter, X = localX, Y = localY, Data = crossData,
                        LocalX = localX - target.Layout.AbsoluteX, LocalY = localY - target.Layout.AbsoluteY,
                        TargetWidth = target.Layout.Width, TargetHeight = target.Layout.Height,
                    });
                    _uiState.CrossWindowDragOver     = target;
                    _uiState.CrossWindowDragOverPath = FiberTreeUtility.GetPathString(target);
                }
            }
            else if (!ReferenceEquals(target, _uiState.CrossWindowDragOver))
            {
                if (_uiState.CrossWindowDragOver != null)
                    DispatchDrag(_uiState.CrossWindowDragOver, new DragEvent
                    {
                        Type = DragEventType.DragLeave, X = localX, Y = localY, Data = crossData,
                        LocalX = localX - _uiState.CrossWindowDragOver.Layout.AbsoluteX,
                        LocalY = localY - _uiState.CrossWindowDragOver.Layout.AbsoluteY,
                        TargetWidth = _uiState.CrossWindowDragOver.Layout.Width,
                        TargetHeight = _uiState.CrossWindowDragOver.Layout.Height,
                    });
                if (target != null)
                    DispatchDrag(target, new DragEvent
                    {
                        Type = DragEventType.DragEnter, X = localX, Y = localY, Data = crossData,
                        LocalX = localX - target.Layout.AbsoluteX, LocalY = localY - target.Layout.AbsoluteY,
                        TargetWidth = target.Layout.Width, TargetHeight = target.Layout.Height,
                    });
                _uiState.CrossWindowDragOver     = target;
                _uiState.CrossWindowDragOverPath = target != null ? FiberTreeUtility.GetPathString(target) : null;
            }
            else if (target != null)
            {
                DispatchDrag(target, new DragEvent
                {
                    Type = DragEventType.DragOver, X = localX, Y = localY, Data = crossData,
                    LocalX = localX - target.Layout.AbsoluteX, LocalY = localY - target.Layout.AbsoluteY,
                    TargetWidth = target.Layout.Width, TargetHeight = target.Layout.Height,
                });
            }
        }

        // Called by the DockSession ExternalPanelArrived handler (macOS eject path):
        // fire a Drop event on whichever zone fiber the synthetic cursor was hovering,
        // then clean up cross-window state exactly as OnMouseButtonUp does.
        internal void SyntheticCrossWindowDrop(Paper.Core.Dock.PanelNode panel)
        {
            if (_reconciler?.Root == null) return;

            var screenPos = _window?.Position ?? default;
            float localX = _dockSession?.CrossDragCursorScreenX - screenPos.X ?? 0;
            float localY = _dockSession?.CrossDragCursorScreenY - screenPos.Y ?? 0;

            // Build the cross-window payload from the arriving panel.
            var crossData = new Paper.Core.Dock.DockDragPayload(panel.PanelId, null, false) { IsCrossWindow = true };

            // Use the currently hovered zone fiber; fall back to a fresh hit test.
            var dropTarget = _uiState.CrossWindowDragOver ?? HitTestAll(localX, localY);
            Console.WriteLine($"[DockDbg] SyntheticCrossWindowDrop: panel={panel.PanelId} winId={WindowId} local=({localX},{localY}) crossWindowDragOver={_uiState.CrossWindowDragOver != null} dropTarget={dropTarget?.Type}({dropTarget?.Props?.OnDrop != null})");

            if (dropTarget != null)
                DispatchDrag(dropTarget, new DragEvent
                {
                    Type = DragEventType.Drop, X = localX, Y = localY, Data = crossData,
                    LocalX = localX - dropTarget.Layout.AbsoluteX,
                    LocalY = localY - dropTarget.Layout.AbsoluteY,
                    TargetWidth = dropTarget.Layout.Width, TargetHeight = dropTarget.Layout.Height,
                });

            if (_uiState.CrossWindowDragOver != null)
            {
                DispatchDrag(_uiState.CrossWindowDragOver, new DragEvent
                    { Type = DragEventType.DragLeave, X = localX, Y = localY, Data = crossData });
                _uiState.CrossWindowDragOver     = null;
                _uiState.CrossWindowDragOverPath = null;
            }
            _uiState.CrossWindowDragActive = false;
            _uiState.CrossWindowDragData   = null;
            _uiState.Hovered               = null;
            _uiState.HoveredPath           = null;
            MarkDirty();
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
                Console.WriteLine($"[DockDbg] DragActivated: source={_uiState.DragSource?.Type} path={_uiState.DragSourcePath}");
                var startEvent = new DragEvent { Type = DragEventType.DragStart, X = layoutCoordsX, Y = layoutCoordsY };
                DispatchDrag(_uiState.DragSource, startEvent);
                _uiState.DragData = startEvent.Data;
                Console.WriteLine($"[DockDbg] DragStart dispatched: hasData={_uiState.DragData != null}");
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
                    wheelEvent.LocalX       = mouseX - node.Layout.AbsoluteX;
                    wheelEvent.LocalY       = mouseY - node.Layout.AbsoluteY;
                    wheelEvent.TargetWidth  = node.Layout.Width;
                    wheelEvent.TargetHeight = node.Layout.Height;
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
