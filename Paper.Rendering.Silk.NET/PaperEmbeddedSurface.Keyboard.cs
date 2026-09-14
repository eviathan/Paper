using Paper.Core.Events;
using Paper.Core.Reconciler;
using Paper.Core.VirtualDom;
using Paper.Rendering.Silk.NET.Utilities;
using Silk.NET.Input;

namespace Paper.Rendering.Silk.NET
{
    // Keyboard/text-editing input forwarding — ported from Canvas.Keyboard.cs. The editing logic
    // itself (caret movement, selection, backspace/delete, cut/copy/paste) is unchanged; only the
    // entry points differ, since there's no IKeyboard/IInputContext here for reading modifier-key
    // state or clipboard text — the host (whatever owns the real input loop) supplies both
    // explicitly. Deliberately NOT ported: Canvas's GlobalKeyFilter/KeyboardShortcutRegistry
    // pre-dispatch — those are app-wide registries a full Paper application sets up for itself;
    // routing a host game's key events through them here would be surprising for an embedded
    // overlay that doesn't own the rest of the app.
    public sealed partial class PaperEmbeddedSurface
    {
        /// <summary>Forward a key-down event. <paramref name="ctrl"/> should be true for
        /// whichever modifier the host treats as its shortcut key (Ctrl on Windows/Linux, Cmd on
        /// macOS) — Paper doesn't distinguish them itself.</summary>
        public void HandleKeyDown(Key key, bool shift = false, bool ctrl = false, bool alt = false)
        {
            if (_reconciler?.Root == null) return;

            var target = _inputState.Focused;

            if (key == Key.Tab)
            {
                HandleTabFocusNavigation(shift);
                return;
            }

            if (target == null) return;

            if ((key == Key.Enter || key == Key.KeypadEnter || key == Key.Space)
                && !(target.Type is string activatable && InputTextUtility.IsTextInput(activatable)))
            {
                ActivateFocused();
                return;
            }

            string keyName = key.ToString();

            if (target.Type is string elementType && InputTextUtility.IsTextInput(elementType))
            {
                HandleTextInputKeyDown(key, keyName, ctrl, alt, shift, target);
            }
            else
            {
                target.Props.OnKeyDown?.Invoke(keyName);
                DispatchKey(target, new KeyEvent { Type = KeyEventType.Down, Key = keyName });
            }
        }

        public void HandleKeyUp(Key key, bool shift = false, bool ctrl = false, bool alt = false)
        {
            var target = _inputState.Focused;
            if (target == null) return;
            string keyName = key.ToString();
            target.Props.OnKeyUp?.Invoke(keyName);
            DispatchKey(target, new KeyEvent { Type = KeyEventType.Up, Key = keyName });
        }

        /// <summary>Forward a typed character (text-input composition) — call this in addition to
        /// <see cref="HandleKeyDown"/>, matching how a host's own character-input event works
        /// alongside its raw key-down event.</summary>
        public void HandleCharInput(char character)
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

            _renderRequested = true;
        }

        /// <summary>Every focusable fiber in tab order: positive TabIndex first (ascending), then
        /// TabIndex 0/unset in tree order — the same ordering Tab/Shift+Tab navigate through, and
        /// what "focus the first element" (<see cref="ResetFocus"/>) means as a result.</summary>
        private List<Fiber> BuildTabOrder()
        {
            var all = new List<Fiber>();
            HitTestUtility.CollectFocusable(_reconciler!.Root, all);

            var ordered = new List<Fiber>();
            ordered.AddRange(all.Where(fiber => (fiber.Props.TabIndex ?? 0) > 0).OrderBy(fiber => fiber.Props.TabIndex));
            ordered.AddRange(all.Where(fiber => fiber.Props.TabIndex == 0 || fiber.Props.TabIndex == null));
            return ordered;
        }

        private void HandleTabFocusNavigation(bool shift)
        {
            var ordered = BuildTabOrder();
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

        private void HandleTextInputKeyDown(Key key, string keyName, bool ctrl, bool alt, bool shift, Fiber target)
        {
            bool shortcutMod = ctrl;
            bool canModify = !target.Props.ReadOnly && !target.Props.Disabled;
            _inputState.LastInputActivityTicks = Environment.TickCount64;
            var currentText = _inputState.InputText ?? target.Props.Text ?? "";
            InputTextUtility.ClampInputIndices(currentText.Length, ref _inputState.InputCaret, ref _inputState.InputSelStart, ref _inputState.InputSelEnd);
            int textLength = currentText.Length;
            bool handled = true;

            if (key == Key.Left)
                HandleCaretLeft(currentText, shortcutMod, alt, shift);
            else if (key == Key.Right)
                HandleCaretRight(currentText, textLength, shortcutMod, alt, shift);
            else if (key == Key.Up && InputTextUtility.IsMultiLineInput(target.Type as string) && !shortcutMod)
            {
                int targetPosition = InputTextUtility.CaretUpLine(currentText, _inputState.InputCaret);
                _inputState.InputCaret = targetPosition;
                if (shift) _inputState.InputSelEnd = targetPosition; else _inputState.InputSelStart = _inputState.InputSelEnd = targetPosition;
            }
            else if (key == Key.Down && InputTextUtility.IsMultiLineInput(target.Type as string) && !shortcutMod)
            {
                int targetPosition = InputTextUtility.CaretDownLine(currentText, _inputState.InputCaret);
                _inputState.InputCaret = targetPosition;
                if (shift) _inputState.InputSelEnd = targetPosition; else _inputState.InputSelStart = _inputState.InputSelEnd = targetPosition;
            }
            else if (key == Key.Enter && InputTextUtility.IsMultiLineInput(target.Type as string))
                HandleEnterKey(currentText, canModify, target);
            else if (key == Key.Home || (shortcutMod && key == Key.Up))
            {
                if (shift) _inputState.InputSelEnd = 0; else _inputState.InputSelStart = _inputState.InputSelEnd = 0;
                _inputState.InputCaret = 0;
            }
            else if (key == Key.End || (shortcutMod && key == Key.Down))
            {
                if (shift) _inputState.InputSelEnd = textLength; else _inputState.InputSelStart = _inputState.InputSelEnd = textLength;
                _inputState.InputCaret = textLength;
            }
            else if (key == Key.Backspace)
                HandleBackspace(currentText, canModify, alt, target);
            else if (key == Key.Delete)
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
                    _setClipboard?.Invoke(currentText[selectionMin..selectionMax]);
                    target.Props.OnCopy?.Invoke(currentText[selectionMin..selectionMax]);
                }
            }
            else if (shortcutMod && key == Key.X)
                HandleCut(currentText, canModify, target);
            else if (shortcutMod && key == Key.V)
                HandlePaste(currentText, canModify, target);
            else
                handled = false;

            if (handled) { _renderRequested = true; return; }

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

        private void HandleCut(string currentText, bool canModify, Fiber target)
        {
            if (!canModify || target.Props.OnChange == null) return;
            int selectionMin = Math.Min(_inputState.InputSelStart, _inputState.InputSelEnd);
            int selectionMax = Math.Max(_inputState.InputSelStart, _inputState.InputSelEnd);
            if (selectionMin >= selectionMax) return;
            string cutText = currentText[selectionMin..selectionMax];
            _setClipboard?.Invoke(cutText);
            target.Props.OnCut?.Invoke(cutText);
            var remaining = currentText[..selectionMin] + currentText[selectionMax..];
            _inputState.InputText = remaining;
            target.Props.OnChange(remaining);
            _inputState.InputCaret = _inputState.InputSelStart = _inputState.InputSelEnd = selectionMin;
        }

        private void HandlePaste(string currentText, bool canModify, Fiber target)
        {
            if (!canModify || target.Props.OnChange == null) return;
            var paste = _getClipboard?.Invoke() ?? "";
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

        /// <summary>Synthesizes a click on the currently-focused fiber — shared by Enter/Space
        /// (<see cref="HandleKeyDown"/>) and directional-navigation confirm (<c>ActivateFocusedElement</c>).</summary>
        private void ActivateFocused()
        {
            if (_inputState.Focused == null || _reconciler?.Root == null || _rootFactory == null) return;
            var e = new PointerEvent { X = 0, Y = 0 };
            DispatchPointerEvent(_reconciler.Root, _inputState.Focused, e, fiber => fiber.Props?.OnPointerClick);
            _reconciler.Update(_rootFactory(Props.Empty), forceReconcile: false);
            _needsLayout = true;
            _renderRequested = true;
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
