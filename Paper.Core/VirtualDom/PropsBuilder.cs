using Paper.Core.Styles;
using Paper.Core.Events;

namespace Paper.Core.VirtualDom
{
    /// <summary>
    /// Fluent builder for <see cref="Props"/>.
    /// </summary>
    public sealed class PropsBuilder
    {
        private readonly Dictionary<string, object?> _data = new();

        // ── Style & class ─────────────────────────────────────────────────────

        public PropsBuilder Style(StyleSheet style) { _data["style"] = style; return this; }
        public PropsBuilder ClassName(string cls) { _data["className"] = cls; return this; }
        public PropsBuilder Id(string id) { _data["id"] = id; return this; }

        // ── Content ───────────────────────────────────────────────────────────

        public PropsBuilder Text(string text) { _data["text"] = text; return this; }
        public PropsBuilder Src(string src) { _data["src"] = src; return this; }

        // ── Children ─────────────────────────────────────────────────────────

        public PropsBuilder Children(params UINode[] nodes)
        {
            _data["children"] = (IReadOnlyList<UINode>)nodes;
            return this;
        }

        public PropsBuilder Children(IEnumerable<UINode> nodes)
        {
            _data["children"] = (IReadOnlyList<UINode>)nodes.ToList();
            return this;
        }

        // ── Events ────────────────────────────────────────────────────────────

        public PropsBuilder OnClick(Action handler) { _data["onClick"] = handler; return this; }
        public PropsBuilder OnDoubleClick(Action handler) { _data["onDoubleClick"] = handler; return this; }
        public PropsBuilder OnMouseEnter(Action handler) { _data["onMouseEnter"] = handler; return this; }
        public PropsBuilder OnMouseLeave(Action handler) { _data["onMouseLeave"] = handler; return this; }
        public PropsBuilder OnMouseDown(Action handler) { _data["onMouseDown"] = handler; return this; }
        public PropsBuilder OnMouseUp(Action handler) { _data["onMouseUp"] = handler; return this; }
        public PropsBuilder OnChange(Action<string> handler) { _data["onChange"] = handler; return this; }
        public PropsBuilder OnFocus(Action handler) { _data["onFocus"] = handler; return this; }
        public PropsBuilder OnBlur(Action handler) { _data["onBlur"] = handler; return this; }
        public PropsBuilder OnSubmit(Action handler) { _data["onSubmit"] = handler; return this; }
        public PropsBuilder OnKeyDown(Action<string> handler) { _data["onKeyDown"] = handler; return this; }
        public PropsBuilder OnKeyUp(Action<string> handler) { _data["onKeyUp"] = handler; return this; }

        public PropsBuilder OnPointerMove(Action<PointerEvent> handler) { _data["onPointerMove"] = handler; return this; }
        public PropsBuilder OnPointerMoveCapture(Action<PointerEvent> handler) { _data["onPointerMoveCapture"] = handler; return this; }
        public PropsBuilder OnPointerDown(Action<PointerEvent> handler) { _data["onPointerDown"] = handler; return this; }
        public PropsBuilder OnPointerDownCapture(Action<PointerEvent> handler) { _data["onPointerDownCapture"] = handler; return this; }
        public PropsBuilder OnPointerUp(Action<PointerEvent> handler) { _data["onPointerUp"] = handler; return this; }
        public PropsBuilder OnPointerUpCapture(Action<PointerEvent> handler) { _data["onPointerUpCapture"] = handler; return this; }
        public PropsBuilder OnPointerClick(Action<PointerEvent> handler) { _data["onPointerClick"] = handler; return this; }
        public PropsBuilder OnPointerClickCapture(Action<PointerEvent> handler) { _data["onPointerClickCapture"] = handler; return this; }
        public PropsBuilder OnPointerEnter(Action<PointerEvent> handler) { _data["onPointerEnter"] = handler; return this; }
        public PropsBuilder OnPointerLeave(Action<PointerEvent> handler) { _data["onPointerLeave"] = handler; return this; }
        public PropsBuilder OnWheel(Action<PointerEvent> handler) { _data["onWheel"] = handler; return this; }

        public PropsBuilder OnKeyDown(Action<KeyEvent> handler) { _data["onKeyDown2"] = handler; return this; }
        public PropsBuilder OnKeyDownCapture(Action<KeyEvent> handler) { _data["onKeyDownCapture2"] = handler; return this; }
        public PropsBuilder OnKeyUp(Action<KeyEvent> handler) { _data["onKeyUp2"] = handler; return this; }
        public PropsBuilder OnKeyUpCapture(Action<KeyEvent> handler) { _data["onKeyUpCapture2"] = handler; return this; }
        public PropsBuilder OnKeyChar(Action<KeyEvent> handler) { _data["onKeyChar"] = handler; return this; }
        public PropsBuilder OnKeyCharCapture(Action<KeyEvent> handler) { _data["onKeyCharCapture"] = handler; return this; }

        public PropsBuilder OnDragStart(Action<DragEvent> handler) { _data["onDragStart"] = handler; return this; }
        public PropsBuilder OnDrag(Action<DragEvent> handler) { _data["onDrag"] = handler; return this; }
        public PropsBuilder OnDragEnd(Action<DragEvent> handler) { _data["onDragEnd"] = handler; return this; }
        public PropsBuilder OnDragEnter(Action<DragEvent> handler) { _data["onDragEnter"] = handler; return this; }
        public PropsBuilder OnDragOver(Action<DragEvent> handler) { _data["onDragOver"] = handler; return this; }
        public PropsBuilder OnDragLeave(Action<DragEvent> handler) { _data["onDragLeave"] = handler; return this; }
        public PropsBuilder OnDrop(Action<DragEvent> handler) { _data["onDrop"] = handler; return this; }

        // ── Clipboard events ─────────────────────────────────────────────────

        public PropsBuilder OnCopy(Action<string> handler) { _data["onCopy"] = handler; return this; }
        public PropsBuilder OnCut(Action<string> handler) { _data["onCut"] = handler; return this; }
        public PropsBuilder OnPaste(Action<string> handler) { _data["onPaste"] = handler; return this; }

        // ── Input attributes ─────────────────────────────────────────────────

        public PropsBuilder Placeholder(string placeholder) { _data["placeholder"] = placeholder; return this; }
        public PropsBuilder MaxLength(int maxLength) { _data["maxLength"] = maxLength; return this; }
        public PropsBuilder ReadOnly(bool readOnly = true) { _data["readOnly"] = readOnly; return this; }
        public PropsBuilder Disabled(bool disabled = true) { _data["disabled"] = disabled; return this; }
        public PropsBuilder InputType(string inputType) { _data["inputType"] = inputType; return this; }

        // ── Accessibility ─────────────────────────────────────────────────────────

        public PropsBuilder Role(string role) { _data["role"] = role; return this; }
        public PropsBuilder TabIndex(int tabIndex) { _data["tabIndex"] = tabIndex; return this; }
        public PropsBuilder AriaLabel(string label) { _data["aria-label"] = label; return this; }
        public PropsBuilder AriaLabelledBy(string id) { _data["aria-labelledby"] = id; return this; }
        public PropsBuilder AriaDescribedBy(string id) { _data["aria-describedby"] = id; return this; }
        public PropsBuilder AriaExpanded(bool expanded = true) { _data["aria-expanded"] = expanded; return this; }
        public PropsBuilder AriaChecked(bool checked_) { _data["aria-checked"] = checked_; return this; }
        public PropsBuilder AriaSelected(bool selected = true) { _data["aria-selected"] = selected; return this; }
        public PropsBuilder AriaDisabled(bool disabled = true) { _data["aria-disabled"] = disabled; return this; }
        public PropsBuilder AriaValueText(string text) { _data["aria-valuetext"] = text; return this; }
        public PropsBuilder AriaLive(string politeness) { _data["aria-live"] = politeness; return this; }
        public PropsBuilder AriaHasPopup(bool hasPopup = true) { _data["aria-haspopup"] = hasPopup.ToString().ToLower(); return this; }
        public PropsBuilder AriaHidden(bool hidden = true) { _data["aria-hidden"] = hidden; return this; }
        public PropsBuilder AriaInvalid(bool invalid = true) { _data["aria-invalid"] = invalid; return this; }
        public PropsBuilder AriaLevel(int level) { _data["aria-level"] = level; return this; }
        public PropsBuilder AriaControls(string ids) { _data["aria-controls"] = ids; return this; }
        public PropsBuilder AriaOrientation(string orientation) { _data["aria-orientation"] = orientation; return this; }

        // ── Custom ────────────────────────────────────────────────────────────

        public PropsBuilder Set(string key, object? value) { _data[key] = value; return this; }

        // ── Build ─────────────────────────────────────────────────────────────

        public Props Build() => new Props(new Dictionary<string, object?>(_data));

        public static implicit operator Props(PropsBuilder b) => b.Build();
    }
}
