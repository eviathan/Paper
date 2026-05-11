using Paper.Core.Events;
using Paper.Core.Reconciler;
using Paper.Core.VirtualDom;
using Paper.Rendering.Silk.NET.Utilities;
using Silk.NET.Input;

namespace Paper.Rendering.Silk.NET
{
    public sealed partial class Canvas
    {
        private void OnMouseButtonDown(IMouse mouse, MouseButton button)
        {
            if (_reconciler?.Root == null || _window == null) return;

            var (mouseX, mouseY) = PaperUtility.ToLayoutCoords(mouse.Position);

            if (_renderer != null)
            {
                foreach (var kvp in _renderer.RenderedScrollbars)
                {
                    var scrollbar = kvp.Value;
                    if (mouseX >= scrollbar.TrackX && mouseX <= scrollbar.TrackX + 6f &&
                        mouseY >= scrollbar.ThumbY && mouseY <= scrollbar.ThumbY + scrollbar.ThumbH)
                    {
                        _scrollState.ScrollbarDragPath = kvp.Key;
                        _scrollState.ScrollbarDragAnchorY = mouseY;
                        _scrollState.ScrollbarDragAnchorScroll = _scrollState.ScrollOffsets.TryGetValue(kvp.Key, out var savedScroll) ? savedScroll.scrollY : 0f;
                        return;
                    }
                }
            }

            var target = HitTestAll(mouseX, mouseY);
            _uiState.Pressed = target;
            _uiState.PressedPath = target != null ? FiberTreeUtility.GetPathString(target) : null;
            if (button == MouseButton.Left || button == MouseButton.Middle)
            {
                _pointerDownFiber     = target;
                _pointerDownFiberPath = target != null ? FiberTreeUtility.GetPathString(target) : null;
            }

            Fiber? dragCandidate = target;
            while (dragCandidate != null && dragCandidate.Props.OnDragStart == null)
                dragCandidate = dragCandidate.Parent;

            Console.WriteLine($"[DockDbg] MouseDown: pos=({mouseX:F0},{mouseY:F0}) target={target?.Type}(onDragStart={target?.Props?.OnDragStart != null}) dragCandidate={dragCandidate?.Type} priorDragActive={_uiState.DragActive} priorDragSource={_uiState.DragSource?.Type}");

            if (button == MouseButton.Left && dragCandidate != null)
            {
                // A local drag is beginning — discard any stale cross-window state so it
                // cannot intercept the upcoming mouse-up event.
                _uiState.CrossWindowDragActive   = false;
                _uiState.CrossWindowDragData     = null;
                _uiState.CrossWindowDragOver     = null;
                _uiState.CrossWindowDragOverPath = null;

                _uiState.DragSource = dragCandidate;
                _uiState.DragSourcePath = FiberTreeUtility.GetPathString(dragCandidate);
                _uiState.DragData = null;
                _uiState.DragActive = false;
                _uiState.DragStartX = mouseX;
                _uiState.DragStartY = mouseY;
                _uiState.DragOver = null;
            }
            else
            {
                _uiState.DragSource = null;
                _uiState.DragSourcePath = null;
                _uiState.DragActive = false;
            }

            DispatchPointer(target, new PointerEvent
            {
                Type = PointerEventType.Down,
                X = mouseX,
                Y = mouseY,
                Button = button == MouseButton.Left ? 0 : button == MouseButton.Right ? 1 : 2,
            });

            Fiber? focusTarget = InputTextUtility.GetInputAncestorOrSelf(target) ?? target;
            if (HitTestUtility.IsFocusable(focusTarget))
            {
                SetFocus(focusTarget);
                if (focusTarget != null && focusTarget.Type is string elementType && InputTextUtility.IsTextInput(elementType))
                {
                    _inputState.LastInputActivityTicks = Environment.TickCount64;
                    StartCaretBlinkTimer();
                    var (scrollX, scrollY) = GetTotalScrollForPath(_inputState.FocusedPath ?? "");
                    float inputScroll = (focusTarget == _inputState.Focused && InputTextUtility.IsTextInput(focusTarget.Type as string)) ? _inputState.InputScrollX : 0f;
                    int caretIndex = GetCaretIndexFromX(focusTarget, mouseX, mouseY, scrollX, scrollY, inputScroll);
                    _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = _inputState.InputSelAnchor = caretIndex;
                    _inputState.InputSelecting = true;
                    RequestRender();
                }
            }
        }

        private void OnMouseButtonUp(IMouse mouse, MouseButton button)
        {
            if (_reconciler?.Root == null || _window == null) return;

            if (_scrollState.ScrollbarDragPath != null) { _scrollState.ScrollbarDragPath = null; return; }

            var (mouseX, mouseY) = PaperUtility.ToLayoutCoords(mouse.Position);
            var target = HitTestAll(mouseX, mouseY);

            if (button == MouseButton.Left && _uiState.CrossWindowDragActive && !_uiState.DragActive)
            {
                // Panel dragged from another OS window — complete the cross-window drop here.
                // Guard: if a local drag is also active, CrossWindowDragActive is stale (leftover
                // from a previous cross-window op that ended without SyntheticCrossWindowDrop).
                // In that case fall through to the local drag-end branch instead.
                var crossData = _uiState.CrossWindowDragData;
                DispatchDrag(target, new DragEvent { Type = DragEventType.Drop, X = mouseX, Y = mouseY, Data = crossData,
                    LocalX = target != null ? mouseX - target.Layout.AbsoluteX : 0,
                    LocalY = target != null ? mouseY - target.Layout.AbsoluteY : 0,
                    TargetWidth = target?.Layout.Width ?? 0, TargetHeight = target?.Layout.Height ?? 0 });
                if (_uiState.CrossWindowDragOver != null)
                {
                    DispatchDrag(_uiState.CrossWindowDragOver, new DragEvent { Type = DragEventType.DragLeave, X = mouseX, Y = mouseY, Data = crossData });
                    _uiState.CrossWindowDragOver     = null;
                    _uiState.CrossWindowDragOverPath = null;
                }
                _uiState.CrossWindowDragActive = false;
                _uiState.CrossWindowDragData   = null;
                MarkDirty();
            }
            else if (button == MouseButton.Left && _uiState.DragActive && _uiState.DragSource != null)
            {
                // Clear any stale cross-window state that was left over from a previous operation.
                _uiState.CrossWindowDragActive   = false;
                _uiState.CrossWindowDragData     = null;
                _uiState.CrossWindowDragOver     = null;
                _uiState.CrossWindowDragOverPath = null;

                bool outsideWindow = mouseX < 0 || mouseY < 0 || mouseX > _width || mouseY > _height;
                Console.WriteLine($"[DockDbg] MouseUp: pos=({mouseX},{mouseY}) windowSize=({_width},{_height}) outsideWindow={outsideWindow} hasData={_uiState.DragData != null}");
                var winPos = _window?.Position ?? default;
                int screenX = winPos.X + (int)mouseX;
                int screenY = winPos.Y + (int)mouseY;
                DispatchDrag(target, new DragEvent { Type = DragEventType.Drop, X = mouseX, Y = mouseY, Data = _uiState.DragData,
                    LocalX = target != null ? mouseX - target.Layout.AbsoluteX : 0,
                    LocalY = target != null ? mouseY - target.Layout.AbsoluteY : 0,
                    TargetWidth = target?.Layout.Width ?? 0, TargetHeight = target?.Layout.Height ?? 0 });
                DispatchDrag(_uiState.DragSource, new DragEvent { Type = DragEventType.DragEnd, X = mouseX, Y = mouseY, Data = _uiState.DragData,
                    OutsideSourceWindow = outsideWindow, ScreenX = screenX, ScreenY = screenY });

                if (_uiState.DragOver != null)
                {
                    DispatchDrag(_uiState.DragOver, new DragEvent { Type = DragEventType.DragLeave, X = mouseX, Y = mouseY, Data = _uiState.DragData });
                    _uiState.DragOver     = null;
                    _uiState.DragOverPath = null;
                }
                _uiState.DragSource = null;
                _uiState.DragSourcePath = null;
                _uiState.DragActive = false;
                _uiState.DragData = null;
            }
            else if (button == MouseButton.Left)
            {
                _uiState.DragSource = null;
                _uiState.DragSourcePath = null;
                _uiState.DragActive = false;
            }

            if ((button == MouseButton.Left || button == MouseButton.Middle) && _pointerDownFiber != null && !ReferenceEquals(_pointerDownFiber, target))
            {
                DispatchPointer(_pointerDownFiber, new PointerEvent
                {
                    Type = PointerEventType.Up,
                    X = mouseX,
                    Y = mouseY,
                    Button = 0,
                });
            }
            if (button == MouseButton.Left || button == MouseButton.Middle) { _pointerDownFiber = null; _pointerDownFiberPath = null; }

            DispatchPointer(target, new PointerEvent
            {
                Type = PointerEventType.Up,
                X = mouseX,
                Y = mouseY,
                Button = button == MouseButton.Left ? 0 : button == MouseButton.Right ? 1 : 2,
            });

            bool sameControl = target != null && _uiState.PressedPath != null &&
                               FiberTreeUtility.GetPathString(target) == _uiState.PressedPath;

            if (button == MouseButton.Left && target != null && sameControl)
            {
                var now = DateTime.UtcNow;
                bool isDouble = (now - _clickState.LastClickAtUtc).TotalMilliseconds < 350;
                _clickState.LastClickAtUtc = now;
                if (!isDouble) _clickState.LastClickWasDoubleOnInput = false;

                DispatchPointer(target, new PointerEvent
                {
                    Type = isDouble ? PointerEventType.DoubleClick : PointerEventType.Click,
                    X = mouseX,
                    Y = mouseY,
                    Button = 0,
                });

                HandleDoubleClickTextSelection(target, isDouble, mouseX, mouseY, now);

                Fiber? clickTarget = HitTestUtility.GetClickTarget(target);
                if (clickTarget != null)
                {
                    try
                    {
                        if (isDouble && clickTarget.Props.OnDoubleClick != null)
                            clickTarget.Props.OnDoubleClick.Invoke();
                        else if (clickTarget.Type is string elementType && elementType == ElementTypes.Checkbox && clickTarget.Props.OnCheckedChange != null)
                            clickTarget.Props.OnCheckedChange(!clickTarget.Props.Checked);
                        else
                            clickTarget.Props.OnClick?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[Paper] Click handler error: " + ex.ToString());
                    }
                    RequestRender();
                }
            }

            _uiState.Pressed = null;
            _uiState.PressedPath = null;
            _inputState.InputSelecting = false;
        }

        private void HandleDoubleClickTextSelection(Fiber target, bool isDouble, float mouseX, float mouseY, DateTime now)
        {
            if (!isDouble || _inputState.Focused == null || _inputState.FocusedPath == null) return;

            Fiber? inputFiber = InputTextUtility.GetInputAncestorOrSelf(target);
            if (inputFiber == null) { _clickState.LastClickWasDoubleOnInput = false; return; }
            if (FiberTreeUtility.GetPathString(inputFiber) != _inputState.FocusedPath) { _clickState.LastClickWasDoubleOnInput = false; return; }
            if (inputFiber.Type is not string inputElementType || !InputTextUtility.IsTextInput(inputElementType)) { _clickState.LastClickWasDoubleOnInput = false; return; }

            var currentText = _inputState.InputText ?? inputFiber.Props?.Text ?? "";
            int textLength = currentText.Length;
            bool sameInputAsLastDouble = _clickState.LastClickWasDoubleOnInput &&
                                         _clickState.LastDoubleClickInputPath == _inputState.FocusedPath &&
                                         (now - _clickState.LastDoubleClickAtUtc).TotalMilliseconds < 350;

            if (sameInputAsLastDouble)
            {
                _inputState.InputSelStart = 0;
                _inputState.InputSelEnd = textLength;
                _inputState.InputCaret = textLength;
            }
            else
            {
                var (wordStart, wordEnd) = InputTextUtility.GetWordBounds(currentText, _inputState.InputCaret);
                _inputState.InputSelStart = wordStart;
                _inputState.InputSelEnd = wordEnd;
                _inputState.InputCaret = wordEnd;
            }

            _inputState.LastInputActivityTicks = Environment.TickCount64;
            _clickState.LastClickWasDoubleOnInput = true;
            _clickState.LastDoubleClickInputPath = _inputState.FocusedPath;
            _clickState.LastDoubleClickAtUtc = now;
        }

        private void DispatchDrag(Fiber? target, DragEvent dragEvent)
        {
            if (target == null) return;
            var pathToRoot = FiberTreeUtility.PathToRoot(target);
            for (int i = pathToRoot.Count - 1; i >= 0; i--)
            {
                var fiber = pathToRoot[i];
                var handler = dragEvent.Type switch
                {
                    DragEventType.DragStart => fiber.Props.OnDragStart,
                    DragEventType.Drag => fiber.Props.OnDrag,
                    DragEventType.DragEnd => fiber.Props.OnDragEnd,
                    DragEventType.DragEnter => fiber.Props.OnDragEnter,
                    DragEventType.DragOver => fiber.Props.OnDragOver,
                    DragEventType.DragLeave => fiber.Props.OnDragLeave,
                    DragEventType.Drop => fiber.Props.OnDrop,
                    _ => null,
                };
                handler?.Invoke(dragEvent);
            }
        }
    }
}
