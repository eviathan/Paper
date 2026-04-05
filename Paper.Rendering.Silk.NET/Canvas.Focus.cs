using Paper.Core.Events;
using Paper.Core.Reconciler;
using Paper.Rendering.Silk.NET.Models;
using Paper.Rendering.Silk.NET.Utilities;

namespace Paper.Rendering.Silk.NET
{
    public sealed partial class Canvas
    {
        private Fiber? HitTestAll(float x, float y)
        {
            Func<string, (float, float)> getScroll = path => _scrollState.ScrollOffsets.TryGetValue(path, out var offsets) ? offsets : (0f, 0f);
            var target = HitTestUtility.HitTest(_reconciler?.Root, x, y, "", 0, 0f, 0f, getScroll);
            if (_reconciler?.PortalRoots is { Count: > 0 } portals)
            {
                foreach (var portal in portals)
                {
                    var hit = HitTestUtility.HitTest(portal, x, y, "", 0, 0f, 0f, getScroll);
                    if (hit != null) target = hit;
                }
            }
            return target;
        }

        private void DispatchPointer(Fiber? target, PointerEvent pointerEvent)
        {
            if (target == null || _reconciler?.Root == null) return;
            pointerEvent.LocalX = pointerEvent.X - target.Layout.AbsoluteX;
            pointerEvent.LocalY = pointerEvent.Y - target.Layout.AbsoluteY;
            pointerEvent.TargetWidth = target.Layout.Width;
            pointerEvent.TargetHeight = target.Layout.Height;
            var path = FiberTreeUtility.PathToRoot(target);

            pointerEvent.Phase = EventPhase.Capturing;
            for (int i = 0; i < path.Count - 1 && !pointerEvent.PropagationStopped; i++)
                EventDispatchUtility.InvokePointerHandlers(path[i], pointerEvent, capture: true);

            if (!pointerEvent.PropagationStopped)
            {
                pointerEvent.Phase = EventPhase.AtTarget;
                EventDispatchUtility.InvokePointerHandlers(target, pointerEvent, capture: false);
                EventDispatchUtility.InvokePointerHandlers(target, pointerEvent, capture: true);
            }

            pointerEvent.Phase = EventPhase.Bubbling;
            for (int i = path.Count - 2; i >= 0 && !pointerEvent.PropagationStopped; i--)
                EventDispatchUtility.InvokePointerHandlers(path[i], pointerEvent, capture: false);
        }

        private void SetFocus(Fiber? next)
        {
            if (ReferenceEquals(next, _inputState.Focused)) return;

            var previous = _inputState.Focused;
            _inputState.Focused = next;
            _inputState.FocusedPath = next != null ? FiberTreeUtility.GetPathString(next) : null;

            if (next != null && next.Type is string nextType && InputTextUtility.IsTextInput(nextType))
            {
                _inputState.InputText = next.Props.Text ?? "";
                int textLength = _inputState.InputText.Length;
                _inputState.InputCaret = textLength;
                _inputState.InputSelStart = textLength;
                _inputState.InputSelEnd = textLength;
                _inputState.LastInputActivityTicks = Environment.TickCount64;
                StartCaretBlinkTimer();
            }
            else
            {
                _inputState.InputText = null;
                StopCaretBlinkTimer();
            }

            previous?.Props.OnBlur?.Invoke();
            next?.Props.OnFocus?.Invoke();
        }

        private void StartCaretBlinkTimer()
        {
            _inputState.CaretBlinkTimer ??= new Timer(_ => RequestRender(), null, InputState.CaretBlinkOnMs, InputState.CaretBlinkOnMs);
            _inputState.CaretBlinkTimer.Change(InputState.CaretBlinkOnMs, InputState.CaretBlinkOnMs);
        }

        private void StopCaretBlinkTimer()
        {
            _inputState.CaretBlinkTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>True when the caret should be drawn: solid while recently active, else blinking (on phase).</summary>
        private bool ComputeCaretVisible()
        {
            if (_inputState.Focused == null || !InputTextUtility.IsTextInput(_inputState.Focused.Type as string))
                return true;
            long elapsed = Environment.TickCount64 - _inputState.LastInputActivityTicks;
            if (elapsed < InputState.CaretIdleMs) return true;
            return (Environment.TickCount64 % InputState.CaretBlinkPeriodMs) < InputState.CaretBlinkOnMs;
        }
    }
}
