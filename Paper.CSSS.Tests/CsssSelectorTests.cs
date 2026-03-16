using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Paper.Core.Reconciler;
using Paper.CSSS;
using Xunit;
using R = Paper.Core.Reconciler.Reconciler;

namespace Paper.CSSS.Tests;

/// <summary>
/// Tests for CSSSSelector matching against real fiber trees.
/// Uses CSSSSheet.FromDictionary (internal) + CSSSSheet.Match to verify
/// selector matching across element, class, id, descendant, child, and pseudo selectors.
/// </summary>
public sealed class CsssSelectorTests
{
    private static readonly InteractionState None   = new(false, false, false);
    private static readonly InteractionState Hover  = new(true,  false, false);
    private static readonly InteractionState Active = new(false, true,  false);
    private static readonly InteractionState Focus  = new(false, false, true);

    private static readonly StyleSheet Marker = new StyleSheet { ZIndex = 99 };

    /// <summary>Build a single-rule sheet and match it against the given fiber tree, returning the root fiber.</summary>
    private static (Fiber fiber, CSSSSheet sheet) MakeSheet(string selector, UINode tree)
    {
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("test.csss",
            new Dictionary<string, StyleSheet> { [selector] = Marker });
        return (rec.Root!, sheet);
    }

    /// <summary>Walk depth-first and find a fiber with the given type string.</summary>
    private static Fiber? FindByType(Fiber? f, string type)
    {
        if (f == null) return null;
        if (f.Type is string t && t == type) return f;
        return FindByType(f.Child, type) ?? FindByType(f.Sibling, type);
    }

    /// <summary>Walk depth-first and find a fiber with the given className.</summary>
    private static Fiber? FindByClass(Fiber? f, string cls)
    {
        if (f == null) return null;
        if (f.Props.ClassName?.Split(' ').Contains(cls) == true) return f;
        return FindByClass(f.Child, cls) ?? FindByClass(f.Sibling, cls);
    }

    /// <summary>Walk depth-first and find a fiber with the given id.</summary>
    private static Fiber? FindById(Fiber? f, string id)
    {
        if (f == null) return null;
        if (f.Props.Id == id) return f;
        return FindById(f.Child, id) ?? FindById(f.Sibling, id);
    }

    // ── Element selector ──────────────────────────────────────────────────────

    [Fact]
    public void Element_MatchesByType()
    {
        var tree = UI.Box();
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet> { ["box"] = Marker });
        var result = sheet.Match(rec.Root!, None);
        Assert.Equal(99, result.ZIndex);
    }

    [Fact]
    public void Element_NoMatchOnDifferentType()
    {
        var rec = new R();
        rec.Mount(UI.Box());
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet> { ["text"] = Marker });
        var result = sheet.Match(rec.Root!, None);
        Assert.NotEqual(99, result.ZIndex);
    }

    // ── Class selector ────────────────────────────────────────────────────────

    [Fact]
    public void Class_MatchesByClassName()
    {
        var rec = new R();
        rec.Mount(UI.Box(new PropsBuilder().ClassName("card").Build()));
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet> { [".card"] = Marker });
        var result = sheet.Match(rec.Root!, None);
        Assert.Equal(99, result.ZIndex);
    }

    [Fact]
    public void Class_NoMatchOnMissingClass()
    {
        var rec = new R();
        rec.Mount(UI.Box(new PropsBuilder().ClassName("other").Build()));
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet> { [".card"] = Marker });
        var result = sheet.Match(rec.Root!, None);
        Assert.NotEqual(99, result.ZIndex);
    }

    [Fact]
    public void Class_MatchesOneOfMultipleClasses()
    {
        var rec = new R();
        rec.Mount(UI.Box(new PropsBuilder().ClassName("card active featured").Build()));
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet> { [".active"] = Marker });
        var result = sheet.Match(rec.Root!, None);
        Assert.Equal(99, result.ZIndex);
    }

    // ── Id selector ───────────────────────────────────────────────────────────

    [Fact]
    public void Id_MatchesByIdProp()
    {
        var rec = new R();
        rec.Mount(UI.Box(new PropsBuilder().Id("header").Build()));
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet> { ["#header"] = Marker });
        var result = sheet.Match(rec.Root!, None);
        Assert.Equal(99, result.ZIndex);
    }

    [Fact]
    public void Id_NoMatchOnDifferentId()
    {
        var rec = new R();
        rec.Mount(UI.Box(new PropsBuilder().Id("footer").Build()));
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet> { ["#header"] = Marker });
        var result = sheet.Match(rec.Root!, None);
        Assert.NotEqual(99, result.ZIndex);
    }

    // ── Descendant selector ───────────────────────────────────────────────────

    [Fact]
    public void Descendant_MatchesDeepChild()
    {
        // .container Text — Text is a descendant of .container
        var tree = UI.Box(
            UI.Box(
                new PropsBuilder().ClassName("container").Children(UI.Box(UI.Text("hello"))).Build()));
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { [".container text"] = Marker });

        var textFiber = FindByType(rec.Root!, "text");
        Assert.NotNull(textFiber);
        var result = sheet.Match(textFiber!, None);
        Assert.Equal(99, result.ZIndex);
    }

    [Fact]
    public void Descendant_NoMatchForNonDescendant()
    {
        // Text outside .container should not match ".container Text"
        var tree = UI.Box(
            UI.Box(new PropsBuilder().ClassName("container").Build()),
            UI.Text("outside")); // sibling, not inside .container
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { [".container text"] = Marker });

        // Find the sibling Text fiber (not inside .container)
        var textFiber = rec.Root!.Child!.Sibling;
        Assert.NotNull(textFiber);
        var result = sheet.Match(textFiber!, None);
        Assert.NotEqual(99, result.ZIndex);
    }

    // ── Child combinator ──────────────────────────────────────────────────────

    [Fact]
    public void ChildCombinator_MatchesDirectChild()
    {
        var tree = UI.Box(new PropsBuilder()
            .ClassName("parent")
            .Children(UI.Box(new PropsBuilder().ClassName("child").Build()))
            .Build());
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { [".parent > .child"] = Marker });

        var childFiber = FindByClass(rec.Root!, "child");
        Assert.NotNull(childFiber);
        var result = sheet.Match(childFiber!, None);
        Assert.Equal(99, result.ZIndex);
    }

    [Fact]
    public void ChildCombinator_NoMatchForGrandchild()
    {
        var tree = UI.Box(new PropsBuilder()
            .ClassName("parent")
            .Children(UI.Box(UI.Box(new PropsBuilder().ClassName("grandchild").Build())))
            .Build());
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { [".parent > .grandchild"] = Marker });

        var grandchild = FindByClass(rec.Root!, "grandchild");
        Assert.NotNull(grandchild);
        var result = sheet.Match(grandchild!, None);
        Assert.NotEqual(99, result.ZIndex); // not a direct child
    }

    // ── Pseudo-classes ────────────────────────────────────────────────────────

    [Fact]
    public void Pseudo_Hover_MatchesOnHoverState()
    {
        var rec = new R();
        rec.Mount(UI.Box(new PropsBuilder().ClassName("btn").Build()));
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { [".btn:hover"] = Marker });

        var result = sheet.Match(rec.Root!, Hover);
        Assert.Equal(99, result.ZIndex);
    }

    [Fact]
    public void Pseudo_Hover_NoMatchWithoutHover()
    {
        var rec = new R();
        rec.Mount(UI.Box(new PropsBuilder().ClassName("btn").Build()));
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { [".btn:hover"] = Marker });

        var result = sheet.Match(rec.Root!, None);
        Assert.NotEqual(99, result.ZIndex);
    }

    [Fact]
    public void Pseudo_Active_MatchesOnActiveState()
    {
        var rec = new R();
        rec.Mount(UI.Box());
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { ["box:active"] = Marker });

        var result = sheet.Match(rec.Root!, Active);
        Assert.Equal(99, result.ZIndex);
    }

    [Fact]
    public void Pseudo_Focus_MatchesOnFocusState()
    {
        var rec = new R();
        rec.Mount(UI.Box());
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { ["box:focus"] = Marker });

        var result = sheet.Match(rec.Root!, Focus);
        Assert.Equal(99, result.ZIndex);
    }

    [Fact]
    public void Pseudo_FirstChild_MatchesFirstSibling()
    {
        var tree = UI.Box(
            UI.Box(new PropsBuilder().ClassName("a").Build()),
            UI.Box(new PropsBuilder().ClassName("b").Build()));
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { ["box:first-child"] = Marker });

        var first = rec.Root!.Child!;
        var second = first.Sibling!;

        Assert.Equal(99, sheet.Match(first, None).ZIndex);
        Assert.NotEqual(99, sheet.Match(second, None).ZIndex);
    }

    [Fact]
    public void Pseudo_LastChild_MatchesLastSibling()
    {
        var tree = UI.Box(
            UI.Box(new PropsBuilder().ClassName("a").Build()),
            UI.Box(new PropsBuilder().ClassName("b").Build()));
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { ["box:last-child"] = Marker });

        var first = rec.Root!.Child!;
        var second = first.Sibling!;

        Assert.NotEqual(99, sheet.Match(first, None).ZIndex);
        Assert.Equal(99, sheet.Match(second, None).ZIndex);
    }

    // ── nth-child ─────────────────────────────────────────────────────────────

    [Fact]
    public void Pseudo_NthChild_Odd_MatchesOddPositions()
    {
        var tree = UI.Box(
            UI.Box(new PropsBuilder().ClassName("a").Build()),
            UI.Box(new PropsBuilder().ClassName("b").Build()),
            UI.Box(new PropsBuilder().ClassName("c").Build()));
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { ["box:nth-child(odd)"] = Marker });

        var first  = rec.Root!.Child!;
        var second = first.Sibling!;
        var third  = second.Sibling!;

        Assert.Equal(99,  sheet.Match(first, None).ZIndex);   // 1st = odd
        Assert.NotEqual(99, sheet.Match(second, None).ZIndex); // 2nd = even
        Assert.Equal(99,  sheet.Match(third, None).ZIndex);   // 3rd = odd
    }

    [Fact]
    public void Pseudo_NthChild_Even_MatchesEvenPositions()
    {
        var tree = UI.Box(
            UI.Box(),
            UI.Box(),
            UI.Box());
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { ["box:nth-child(even)"] = Marker });

        var first  = rec.Root!.Child!;
        var second = first.Sibling!;

        Assert.NotEqual(99, sheet.Match(first, None).ZIndex);
        Assert.Equal(99,    sheet.Match(second, None).ZIndex);
    }

    [Fact]
    public void Pseudo_NthChild_N_MatchesExactPosition()
    {
        var tree = UI.Box(UI.Box(), UI.Box(), UI.Box());
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { ["box:nth-child(2)"] = Marker });

        var first  = rec.Root!.Child!;
        var second = first.Sibling!;
        var third  = second.Sibling!;

        Assert.NotEqual(99, sheet.Match(first, None).ZIndex);
        Assert.Equal(99,    sheet.Match(second, None).ZIndex);
        Assert.NotEqual(99, sheet.Match(third, None).ZIndex);
    }

    // ── Not pseudo ────────────────────────────────────────────────────────────

    [Fact]
    public void Pseudo_Not_ExcludesMatchingSelector()
    {
        var tree = UI.Box(
            UI.Box(new PropsBuilder().ClassName("special").Build()),
            UI.Box(new PropsBuilder().ClassName("normal").Build()));
        var rec = new R();
        rec.Mount(tree);
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { ["box:not(.special)"] = Marker });

        var special = FindByClass(rec.Root!, "special");
        var normal  = FindByClass(rec.Root!, "normal");
        Assert.NotNull(special);
        Assert.NotNull(normal);

        Assert.NotEqual(99, sheet.Match(special!, None).ZIndex);
        Assert.Equal(99,    sheet.Match(normal!, None).ZIndex);
    }

    // ── Compound selector (element.class) ─────────────────────────────────────

    [Fact]
    public void Compound_ElementDotClass_MatchesBoth()
    {
        var rec = new R();
        rec.Mount(UI.Box(new PropsBuilder().ClassName("card").Build()));
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { ["box.card"] = Marker });

        var result = sheet.Match(rec.Root!, None);
        Assert.Equal(99, result.ZIndex);
    }

    [Fact]
    public void Compound_ElementDotClass_NoMatchOnWrongElement()
    {
        // Text.card should not match a Box with class "card"
        var rec = new R();
        rec.Mount(UI.Box(new PropsBuilder().ClassName("card").Build()));
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
            { ["text.card"] = Marker });

        var result = sheet.Match(rec.Root!, None);
        Assert.NotEqual(99, result.ZIndex);
    }

    // ── Rule cascade (multiple matching rules merge) ───────────────────────────

    [Fact]
    public void Sheet_MultipleMatchingRules_StylesMerged()
    {
        var rec = new R();
        rec.Mount(UI.Box(new PropsBuilder().ClassName("card").Build()));
        var sheet = CSSSSheet.FromDictionary("t", new Dictionary<string, StyleSheet>
        {
            ["box"]   = new StyleSheet { ZIndex = 1 },
            [".card"] = new StyleSheet { Opacity = 0.8f },
        });

        var result = sheet.Match(rec.Root!, None);
        Assert.Equal(1,    result.ZIndex);
        Assert.True(Math.Abs(0.8f - (result.Opacity ?? 0f)) < 0.01f);
    }
}
