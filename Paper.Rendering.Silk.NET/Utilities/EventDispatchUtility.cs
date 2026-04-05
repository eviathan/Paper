using Paper.Core.Events;
using Paper.Core.Reconciler;

namespace Paper.Rendering.Silk.NET.Utilities
{
    /// <summary>Routes pointer and keyboard events to fiber prop handlers respecting capture/bubble phases.</summary>
    public static class EventDispatchUtility
    {
        public static void InvokePointerHandlers(Fiber fiber, PointerEvent pointerEvent, bool capture)
        {
            var props = fiber.Props;
            switch (pointerEvent.Type)
            {
                case PointerEventType.Move:
                    (capture ? props.OnPointerMoveCapture : props.OnPointerMove)?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Down:
                    (capture ? props.OnPointerDownCapture : props.OnPointerDown)?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Up:
                    (capture ? props.OnPointerUpCapture : props.OnPointerUp)?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Click:
                    (capture ? props.OnPointerClickCapture : props.OnPointerClick)?.Invoke(pointerEvent);
                    break;
                case PointerEventType.DoubleClick:
                    (capture ? props.OnPointerClickCapture : props.OnPointerClick)?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Enter:
                    props.OnPointerEnter?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Leave:
                    props.OnPointerLeave?.Invoke(pointerEvent);
                    break;
                case PointerEventType.Wheel:
                    props.OnWheel?.Invoke(pointerEvent);
                    break;
            }
        }

        public static void InvokeKeyHandlers(Fiber fiber, KeyEvent keyEvent, bool capture)
        {
            var props = fiber.Props;
            switch (keyEvent.Type)
            {
                case KeyEventType.Down:
                    (capture
                        ? props.OnKeyDownEventCapture
                        : props.OnKeyDownEvent
                    )?.Invoke(keyEvent);
                    break;
                case KeyEventType.Up:
                    (capture
                        ? props.OnKeyUpEventCapture
                        : props.OnKeyUpEvent
                    )?.Invoke(keyEvent);
                    break;
                case KeyEventType.Char:
                    (capture
                        ? props.OnKeyCharCapture
                        : props.OnKeyChar
                    )?.Invoke(keyEvent);
                    break;
            }
        }
    }
}
