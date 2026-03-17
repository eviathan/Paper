using System.Reflection;
using Paper.Core.Styles;
using Paper.Core.Events;

namespace Paper.Core.VirtualDom
{
    /// <summary>
    /// Immutable property bag passed to a UI element.
    /// Supports well-known props (style, events, children) plus arbitrary custom data.
    /// </summary>
    public sealed class Props
    {
        private readonly IReadOnlyDictionary<string, object?> _data;

        public static readonly Props Empty = new(new Dictionary<string, object?>());

        public Props(IReadOnlyDictionary<string, object?> data)
        {
            _data = data;
        }

        // ── Well-known props ──────────────────────────────────────────────────

        public StyleSheet?           Style        => Get<StyleSheet>("style");
        public StyleSheet?           HoverStyle   => Get<StyleSheet>("hoverStyle");
        public StyleSheet?           ActiveStyle  => Get<StyleSheet>("activeStyle");
        public StyleSheet?           FocusStyle   => Get<StyleSheet>("focusStyle");
        public string?               ClassName    => Get<string>("className");
        public string?               Id           => Get<string>("id");
        public string?               Text         => Get<string>("text");
        public string?               Src          => Get<string>("src");   // image source
        public bool                  Checked      => Get<bool>("checked");
        public Action<bool>?         OnCheckedChange => Get<Action<bool>>("onCheckedChange");
        public int? Rows => _data.TryGetValue("rows", out var v) && v is int i ? i : null;
        public IReadOnlyList<(string Value, string Label)>? Options => Get<IReadOnlyList<(string Value, string Label)>>("options");
        public string? SelectedValue => Get<string>("selectedValue");
        public Action<string>? OnSelect => Get<Action<string>>("onSelect");
        public string? Value => Get<string>("value");
        public bool RadioChecked => Get<bool>("checked");
        public uint                  TextureHandle => Get<uint>("textureHandle");

        // ── Children ─────────────────────────────────────────────────────────

        public IReadOnlyList<UINode> Children =>
            Get<IReadOnlyList<UINode>>("children") ?? Array.Empty<UINode>();

        // ── Events ────────────────────────────────────────────────────────────

        public Action?          OnClick       => Get<Action>("onClick");
        public Action<PointerEvent>? OnPointerClick => Get<Action<PointerEvent>>("onPointerClick");
        public Action<PointerEvent>? OnPointerClickCapture => Get<Action<PointerEvent>>("onPointerClickCapture");

        public Action?          OnDoubleClick => Get<Action>("onDoubleClick");
        public Action?          OnMouseEnter  => Get<Action>("onMouseEnter");
        public Action?          OnMouseLeave  => Get<Action>("onMouseLeave");
        public Action?          OnMouseDown   => Get<Action>("onMouseDown");
        public Action?          OnMouseUp     => Get<Action>("onMouseUp");
        public Action<string>?  OnChange      => Get<Action<string>>("onChange");
        public Action?          OnFocus       => Get<Action>("onFocus");
        public Action?          OnBlur        => Get<Action>("onBlur");
        public Action?          OnSubmit      => Get<Action>("onSubmit");
        public Action<string>?  OnKeyDown     => Get<Action<string>>("onKeyDown");
        public Action<string>?  OnKeyUp       => Get<Action<string>>("onKeyUp");

        public Action<PointerEvent>? OnPointerMove        => Get<Action<PointerEvent>>("onPointerMove");
        public Action<PointerEvent>? OnPointerMoveCapture => Get<Action<PointerEvent>>("onPointerMoveCapture");
        public Action<PointerEvent>? OnPointerDown        => Get<Action<PointerEvent>>("onPointerDown");
        public Action<PointerEvent>? OnPointerDownCapture => Get<Action<PointerEvent>>("onPointerDownCapture");
        public Action<PointerEvent>? OnPointerUp          => Get<Action<PointerEvent>>("onPointerUp");
        public Action<PointerEvent>? OnPointerUpCapture   => Get<Action<PointerEvent>>("onPointerUpCapture");
        public Action<PointerEvent>? OnPointerEnter       => Get<Action<PointerEvent>>("onPointerEnter");
        public Action<PointerEvent>? OnPointerLeave       => Get<Action<PointerEvent>>("onPointerLeave");
        public Action<PointerEvent>? OnWheel              => Get<Action<PointerEvent>>("onWheel");

        public Action<KeyEvent>? OnKeyDownEvent        => Get<Action<KeyEvent>>("onKeyDown2");
        public Action<KeyEvent>? OnKeyDownEventCapture => Get<Action<KeyEvent>>("onKeyDownCapture2");
        public Action<KeyEvent>? OnKeyUpEvent          => Get<Action<KeyEvent>>("onKeyUp2");
        public Action<KeyEvent>? OnKeyUpEventCapture   => Get<Action<KeyEvent>>("onKeyUpCapture2");
        public Action<KeyEvent>? OnKeyChar         => Get<Action<KeyEvent>>("onKeyChar");
        public Action<KeyEvent>? OnKeyCharCapture  => Get<Action<KeyEvent>>("onKeyCharCapture");

        // ── Input attributes ─────────────────────────────────────────────────────

        public string? Placeholder => Get<string>("placeholder");
        public int? MaxLength => _data.TryGetValue("maxLength", out var v) && v is int i ? i : null;
        public bool ReadOnly => Get<bool>("readOnly");
        public bool Disabled => Get<bool>("disabled");
        public string? InputType => Get<string>("inputType");

        public Action<DragEvent>? OnDragStart  => Get<Action<DragEvent>>("onDragStart");
        public Action<DragEvent>? OnDrag       => Get<Action<DragEvent>>("onDrag");
        public Action<DragEvent>? OnDragEnd    => Get<Action<DragEvent>>("onDragEnd");
        public Action<DragEvent>? OnDragEnter  => Get<Action<DragEvent>>("onDragEnter");
        public Action<DragEvent>? OnDragOver   => Get<Action<DragEvent>>("onDragOver");
        public Action<DragEvent>? OnDragLeave  => Get<Action<DragEvent>>("onDragLeave");
        public Action<DragEvent>? OnDrop       => Get<Action<DragEvent>>("onDrop");

        // ── Clipboard events ───────────────────────────────────────────────────

        public Action<string>? OnCopy  => Get<Action<string>>("onCopy");
        public Action<string>? OnCut   => Get<Action<string>>("onCut");
        public Action<string>? OnPaste => Get<Action<string>>("onPaste");

        // ── Accessibility (ARIA) ────────────────────────────────────────────────

        /// <summary>The ARIA role attribute (e.g., "button", "menu", "dialog").</summary>
        public string? Role => Get<string>("role");

        /// <summary>Whether the element is focusable (0 = not focusable, -1 = focusable but not in tab order, positive = tab order).</summary>
        public int? TabIndex => _data.TryGetValue("tabIndex", out var v) && v is int i ? i : null;

        /// <summary>Accessible label for screen readers.</summary>
        public string? AriaLabel => Get<string>("aria-label");

        /// <summary>ID of the element that labels this element.</summary>
        public string? AriaLabelledBy => Get<string>("aria-labelledby");

        /// <summary>ID of the element that describes this element.</summary>
        public string? AriaDescribedBy => Get<string>("aria-describedby");

        /// <summary>Whether the element is currently in an expanded state.</summary>
        public bool? AriaExpanded => Get<bool?>("aria-expanded");

        /// <summary>Whether the element is checked (for checkboxes/radio).</summary>
        public bool? AriaChecked => Get<bool?>("aria-checked");

        /// <summary>Whether the element is selected (for listbox/tab).</summary>
        public bool? AriaSelected => Get<bool?>("aria-selected");

        /// <summary>Whether the element is disabled.</summary>
        public bool? AriaDisabled => Get<bool?>("aria-disabled");

        /// <summary>Current value for aria-valuenow (e.g., slider position).</summary>
        public string? AriaValueText => Get<string>("aria-valuetext");

        /// <summary>Live region politeness for dynamic content ("polite" or "assertive").</summary>
        public string? AriaLive => Get<string>("aria-live");

        /// <summary>Whether the element has a popup menu.</summary>
        public string? AriaHasPopup => Get<string>("aria-haspopup");

        /// <summary>Whether the element is hidden from screen readers.</summary>
        public bool? AriaHidden => Get<bool?>("aria-hidden");

        /// <summary>Whether the element's value is invalid.</summary>
        public bool? AriaInvalid => Get<bool?>("aria-invalid");

        /// <summary>Heading level for screen reader navigation (1-6).</summary>
        public int? AriaLevel => Get<int?>("aria-level");

        /// <summary>IDs of elements controlled by this element.</summary>
        public string? AriaControls => Get<string>("aria-controls");

        /// <summary>Orientation of the element (horizontal/vertical).</summary>
        public string? AriaOrientation => Get<string>("aria-orientation");

        // ── Generic access ────────────────────────────────────────────────────

        public T? Get<T>(string key) =>
            _data.TryGetValue(key, out var val) && val is T typed ? typed : default;

        public object? Get(string key) =>
            _data.TryGetValue(key, out var val) ? val : null;

        public bool Has(string key) => _data.ContainsKey(key);

        public IReadOnlyDictionary<string, object?> All => _data;

        // ── Typed view ───────────────────────────────────────────────────────

        /// <summary>
        /// Deserialises this props bag into a strongly-typed record or class.
        /// Each public settable property / constructor parameter on <typeparamref name="T"/> whose
        /// name (case-insensitive) matches a key in the bag is populated from it.
        /// Works best with C# records: <c>record MyProps(string Label, int Count = 0);</c>
        /// </summary>
        public T As<T>() where T : class
        {
            // Try primary constructor first (records)
            var ctors = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in ctors.OrderByDescending(c => c.GetParameters().Length))
            {
                var ctorParams = ctor.GetParameters();
                if (ctorParams.Length == 0) continue;
                var args = new object?[ctorParams.Length];
                bool ok = true;
                for (int i = 0; i < ctorParams.Length; i++)
                {
                    var p = ctorParams[i];
                    // find matching key case-insensitively (camelCase or PascalCase)
                    var key = _data.Keys.FirstOrDefault(k => string.Equals(k, p.Name, StringComparison.OrdinalIgnoreCase));
                    if (key != null && _data[key] is object v)
                    {
                        try { args[i] = Convert.ChangeType(v, Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType); }
                        catch { args[i] = v; }
                    }
                    else if (p.HasDefaultValue)
                    {
                        args[i] = p.DefaultValue;
                    }
                    else
                    {
                        ok = false; break;
                    }
                }
                if (ok) return (T)ctor.Invoke(args);
            }

            // Fall back: default construct then set properties
            var instance = Activator.CreateInstance<T>();
            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
            {
                var key = _data.Keys.FirstOrDefault(k => string.Equals(k, prop.Name, StringComparison.OrdinalIgnoreCase));
                if (key == null || _data[key] is not object v) continue;
                try { prop.SetValue(instance, Convert.ChangeType(v, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType)); }
                catch { prop.SetValue(instance, v); }
            }
            return instance;
        }

        // ── Merge ────────────────────────────────────────────────────────────

        /// <summary>Returns a new Props with all keys from <paramref name="other"/> merged on top.</summary>
        public Props Merge(Props other)
        {
            var merged = new Dictionary<string, object?>(_data);
            foreach (var kv in other._data)
                merged[kv.Key] = kv.Value;
            return new Props(merged);
        }
    }
}
