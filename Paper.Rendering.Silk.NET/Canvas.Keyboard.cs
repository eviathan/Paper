using Paper.Core.Events;
using Paper.Core.Hooks;
using Paper.Core.Reconciler;
using Paper.Core.VirtualDom;
using Paper.Rendering.Silk.NET.Utilities;
using Silk.NET.Input;

namespace Paper.Rendering.Silk.NET
{
    public sealed partial class Canvas
    {
        private void OnKeyDown(IKeyboard keyboard, Key key, int _)
        {
            string keyName = key.ToString();
            bool ctrl = keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight);
            bool cmd = keyboard.IsKeyPressed(Key.SuperLeft) || keyboard.IsKeyPressed(Key.SuperRight);
            bool alt = keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight);
            bool shortcutMod = ctrl || cmd;
            bool shift = keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight);

            if (KeyboardShortcutRegistry.TryDispatch(keyName, ctrl, alt, shift, cmd, out bool shortcutHandled) && shortcutHandled)
                return;

            var target = _inputState.Focused;
            if (target == null) return;

            if (key == Key.Tab && _reconciler?.Root != null)
            {
                HandleTabFocusNavigation(shift);
                return;
            }

            if (target.Type is string elementType && InputTextUtility.IsTextInput(elementType))
            {
                HandleTextInputKeyDown(keyboard, key, keyName, cmd, alt, shift, shortcutMod, target);
            }
            else
            {
                target.Props.OnKeyDown?.Invoke(keyName);
                DispatchKey(target, new KeyEvent { Type = KeyEventType.Down, Key = keyName });
            }
        }

        private void HandleTabFocusNavigation(bool shift)
        {
            var all = new List<Fiber>();
            HitTestUtility.CollectFocusable(_reconciler!.Root, all);

            var ordered = new List<Fiber>();
            ordered.AddRange(all.Where(fiber => (fiber.Props.TabIndex ?? 0) > 0).OrderBy(fiber => fiber.Props.TabIndex));
            ordered.AddRange(all.Where(fiber => fiber.Props.TabIndex == 0 || fiber.Props.TabIndex == null));

            if (ordered.Count == 0) return;

            int currentFocusIndex = ordered.FindIndex(fiber => ReferenceEquals(fiber, _inputState.Focused));
            int nextFocusIndex = shift
                ? (currentFocusIndex <= 0 ? ordered.Count - 1 : currentFocusIndex - 1)
                : (currentFocusIndex < 0 || currentFocusIndex >= ordered.Count - 1 ? 0 : currentFocusIndex + 1);

            SetFocus(ordered[nextFocusIndex]);
            if (_inputState.InputText != null)
            {
                _inputState.InputSelStart = 0;
                _inputState.InputSelEnd = _inputState.InputText.Length;
                _inputState.InputCaret = _inputState.InputText.Length;
            }
        }

        private void HandleTextInputKeyDown(IKeyboard keyboard, Key key, string keyName, bool cmd, bool alt, bool shift, bool shortcutMod, Fiber target)
        {
            bool canModify = !target.Props.ReadOnly && !target.Props.Disabled;
            _inputState.LastInputActivityTicks = Environment.TickCount64;
            var currentText = _inputState.InputText ?? target.Props.Text ?? "";
            InputTextUtility.ClampInputIndices(currentText.Length, ref _inputState.InputCaret, ref _inputState.InputSelStart, ref _inputState.InputSelEnd);
            int textLength = currentText.Length;
            bool handled = true;

            if (key == Key.Left)
                HandleCaretLeft(currentText, cmd, alt, shift);
            else if (key == Key.Right)
                HandleCaretRight(currentText, textLength, cmd, alt, shift);
            else if (key == Key.Up && InputTextUtility.IsMultiLineInput(target.Type as string) && !cmd)
            {
                int targetPosition = InputTextUtility.CaretUpLine(currentText, _inputState.InputCaret);
                _inputState.InputCaret = targetPosition;
                if (shift) _inputState.InputSelEnd = targetPosition; else _inputState.InputSelStart = _inputState.InputSelEnd = targetPosition;
            }
            else if (key == Key.Down && InputTextUtility.IsMultiLineInput(target.Type as string) && !cmd)
            {
                int targetPosition = InputTextUtility.CaretDownLine(currentText, _inputState.InputCaret);
                _inputState.InputCaret = targetPosition;
                if (shift) _inputState.InputSelEnd = targetPosition; else _inputState.InputSelStart = _inputState.InputSelEnd = targetPosition;
            }
            else if (key == Key.Enter && InputTextUtility.IsMultiLineInput(target.Type as string))
                HandleEnterKey(currentText, canModify, target);
            else if (key == Key.Home || (cmd && key == Key.Up))
            {
                if (shift) _inputState.InputSelEnd = 0; else _inputState.InputSelStart = _inputState.InputSelEnd = 0;
                _inputState.InputCaret = 0;
            }
            else if (key == Key.End || (cmd && key == Key.Down))
            {
                if (shift) _inputState.InputSelEnd = textLength; else _inputState.InputSelStart = _inputState.InputSelEnd = textLength;
                _inputState.InputCaret = textLength;
            }
            else if (key == Key.Backspace || keyName.Equals("Backspace", StringComparison.OrdinalIgnoreCase))
                HandleBackspace(currentText, canModify, alt, target);
            else if (key == Key.Delete || keyName.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                HandleDelete(currentText, textLength, canModify, target);
            else if (shortcutMod && key == Key.A)
            {
                _inputState.InputSelStart = 0;
                _inputState.InputSelEnd = textLength;
                _inputState.InputCaret = textLength;
            }
            else if (shortcutMod && key == Key.C)
            {
                int selectionMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
                int selectionMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
                if (textLength > 0 && selectionMin < selectionMax)
                {
                    keyboard.ClipboardText = currentText[selectionMin..selectionMax];
                    target.Props.OnCopy?.Invoke(currentText[selectionMin..selectionMax]);
                }
            }
            else if (shortcutMod && key == Key.X)
                HandleCut(currentText, canModify, keyboard, target);
            else if (shortcutMod && key == Key.V)
                HandlePaste(currentText, canModify, keyboard, target);
            else
                handled = false;

            if (handled) { MarkDirty(); return; }

            target.Props.OnKeyDown?.Invoke(keyName);
            DispatchKey(target, new KeyEvent { Type = KeyEventType.Down, Key = keyName });
        }

        private void HandleCaretLeft(string currentText, bool cmd, bool alt, bool shift)
        {
            int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
            int targetPosition;
            if (cmd) targetPosition = 0;
            else if (alt) targetPosition = InputTextUtility.WordStartBefore(currentText, _inputState.InputCaret);
            else if (!shift && _inputState.InputSelStart != _inputState.InputSelEnd) targetPosition = selMin;
            else targetPosition = Math.Max(0, _inputState.InputCaret - 1);
            _inputState.InputCaret = targetPosition;
            if (shift) _inputState.InputSelEnd = targetPosition; else _inputState.InputSelStart = _inputState.InputSelEnd = targetPosition;
        }

        private void HandleCaretRight(string currentText, int textLength, bool cmd, bool alt, bool shift)
        {
            int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
            int targetPosition;
            if (cmd) targetPosition = textLength;
            else if (alt) targetPosition = InputTextUtility.WordEndAfter(currentText, _inputState.InputCaret);
            else if (!shift && _inputState.InputSelStart != _inputState.InputSelEnd) targetPosition = selMax;
            else targetPosition = Math.Min(textLength, _inputState.InputCaret + 1);
            _inputState.InputCaret = targetPosition;
            if (shift) _inputState.InputSelEnd = targetPosition; else _inputState.InputSelStart = _inputState.InputSelEnd = targetPosition;
        }

        private void HandleEnterKey(string currentText, bool canModify, Fiber target)
        {
            if (!canModify || target.Props.OnChange == null) return;
            int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
            int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
            string nextText = currentText[..selMin] + "\n" + currentText[selMax..];
            int nextCaret = selMin + 1;
            _inputState.InputText = nextText;
            target.Props.OnChange(nextText);
            _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret;
        }

        private void HandleBackspace(string currentText, bool canModify, bool alt, Fiber target)
        {
            if (!canModify || target.Props.OnChange == null) return;
            int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
            int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
            string nextText = currentText;
            int nextCaret = _inputState.InputCaret;
            if (selMin != selMax)
            {
                nextText = currentText[..selMin] + currentText[selMax..];
                nextCaret = selMin;
            }
            else if (alt && _inputState.InputCaret > 0)
            {
                int wordStart = InputTextUtility.WordStartBefore(currentText, _inputState.InputCaret);
                nextText = currentText[..wordStart] + currentText[_inputState.InputCaret..];
                nextCaret = wordStart;
            }
            else if (_inputState.InputCaret > 0)
            {
                nextText = currentText[..(_inputState.InputCaret - 1)] + currentText[_inputState.InputCaret..];
                nextCaret = _inputState.InputCaret - 1;
            }
            if (nextText != currentText) { _inputState.InputText = nextText; target.Props.OnChange(nextText); _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret; }
        }

        private void HandleDelete(string currentText, int textLength, bool canModify, Fiber target)
        {
            if (!canModify || target.Props.OnChange == null) return;
            int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
            int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
            string nextText = currentText;
            int nextCaret = _inputState.InputCaret;
            if (selMin != selMax)
            {
                nextText = currentText[..selMin] + currentText[selMax..];
                nextCaret = selMin;
            }
            else if (_inputState.InputCaret < textLength)
            {
                nextText = currentText[.._inputState.InputCaret] + currentText[(_inputState.InputCaret + 1)..];
                nextCaret = _inputState.InputCaret;
            }
            if (nextText != currentText) { _inputState.InputText = nextText; target.Props.OnChange(nextText); _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret; }
        }

        private void HandleCut(string currentText, bool canModify, IKeyboard keyboard, Fiber target)
        {
            if (!canModify || target.Props.OnChange == null) return;
            int selectionMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
            int selectionMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
            if (selectionMin >= selectionMax) return;
            string cutText = currentText[selectionMin..selectionMax];
            keyboard.ClipboardText = cutText;
            target.Props.OnCut?.Invoke(cutText);
            var remaining = currentText[..selectionMin] + currentText[selectionMax..];
            _inputState.InputText = remaining;
            target.Props.OnChange(remaining);
            _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = selectionMin;
        }

        private void HandlePaste(string currentText, bool canModify, IKeyboard keyboard, Fiber target)
        {
            if (!canModify || target.Props.OnChange == null) return;
            var paste = keyboard.ClipboardText ?? "";
            if (paste.Length == 0) return;
            int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
            int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
            int maxLen = target.Props.MaxLength ?? int.MaxValue;
            int insertLen = currentText.Length - (selMax - selMin) + paste.Length;
            if (insertLen > maxLen)
            {
                int available = maxLen - (currentText.Length - (selMax - selMin));
                paste = available > 0 ? paste[..available] : "";
            }
            if (paste.Length == 0) return;
            string nextText = currentText[..selMin] + paste + currentText[selMax..];
            int nextCaret = selMin + paste.Length;
            _inputState.InputText = nextText;
            target.Props.OnChange(nextText);
            target.Props.OnPaste?.Invoke(paste);
            _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret;
        }

        private void OnKeyUp(IKeyboard keyboard, Key key, int _)
        {
            var target = _inputState.Focused;
            if (target == null) return;
            string keyName = key.ToString();
            target.Props.OnKeyUp?.Invoke(keyName);
            DispatchKey(target, new KeyEvent { Type = KeyEventType.Up, Key = keyName });
        }

        private void OnKeyChar(IKeyboard keyboard, char character)
        {
            var target = _inputState.Focused;
            if (target == null) return;

            DispatchKey(target, new KeyEvent { Type = KeyEventType.Char, Key = character.ToString(), Char = character });

            if (target.Type is not string elementType || !InputTextUtility.IsTextInput(elementType) || target.Props.OnChange == null)
                return;
            if (target.Props.ReadOnly || target.Props.Disabled) return;

            _inputState.LastInputActivityTicks = Environment.TickCount64;

            var currentText = _inputState.InputText ?? target.Props.Text ?? "";
            InputTextUtility.ClampInputIndices(currentText.Length, ref _inputState.InputCaret, ref _inputState.InputSelStart, ref _inputState.InputSelEnd);
            int selMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
            int selMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
            int maxLen = target.Props.MaxLength ?? int.MaxValue;

            if (character == '\b') return;

            bool isNumberInput = target.Props.InputType == "number";

            if (InputTextUtility.IsMultiLineInput(elementType) && (character == '\n' || character == '\r'))
            {
                int insertLen = currentText.Length - (selMax - selMin) + 1;
                if (insertLen > maxLen) return;
                string nextText = currentText[..selMin] + "\n" + currentText[selMax..];
                int nextCaret = selMin + 1;
                _inputState.InputText = nextText;
                target.Props.OnChange(nextText);
                _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret;
            }
            else if (!char.IsControl(character))
            {
                if (isNumberInput && !char.IsDigit(character) && character != '-' && character != '.' && character != ',') return;
                if (isNumberInput && character == '-' && selMin > 0) return;
                if ((character == '.' || character == ',') && currentText.Contains('.') && currentText.Contains(',')) return;
                string insert = character.ToString();
                int insertLen = currentText.Length - (selMax - selMin) + insert.Length;
                if (insertLen > maxLen) return;
                string nextText = currentText[..selMin] + insert + currentText[selMax..];
                int nextCaret = selMin + insert.Length;
                _inputState.InputText = nextText;
                target.Props.OnChange(nextText);
                _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = nextCaret;
            }
        }

        private void DispatchKey(Fiber target, KeyEvent keyEvent)
        {
            var path = FiberTreeUtility.PathToRoot(target);

            keyEvent.Phase = EventPhase.Capturing;
            for (int i = 0; i < path.Count - 1 && !keyEvent.PropagationStopped; i++)
                EventDispatchUtility.InvokeKeyHandlers(path[i], keyEvent, capture: true);

            if (!keyEvent.PropagationStopped)
            {
                keyEvent.Phase = EventPhase.AtTarget;
                EventDispatchUtility.InvokeKeyHandlers(target, keyEvent, capture: false);
                EventDispatchUtility.InvokeKeyHandlers(target, keyEvent, capture: true);
            }

            keyEvent.Phase = EventPhase.Bubbling;
            for (int i = path.Count - 2; i >= 0 && !keyEvent.PropagationStopped; i--)
                EventDispatchUtility.InvokeKeyHandlers(path[i], keyEvent, capture: false);
        }
    }
}
