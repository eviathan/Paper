using Paper.Core.Reconciler;
using Paper.Rendering.Silk.NET.Models;
using Paper.Rendering.Silk.NET.Utilities;

namespace Paper.Rendering.Silk.NET
{
    // Focus/text-input-caret state — ported from Canvas.Focus.cs. Canvas's own version reads and
    // writes the same InputState shape this class also uses, so the logic here is unchanged from
    // Canvas's; only the surrounding method signatures (Stage 2/PaperEmbeddedSurface.Keyboard.cs)
    // differ, since there's no IKeyboard/IInputContext to read modifier keys or clipboard from.
    public sealed partial class PaperEmbeddedSurface
    {
        private readonly InputState _inputState = new();

        private void SetFocus(Fiber? next)
        {
            if (ReferenceEquals(next, _inputState.Focused)) return;

            var previous = _inputState.Focused;
            _inputState.Focused = next;
            _inputState.FocusedPath = next != null ? GetPathString(_reconciler?.Root, next) : null;

            if (next != null && next.Type is string nextType && InputTextUtility.IsTextInput(nextType))
            {
                _inputState.InputText = next.Props.Text ?? "";
                int textLength = _inputState.InputText.Length;
                _inputState.InputCaret = textLength;
                // AutoFocus (keyboard-triggered rename) → select all so typing immediately replaces.
                // Click-triggered focus → no selection; mouse handler sets caret to click position.
                _inputState.InputSelStart = next.Props.AutoFocus ? 0 : textLength;
                _inputState.InputSelEnd   = textLength;
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
            _renderRequested = true;
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

        /// <summary>Recursive child-then-sibling walk for the first AutoFocus-eligible fiber —
        /// ported from Canvas.Rendering.cs's FindAutoFocus, called once per reconcile so a
        /// freshly-mounted auto-focus input actually gets focus.</summary>
        private static Fiber? FindAutoFocus(Fiber? fiber)
        {
            if (fiber == null) return null;
            if (fiber.Props.AutoFocus == true && HitTestUtility.IsFocusable(fiber)) return fiber;

            var childResult = FindAutoFocus(fiber.Child);
            if (childResult != null) return childResult;

            return FindAutoFocus(fiber.Sibling);
        }
    }
}
