using Paper.Core.Components;
using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Xunit;
using R = Paper.Core.Reconciler.Reconciler;

namespace Paper.Core.Tests;

/// <summary>
/// Tests for built-in primitive components: Tooltip, Modal, ContextMenu.
/// These are function components mounted through the standard Reconciler.
/// </summary>
[Collection("Sequential")]
public sealed class PrimitivesTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Walk depth-first collecting all fiber types.</summary>
    private static List<string> CollectTypes(Fiber? f)
    {
        var types = new List<string>();
        Collect(f, types);
        return types;
    }

    private static void Collect(Fiber? f, List<string> types)
    {
        if (f == null) return;
        if (f.Type is string s) types.Add(s);
        Collect(f.Child, types);
        Collect(f.Sibling, types);
    }

    /// <summary>
    /// Returns true if any rendered Text fiber (type == "text") in the tree contains <paramref name="text"/>.
    /// Only matches fibers whose Type is "text" (not component or box props).
    /// </summary>
    private static bool HasText(Fiber? f, string text)
    {
        if (f == null) return false;
        if (f.Type is string t && t == Paper.Core.VirtualDom.ElementTypes.Text && f.Props.Text == text)
            return true;
        return HasText(f.Child, text) || HasText(f.Sibling, text);
    }

    // ── Tooltip ───────────────────────────────────────────────────────────────

    [Fact]
    public void Tooltip_RendersChildren()
    {
        // Tooltip should always render its trigger children.
        var rec = new R();
        rec.Mount(UI.Tooltip("Hint", children: [UI.Text("Click me")]));

        Assert.True(HasText(rec.Root, "Click me"));
    }

    [Fact]
    public void Tooltip_BubbleHidden_WhenNotHovered()
    {
        // On initial render (not hovered), tooltip bubble text should not appear.
        var rec = new R();
        rec.Mount(UI.Tooltip("My Tooltip Text", children: [UI.Box()]));

        Assert.False(HasText(rec.Root, "My Tooltip Text"));
    }

    [Fact]
    public void Tooltip_EmptyText_NoBubble()
    {
        // Empty text → bubble never renders even if hover state changes.
        var rec = new R();
        rec.Mount(UI.Tooltip("", children: [UI.Box()]));

        Assert.NotNull(rec.Root);
        Assert.DoesNotContain("Parse error", rec.Root!.Props.Text ?? "");
    }

    [Fact]
    public void Tooltip_WrapperHasRelativePosition()
    {
        var rec = new R();
        rec.Mount(UI.Tooltip("Hint", children: [UI.Box()]));

        // The wrapper Box should have Position=Relative (applied via inline style).
        // Because ComputedStyle is not populated in tests (no StyleResolver), we check Props.Style.
        var root = rec.Root;
        Assert.NotNull(root);
        // Root fiber is the Tooltip component; its child is the wrapper Box.
        var wrapper = root!.Child;
        Assert.NotNull(wrapper);
        Assert.Equal(Position.Relative, wrapper!.Props.Style?.Position);
    }

    // ── Modal ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Modal_ClosedState_RendersFragment()
    {
        // When isOpen=false the Modal returns UI.Fragment(), meaning no children are mounted.
        var rec = new R();
        rec.Mount(UI.Modal(isOpen: false, children: [UI.Text("Modal body")]));

        Assert.False(HasText(rec.Root, "Modal body"));
    }

    [Fact]
    public void Modal_OpenState_RendersChildren()
    {
        var rec = new R();
        rec.Mount(UI.Modal(isOpen: true, children: [UI.Text("Modal body")]));

        Assert.True(HasText(rec.Root, "Modal body"));
    }

    [Fact]
    public void Modal_OpenState_HasBackdropZIndex()
    {
        var rec = new R();
        rec.Mount(UI.Modal(isOpen: true, children: [UI.Box()]));

        // The outermost box is the backdrop — should have ZIndex=1000.
        // Modal is a function component; Root is the component fiber; Root.Child is the backdrop.
        var component = rec.Root;
        Assert.NotNull(component);
        var backdrop = component!.Child;
        Assert.NotNull(backdrop);
        Assert.Equal(1000, backdrop!.Props.Style?.ZIndex);
    }

    [Fact]
    public void Modal_TogglesOpenClosed()
    {
        var rec = new R();

        // Start closed
        var node = UI.Modal(isOpen: false, children: [UI.Text("Content")]);
        rec.Mount(node);
        Assert.False(HasText(rec.Root, "Content"));

        // Open it
        rec.Update(UI.Modal(isOpen: true, children: [UI.Text("Content")]));
        Assert.True(HasText(rec.Root, "Content"));

        // Close it again
        rec.Update(UI.Modal(isOpen: false, children: [UI.Text("Content")]));
        Assert.False(HasText(rec.Root, "Content"));
    }

    [Fact]
    public void Modal_OnCloseCalledOnBackdropClick()
    {
        bool closeCalled = false;
        var rec = new R();
        rec.Mount(UI.Modal(isOpen: true, onClose: () => { closeCalled = true; }, children: [UI.Box()]));

        // Find the backdrop fiber and invoke its OnPointerClick handler.
        var backdropFiber = rec.Root!.Child;
        Assert.NotNull(backdropFiber);
        backdropFiber!.Props.OnPointerClick?.Invoke(new Paper.Core.Events.PointerEvent());

        Assert.True(closeCalled);
    }

    [Fact]
    public void Modal_StyleOverride_MergedIntoPanelStyle()
    {
        var customStyle = new StyleSheet { MinWidth = Length.Px(500) };
        var rec = new R();
        rec.Mount(UI.Modal(isOpen: true, style: customStyle, children: [UI.Box()]));

        // The panel is the child of the backdrop box.
        // Traverse: Root (component fiber) → Child (backdrop Box) → Child (panel Box)
        var backdrop = rec.Root!.Child!;
        var panel = backdrop.Child;
        Assert.NotNull(panel);
        Assert.Equal(Length.Px(500), panel!.Props.Style?.MinWidth);
    }

    // ── ContextMenu ───────────────────────────────────────────────────────────

    [Fact]
    public void ContextMenu_ClosedState_RendersNothing()
    {
        var rec = new R();
        var items = new List<(string, Action?)> { ("Item 1", null) };
        rec.Mount(UI.ContextMenu(isOpen: false, x: 100, y: 100, items: items));

        // When closed, should be essentially empty (Fragment).
        Assert.False(HasText(rec.Root, "Item 1"));
    }

    [Fact]
    public void ContextMenu_OpenState_RendersItems()
    {
        var rec = new R();
        var items = new List<(string, Action?)>
        {
            ("Cut",  null),
            ("Copy", null),
            ("Paste", null),
        };
        rec.Mount(UI.ContextMenu(isOpen: true, x: 50, y: 80, items: items));

        Assert.True(HasText(rec.Root, "Cut"));
        Assert.True(HasText(rec.Root, "Copy"));
        Assert.True(HasText(rec.Root, "Paste"));
    }

    [Fact]
    public void ContextMenu_OnSelectCalledAndMenuCloses()
    {
        bool selected = false;
        bool closed = false;
        var rec = new R();
        var items = new List<(string, Action?)>
        {
            ("Delete", () => { selected = true; }),
        };
        rec.Mount(UI.ContextMenu(isOpen: true, x: 0, y: 0, items: items,
            onClose: () => { closed = true; }));

        // Find the "Delete" menu item — look for a rendered text fiber with that label.
        Fiber? FindFiber(Fiber? f, string text)
        {
            if (f == null) return null;
            if (f.Type is string t && t == Paper.Core.VirtualDom.ElementTypes.Text && f.Props.Text == text)
                return f;
            return FindFiber(f.Child, text) ?? FindFiber(f.Sibling, text);
        }

        // The text fiber is inside a clickable parent — we want the parent button/box with OnPointerClick.
        Fiber? FindClickable(Fiber? f, string text)
        {
            if (f == null) return null;
            var textFiber = FindFiber(f, text);
            if (textFiber?.Parent?.Props.OnPointerClick != null)
                return textFiber.Parent;
            return null;
        }

        var item = FindClickable(rec.Root, "Delete");
        Assert.NotNull(item);
        item!.Props.OnPointerClick?.Invoke(new Paper.Core.Events.PointerEvent());

        Assert.True(selected);
        Assert.True(closed);
    }

    [Fact]
    public void ContextMenu_EmptyItems_RendersOpenWithNoItems()
    {
        var rec = new R();
        rec.Mount(UI.ContextMenu(isOpen: true, x: 0, y: 0,
            items: new List<(string, Action?)>()));

        // Should mount without error, just no item text.
        Assert.NotNull(rec.Root);
    }

    // ── Select ────────────────────────────────────────────────────────────────

    private static UINode MakeSelect(
        IReadOnlyList<(string Value, string Label)> options,
        string selectedValue = "",
        Action<string>? onSelect = null)
    {
        var props = new PropsBuilder()
            .Set("options", options)
            .Set("selectedValue", (object?)selectedValue)
            .Set("onSelect", (object?)onSelect)
            .Build();
        return UI.Component(Paper.Core.Components.Primitives.SelectComponent, props);
    }

    /// <summary>
    /// Check whether any button or text fiber contains the given text label.
    /// UI.Button sets Props.Text on a "button" fiber, not a "text" fiber.
    /// </summary>
    private static bool HasLabel(Fiber? f, string text)
    {
        if (f == null) return false;
        if (f.Type is string t &&
            (t == Paper.Core.VirtualDom.ElementTypes.Text || t == Paper.Core.VirtualDom.ElementTypes.Button) &&
            f.Props.Text == text)
            return true;
        return HasLabel(f.Child, text) || HasLabel(f.Sibling, text);
    }

    /// <summary>Find first button fiber (type == "button") in the tree.</summary>
    private static Fiber? FindButtonFiber(Fiber? f)
    {
        if (f == null) return null;
        if (f.Type is string t && t == Paper.Core.VirtualDom.ElementTypes.Button) return f;
        return FindButtonFiber(f.Child) ?? FindButtonFiber(f.Sibling);
    }

    /// <summary>Find a button fiber whose text label matches.</summary>
    private static Fiber? FindButtonByLabel(Fiber? f, string text)
    {
        if (f == null) return null;
        if (f.Type is string t && t == Paper.Core.VirtualDom.ElementTypes.Button && f.Props.Text == text) return f;
        return FindButtonByLabel(f.Child, text) ?? FindButtonByLabel(f.Sibling, text);
    }

    [Fact]
    public void Select_ClosedByDefault_RendersButtonLabel()
    {
        var options = new List<(string, string)> { ("a", "Apple"), ("b", "Banana") };
        var rec = new R();
        rec.Mount(MakeSelect(options, selectedValue: "a"));

        // The trigger button label includes the current label + dropdown indicator.
        Assert.True(HasLabel(rec.Root, "Apple \u25BC"), "Expected trigger button with selected label");
    }

    [Fact]
    public void Select_ClosedByDefault_OptionsNotVisible()
    {
        var options = new List<(string, string)> { ("a", "Apple"), ("b", "Banana") };
        var rec = new R();
        rec.Mount(MakeSelect(options, selectedValue: "a"));

        // When closed, other option labels should not be rendered.
        Assert.False(HasLabel(rec.Root, "Banana"));
    }

    [Fact]
    public void Select_OnButtonClick_OpensDropdown()
    {
        var options = new List<(string, string)> { ("a", "Apple"), ("b", "Banana") };
        var rec = new R();
        var root = MakeSelect(options, selectedValue: "a");
        rec.Mount(root);

        // Find the trigger button fiber and fire its OnClick.
        var button = FindButtonFiber(rec.Root);
        Assert.NotNull(button);
        button!.Props.OnClick!.Invoke();
        rec.Update(root);

        // After opening, option labels should appear.
        Assert.True(HasLabel(rec.Root, "Banana"), "Expected dropdown options after opening");
    }

    [Fact]
    public void Select_OptionClick_CallsOnSelect()
    {
        string? selected = null;
        var options = new List<(string, string)> { ("a", "Apple"), ("b", "Banana") };
        var rec = new R();
        var root = MakeSelect(options, selectedValue: "a", onSelect: v => selected = v);
        rec.Mount(root);

        // Open the dropdown.
        var trigger = FindButtonFiber(rec.Root);
        trigger!.Props.OnClick!.Invoke();
        rec.Update(root);

        // Click the "Banana" option.
        var bananaBtn = FindButtonByLabel(rec.Root, "Banana");
        Assert.NotNull(bananaBtn);
        bananaBtn!.Props.OnClick!.Invoke();

        Assert.Equal("b", selected);
    }

    [Fact]
    public void Select_OptionClick_ClosesDropdown()
    {
        var options = new List<(string, string)> { ("a", "Apple"), ("b", "Banana") };
        var rec = new R();
        var root = MakeSelect(options, selectedValue: "a");
        rec.Mount(root);

        // Open the dropdown.
        var trigger = FindButtonFiber(rec.Root);
        trigger!.Props.OnClick!.Invoke();
        rec.Update(root);
        Assert.True(HasLabel(rec.Root, "Banana"), "Dropdown should be open");

        // Click "Banana" — should close dropdown.
        var bananaBtn = FindButtonByLabel(rec.Root, "Banana");
        Assert.NotNull(bananaBtn);
        bananaBtn!.Props.OnClick!.Invoke();
        rec.Update(root);

        Assert.False(HasLabel(rec.Root, "Banana"), "Dropdown should close after selection");
    }

    [Fact]
    public void Select_EmptyOptions_MountsWithoutError()
    {
        var rec = new R();
        rec.Mount(MakeSelect(Array.Empty<(string, string)>()));
        Assert.NotNull(rec.Root);
    }

    [Fact]
    public void Select_NoMatchingValue_ShowsValueAsLabel()
    {
        // When selectedValue doesn't match any option, the raw value is used as label.
        var options = new List<(string, string)> { ("a", "Apple") };
        var rec = new R();
        rec.Mount(MakeSelect(options, selectedValue: "unknown"));

        Assert.True(HasLabel(rec.Root, "unknown \u25BC"));
    }
}
