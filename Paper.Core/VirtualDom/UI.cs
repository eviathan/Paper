using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paper.Core.Styles;

namespace Paper.Core.VirtualDom
{
    /// <summary>
    /// Static factory for creating <see cref="UINode"/> instances.
    /// Provides convenient, concise authoring syntax for building UI trees.
    /// </summary>
    public static class UI
    {
        // ── Intrinsics ────────────────────────────────────────────────────────

        /// <summary>Generic container element (like an HTML div).</summary>
        public static UINode Box(StyleSheet style, params UINode[] children) =>
            new(ElementTypes.Box, new PropsBuilder()
                .Style(style)
                .Children(children)
                .Build());

        /// <summary>Generic container element.</summary>
        public static UINode Box(params UINode[] children) =>
            new(ElementTypes.Box, new PropsBuilder()
                .Children(children)
                .Build());

        /// <summary>Generic container element with full props control.</summary>
        public static UINode Box(Props props, string? key = null) =>
            new(ElementTypes.Box, props, key);

        /// <summary>Text element — renders its <paramref name="content"/> as a text run.</summary>
        public static UINode Text(string content, StyleSheet? style = null, string? key = null) =>
            new(ElementTypes.Text, new PropsBuilder()
                .Text(content)
                .Style(style ?? StyleSheet.Empty)
                .Build(), key);

        /// <summary>Image element — loads from <paramref name="src"/>.</summary>
        public static UINode Image(string src, StyleSheet? style = null, string? key = null) =>
            new(ElementTypes.Image, new PropsBuilder()
                .Src(src)
                .Style(style ?? StyleSheet.Empty)
                .Build(), key);

        /// <summary>
        /// Multiline text input (value, onChange, optional rows).
        /// </summary>
        public static UINode Textarea(
            string value,
            Action<string>? onChange = null,
            int? rows = null,
            StyleSheet? style = null,
            string? key = null,
            string? placeholder = null,
            int? maxLength = null,
            bool readOnly = false,
            bool disabled = false)
        {
            var b = new PropsBuilder().Text(value);
            if (onChange != null) b.OnChange(onChange);
            if (rows.HasValue) b.Set("rows", rows.Value);
            if (style != null) b.Style(style);
            if (placeholder != null) b.Placeholder(placeholder);
            if (maxLength.HasValue) b.MaxLength(maxLength.Value);
            if (readOnly) b.ReadOnly(true);
            if (disabled) b.Disabled(true);
            return new UINode(ElementTypes.Textarea, b.Build(), key);
        }

        /// <summary>
        /// Multiline markdown editor — source-mode with syntax highlighting.
        /// Value is always plain markdown text; rows sets the minimum height.
        /// </summary>
        public static UINode MarkdownEditor(
            string value,
            Action<string>? onChange = null,
            int? rows = null,
            StyleSheet? style = null,
            string? key = null,
            string? placeholder = null,
            bool readOnly = false,
            bool disabled = false)
        {
            var b = new PropsBuilder().Text(value);
            if (onChange != null) b.OnChange(onChange);
            if (rows.HasValue) b.Set("rows", rows.Value);
            if (style != null) b.Style(style);
            if (placeholder != null) b.Placeholder(placeholder);
            if (readOnly) b.ReadOnly(true);
            if (disabled) b.Disabled(true);
            return new UINode(ElementTypes.MarkdownEditor, b.Build(), key);
        }

        /// <summary>
        /// Renders a markdown string as a styled Paper UINode tree (preview mode).
        /// </summary>
        public static UINode MarkdownPreview(string markdown, StyleSheet? style = null) =>
            Markdown.MarkdownPreviewRenderer.Render(markdown, style);

        /// <summary>
        /// Single-line text input.
        /// </summary>
        public static UINode Input(
            string value,
            Action<string>? onChange = null,
            StyleSheet? style = null,
            string? key = null,
            string? placeholder = null,
            int? maxLength = null,
            bool readOnly = false,
            bool disabled = false,
            string? inputType = null)
        {
            var b = new PropsBuilder().Text(value);
            if (style != null) b.Style(style);
            if (onChange != null) b.OnChange(onChange);
            if (placeholder != null) b.Placeholder(placeholder);
            if (maxLength.HasValue) b.MaxLength(maxLength.Value);
            if (readOnly) b.ReadOnly(true);
            if (disabled) b.Disabled(true);
            if (inputType != null) b.InputType(inputType);
            return new UINode(ElementTypes.Input, b.Build(), key);
        }

        /// <summary>Checkbox element — checked state and optional label.</summary>
        public static UINode Checkbox(
            bool @checked,
            Action<bool>? onCheckedChange = null,
            string? label = null,
            StyleSheet? style = null,
            string? key = null)
        {
            var b = new PropsBuilder().Set("checked", @checked);
            if (onCheckedChange != null) b.Set("onCheckedChange", onCheckedChange);
            if (label != null) b.Text(label);
            if (style != null) b.Style(style);
            return new UINode(ElementTypes.Checkbox, b.Build(), key);
        }

        /// <summary>Clickable button element.</summary>
        public static UINode Button(
            string label,
            Action? onClick = null,
            StyleSheet? style = null,
            string? key = null)
        {
            var b = new PropsBuilder().Text(label);
            if (style != null) b.Style(style);
            if (onClick != null) b.OnClick(onClick);
            return new UINode(ElementTypes.Button, b.Build(), key);
        }

        /// <summary>Radio group — options (value, label), selectedValue, onSelect. Renders as column of radio options.</summary>
        public static UINode RadioGroup(
            IReadOnlyList<(string Value, string Label)> options,
            string? selectedValue,
            Action<string>? onSelect,
            StyleSheet? style = null,
            string? key = null)
        {
            var b = new PropsBuilder().Set("options", options).Set("selectedValue", selectedValue ?? "");
            if (onSelect != null) b.Set("onSelect", onSelect);
            if (style != null) b.Style(style);
            return new UINode(ElementTypes.RadioGroup, b.Build(), key);
        }

        /// <summary>Radio group — options (value, label), selectedValue, onSelect. Renders as column of radio options.</summary>
        public static UINode Panel(
            IReadOnlyList<(string Value, string Label)> options,
            string? selectedValue,
            Action<string>? onSelect,
            StyleSheet? style = null,
            string? key = null)
        {
            // TODO: Implement this
            var b = new PropsBuilder().Set("options", options).Set("selectedValue", selectedValue ?? "");
            if (onSelect != null) b.Set("onSelect", onSelect);
            if (style != null) b.Style(style);
            return new UINode(ElementTypes.Panel, b.Build(), key);
        }

        /// <summary>Table container (block).</summary>
        public static UINode Table(StyleSheet? style = null, string? key = null, params UINode[] children) =>
            new(ElementTypes.Table, new PropsBuilder()
                .Style(style ?? StyleSheet.Empty)
                .Children(children)
                .Build(), key);

        /// <summary>Table row (flex row).</summary>
        public static UINode TableRow(StyleSheet? style = null, string? key = null, params UINode[] children) =>
            new(ElementTypes.TableRow, new PropsBuilder()
                .Style(style ?? StyleSheet.Empty)
                .Children(children)
                .Build(), key);

        /// <summary>Table cell (block).</summary>
        public static UINode TableCell(StyleSheet? style = null, string? key = null, params UINode[] children) =>
            new(ElementTypes.TableCell, new PropsBuilder()
                .Style(style ?? StyleSheet.Empty)
                .Children(children)
                .Build(), key);

        /// <summary>Scrollable container.</summary>
        public static UINode Scroll(StyleSheet style, params UINode[] children) =>
            new(ElementTypes.Scroll, new PropsBuilder()
                .Style(style)
                .Children(children)
                .Build());

        /// <summary>Fragment — renders children inline with no wrapper box.</summary>
        public static UINode Fragment(params UINode[] children) =>
            new(ElementTypes.Fragment, new PropsBuilder()
                .Children(children)
                .Build());

        /// <summary>
        /// Renders <paramref name="children"/> into a separate overlay layer managed by the host renderer.
        /// Portal children are collected in <see cref="Paper.Core.Reconciler.Reconciler.PortalRoots"/> and
        /// rendered above the main scene.
        /// </summary>
        public static UINode Portal(params UINode[] children) =>
            new(ElementTypes.Portal, new PropsBuilder().Children(children).Build());

        /// <summary>
        /// Renders <paramref name="children"/> into a separate overlay layer.
        /// </summary>
        public static UINode Portal(UINode[] children, string? key = null) =>
            new(ElementTypes.Portal, new PropsBuilder().Children(children).Build(), key);

        /// <summary>
        /// Renders an OpenGL texture as a viewport panel. The texture handle is typically
        /// obtained from the embedded engine's game-view framebuffer.
        /// </summary>
        public static UINode Viewport(uint textureHandle, StyleSheet? style = null, string? key = null, Action<int, int>? onSizeChanged = null) =>
            new(ElementTypes.Viewport, new PropsBuilder()
                .Set("textureHandle", textureHandle)
                .Set("onViewportSize", onSizeChanged)
                .Style(style ?? StyleSheet.Empty)
                .Build(), key);

        // ── Primitive components ──────────────────────────────────────────────

        /// <summary>
        /// Wraps <paramref name="children"/> in a tooltip that appears on hover.
        /// </summary>
        public static UINode Tooltip(string text, StyleSheet? style = null, string? key = null, params UINode[] children) =>
            new(Components.Primitives.TooltipComponent,
                new PropsBuilder().Set("text", text).Children(children).Style(style ?? StyleSheet.Empty).Build(),
                key);

        /// <summary>
        /// Full-screen modal overlay. Renders <paramref name="children"/> centred on screen when <paramref name="isOpen"/> is true.
        /// </summary>
        public static UINode Modal(bool isOpen, Action? onClose = null, StyleSheet? style = null, string? key = null, params UINode[] children) =>
            new(Components.Primitives.ModalComponent,
                new PropsBuilder()
                    .Set("isOpen", isOpen)
                    .Set("onClose", onClose)
                    .Children(children)
                    .Style(style ?? StyleSheet.Empty)
                    .Build(),
                key);

        /// <summary>
        /// Positioned context menu floating at (<paramref name="x"/>, <paramref name="y"/>) in window pixels.
        /// <paramref name="items"/> are (Label, OnSelect) pairs. Calls <paramref name="onClose"/> after selection or backdrop click.
        /// </summary>
        public static UINode ContextMenu(
            bool isOpen,
            float x, float y,
            IReadOnlyList<(string Label, Action? OnSelect)> items,
            Action? onClose = null,
            string? key = null) =>
            new(Components.Primitives.ContextMenuComponent,
                new PropsBuilder()
                    .Set("isOpen", isOpen)
                    .Set("x", (float?)x)
                    .Set("y", (float?)y)
                    .Set("items", items)
                    .Set("onClose", onClose)
                    .Build(),
                key);

        /// <summary>
        /// Horizontal range slider. Scroll wheel adjusts by one step.
        /// </summary>
        public static UINode Slider(
            float value,
            float min = 0f,
            float max = 100f,
            float step = 1f,
            Action<float>? onChange = null,
            StyleSheet? style = null,
            string? key = null)
        {
            var b = new PropsBuilder()
                .Set("value",    (float?)value)
                .Set("min",      (float?)min)
                .Set("max",      (float?)max)
                .Set("step",     (float?)step);
            if (onChange != null) b.Set("onChange", onChange);
            if (style != null) b.Style(style);
            return new UINode(Components.Primitives.SliderComponent, b.Build(), key);
        }

        /// <summary>
        /// Numeric text input with − / + buttons.
        /// </summary>
        public static UINode NumberInput(
            float value,
            float? min = null,
            float? max = null,
            float step = 1f,
            Action<float>? onChange = null,
            StyleSheet? style = null,
            string? key = null)
        {
            var b = new PropsBuilder()
                .Set("value", (float?)value)
                .Set("step",  (float?)step);
            if (min.HasValue) b.Set("min", (float?)min.Value);
            if (max.HasValue) b.Set("max", (float?)max.Value);
            if (onChange != null) b.Set("onChange", onChange);
            if (style != null) b.Style(style);
            return new UINode(Components.Primitives.NumberInputComponent, b.Build(), key);
        }

        /// <summary>
        /// Tab strip + panel switching. <paramref name="panels"/> order must match <paramref name="tabs"/> order.
        /// </summary>
        public static UINode Tabs(
            IReadOnlyList<(string Id, string Label)> tabs,
            string activeTab,
            Action<string>? onTabChange = null,
            StyleSheet? style = null,
            string? key = null,
            params UINode[] panels)
        {
            var b = new PropsBuilder()
                .Set("tabs", tabs)
                .Set("activeTab", (object?)activeTab)
                .Children(panels);
            if (onTabChange != null) b.Set("onTabChange", onTabChange);
            if (style != null) b.Style(style);
            return new UINode(Components.Primitives.TabsComponent, b.Build(), key);
        }

        /// <summary>
        /// Interactive floating panel anchored to its trigger child.
        /// The first child is the trigger; remaining children are the popover content.
        /// </summary>
        public static UINode Popover(
            bool isOpen,
            Action? onClose = null,
            string placement = "bottom",
            StyleSheet? style = null,
            string? key = null,
            params UINode[] children)
        {
            var b = new PropsBuilder()
                .Set("isOpen", isOpen)
                .Set("placement", (object?)placement)
                .Children(children);
            if (onClose != null) b.Set("onClose", onClose);
            if (style != null) b.Style(style);
            return new UINode(Components.Primitives.PopoverComponent, b.Build(), key);
        }

        /// <summary>
        /// Renders all active toasts in the top-right corner.
        /// </summary>
        public static UINode ToastContainer(
            IReadOnlyList<Components.Primitives.ToastEntry> toasts,
            Action<string>? onDismiss = null,
            string? key = null)
        {
            var b = new PropsBuilder().Set("toasts", toasts);
            if (onDismiss != null) b.Set("onDismiss", onDismiss);
            return new UINode(Components.Primitives.ToastContainerComponent, b.Build(), key);
        }

        // ── Function components ───────────────────────────────────────────────

        /// <summary>
        /// Render a function component.
        /// </summary>
        /// <param name="component">A function that takes <see cref="Props"/> and returns a <see cref="UINode"/>.</param>
        public static UINode Component(Func<Props, UINode> component, Props? props = null, string? key = null) =>
            new(component, props ?? Props.Empty, key);

        /// <summary>
        /// Render a functional component with strongly-typed props.
        /// The typed props object is serialised to a Props bag so the component can deserialise
        /// it with the injected <c>var myProps = __props.As&lt;TProps&gt;();</c> binding.
        /// </summary>
        public static UINode Component<TProps>(Func<Props, UINode> component, TProps typedProps, string? key = null) where TProps : class =>
            new(component, Props.From(typedProps), key);

        /// <summary>
        /// Render a class component.
        /// </summary>
        public static UINode Component<TComponent>(Props? props = null, string? key = null)
            where TComponent : Components.Component =>
            new(typeof(TComponent), props ?? Props.Empty, key);

        // ── List helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Map a collection to UINodes, automatically setting the key from the provided selector.
        /// </summary>
        public static UINode[] Map<T>(IEnumerable<T> items, Func<T, string> keySelector, Func<T, UINode> render)
        {
            return items.Select(item =>
            {
                var node = render(item);
                return new UINode(node.Type, node.Props, keySelector(item));
            }).ToArray();
        }

        /// <summary>
        /// Virtualized scrollable list — only the visible rows (plus <paramref name="overscan"/> extra rows) are
        /// reconciled and drawn each frame. Handles scroll internally via wheel events.
        /// </summary>
        /// <typeparam name="T">Item type.</typeparam>
        /// <param name="items">Full item collection.</param>
        /// <param name="itemHeight">Fixed row height in pixels. Every row must be the same height.</param>
        /// <param name="containerH">Visible area height in pixels.</param>
        /// <param name="renderItem">Render function — receives the item and its original index.</param>
        /// <param name="style">Optional extra style applied to the outer container box.</param>
        /// <param name="overscan">Extra rows rendered above/below the visible window to avoid pop-in.</param>
        /// <param name="key">Reconciler key for this node.</param>
        public static UINode List<T>(
            IReadOnlyList<T> items,
            float            itemHeight,
            float            containerH,
            Func<T, int, UINode> renderItem,
            StyleSheet?      style     = null,
            int              overscan  = 3,
            string?          key       = null)
        {
            // Box the typed list and render delegate so they fit the Props dictionary (object keys).
            var boxedItems  = (IReadOnlyList<object>)(items.Count == 0
                ? Array.Empty<object>()
                : items.Select(i => (object)i!).ToList());
            Func<object, int, UINode> boxedRender = (obj, idx) => renderItem((T)obj, idx);

            var props = new PropsBuilder()
                .Set("items",       boxedItems)
                .Set("itemHeight",  (float?)itemHeight)
                .Set("containerH",  (float?)containerH)
                .Set("renderItem",  boxedRender)
                .Set("overscan",    (int?)overscan)
                .Style(style ?? StyleSheet.Empty)
                .Build();

            return new UINode(Components.Primitives.ListComponent, props, key);
        }

        /// <summary>
        /// Virtualized scrollable list — each item rendered as <c>UI.Text(item.ToString()!)</c>.
        /// </summary>
        public static UINode List<T>(
            IReadOnlyList<T> items,
            float            itemHeight,
            float            containerH,
            StyleSheet?      style    = null,
            int              overscan = 3,
            string?          key      = null) =>
            List(items, itemHeight, containerH,
                (item, _) => UI.Text(item!.ToString()!),
                style, overscan, key);

        /// <summary>
        /// Virtualized scrollable list (convenience overload without index parameter).
        /// </summary>
        public static UINode List<T>(
            IReadOnlyList<T> items,
            float            itemHeight,
            float            containerH,
            Func<T, UINode>  renderItem,
            StyleSheet?      style    = null,
            int              overscan = 3,
            string?          key      = null) =>
            List(items, itemHeight, containerH,
                (item, _) => renderItem(item),
                style, overscan, key);

        /// <summary>
        /// Conditionally include a node — returns an empty fragment when <paramref name="condition"/> is false.
        /// </summary>
        public static UINode When(bool condition, UINode node) =>
            condition ? node : Fragment();

        /// <summary>
        /// Ternary: returns <paramref name="ifTrue"/> when condition is met, otherwise <paramref name="ifFalse"/>.
        /// </summary>
        public static UINode Ternary(bool condition, UINode ifTrue, UINode ifFalse) =>
            condition ? ifTrue : ifFalse;

        /// <summary>
        /// Flattens a mix of <see cref="UINode"/> and <see cref="IEnumerable{UINode}"/> items into a single
        /// UINode array. Used by codegen when children include dynamic array-producing expressions alongside
        /// static nodes — avoids the C# params limitation of mixing <c>UINode</c> and <c>UINode[]</c> arguments.
        /// </summary>
        public static UINode[] Nodes(params object?[] items)
        {
            var list = new List<UINode>();
            foreach (var item in items)
            {
                switch (item)
                {
                    case UINode n: list.Add(n); break;
                    case IEnumerable<UINode> e: list.AddRange(e); break;
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// Renders a single icon glyph from an icon font family (e.g. "material-icons").
        /// <paramref name="codepoint"/> is the Unicode codepoint as a string (e.g. "\uE88A" for
        /// Material Icons "home"). Use the generated <c>MaterialIcons</c> or <c>FontAwesomeIcons</c>
        /// constants for named access. Drop the .ttf into Assets/fonts with "icon" in its filename
        /// and it will be auto-loaded with full glyph discovery.
        /// </summary>
        public static UINode Icon(
            string codepoint,
            string fontFamily,
            float size = 24f,
            StyleSheet? style = null,
            string? key = null)
        {
            var iconStyle = new StyleSheet
            {
                FontFamily = fontFamily,
                FontSize   = Paper.Core.Styles.Length.Px(size),
                Width      = Paper.Core.Styles.Length.Px(size),
                Height     = Paper.Core.Styles.Length.Px(size),
                Display    = Paper.Core.Styles.Display.InlineFlex,
            }.Merge(style ?? StyleSheet.Empty);

            return new UINode(ElementTypes.Text,
                new PropsBuilder().Text(codepoint).Style(iconStyle).Build(),
                key);
        }
    }
}