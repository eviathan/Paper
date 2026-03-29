using Paper.Core.Hooks;
using Paper.Core.VirtualDom;
using Xunit;
using static Paper.Core.Hooks.Hooks;
using R = Paper.Core.Reconciler.Reconciler;

namespace Paper.Core.Tests;

/// <summary>
/// Tests for UseVirtualScroll hook and the UI.List virtualized list primitive.
/// </summary>
[Collection("Sequential")]
public sealed class VirtualScrollTests
{
    // ── UseVirtualScroll via UI.List ──────────────────────────────────────────

    private static readonly string[] Items100 =
        Enumerable.Range(0, 100).Select(i => $"Item {i}").ToArray();

    [Fact]
    public void VirtualList_MountsWithoutError()
    {
        var rec = new R();
        rec.Mount(UI.List(
            Items100,
            itemHeight: 40f,
            containerH: 200f,
            renderItem: (item, _) => UI.Text(item)));

        Assert.NotNull(rec.Root);
    }

    [Fact]
    public void VirtualList_RendersOnlyVisibleItems()
    {
        // 200px container / 40px items → 5 visible rows. With default overscan=3, up to 11 rows max.
        var rec = new R();
        rec.Mount(UI.List(
            Items100,
            itemHeight: 40f,
            containerH: 200f,
            renderItem: (item, _) => UI.Text(item.ToString()!)));

        // Count how many Text fibers are rendered
        int textCount = CountFibers(rec.Root, "text");
        // With overscan=3: start=max(0,0-3)=0, end=min(99,5+3)=8 → 9 items, not all 100
        Assert.True(textCount < Items100.Length, $"Expected fewer than {Items100.Length} text nodes, got {textCount}");
        Assert.True(textCount >= 5, $"Expected at least 5 visible rows, got {textCount}");
    }

    [Fact]
    public void VirtualList_TotalHeightMatchesItemCount()
    {
        // The VirtualScrollState.TotalHeight (100 × 36 = 3600) is reported correctly.
        // The List component now uses absolute positioning, so there's no spacer box in the tree;
        // verify via the UseVirtualScroll hook directly.
        VirtualScrollState<string>? vs = null;
        UINode Comp(Props _)
        {
            vs = UseVirtualScroll<string>(Items100, itemHeight: 36f, containerH: 180f, overscan: 3);
            return UI.Box();
        }
        var rec = new R();
        rec.Mount(UI.Component(Comp));
        Assert.NotNull(vs);
        Assert.Equal(100 * 36f, vs!.TotalHeight, 1f);
    }

    [Fact]
    public void VirtualList_EmptyList_RendersNoItems()
    {
        var rec = new R();
        rec.Mount(UI.List(
            Array.Empty<string>(),
            itemHeight: 48f,
            containerH: 300f,
            renderItem: (item, _) => UI.Text(item.ToString()!)));

        int textCount = CountFibers(rec.Root, "text");
        Assert.Equal(0, textCount);
    }

    [Fact]
    public void VirtualList_FewerItemsThanViewport_AllRendered()
    {
        var fewItems = new[] { "A", "B", "C" };
        var rec = new R();
        rec.Mount(UI.List(
            fewItems,
            itemHeight: 40f,
            containerH: 400f,  // fits all 3 easily
            renderItem: (item, _) => UI.Text(item.ToString()!)));

        int textCount = CountFibers(rec.Root, "text");
        Assert.Equal(3, textCount); // all items rendered
    }

    // ── UseVirtualScroll hook directly ────────────────────────────────────────

    [Fact]
    public void UseVirtualScroll_InitialState_StartsAtTop()
    {
        var items = Enumerable.Range(0, 50).Select(i => i).ToArray();

        VirtualScrollState<int>? vs = null;
        UINode Comp(Props _)
        {
            vs = UseVirtualScroll(items, itemHeight: 40f, containerH: 200f, overscan: 0);
            return UI.Box();
        }

        var rec = new R();
        rec.Mount(UI.Component(Comp));

        Assert.NotNull(vs);
        // At scroll=0, first item should be index 0
        Assert.Equal(0, vs!.VisibleItems.First().index);
    }

    [Fact]
    public void UseVirtualScroll_CorrectVisibleCount()
    {
        // 200px / 40px = 5 visible rows, overscan=0
        var items = Enumerable.Range(0, 50).Select(i => i).ToArray();

        VirtualScrollState<int>? vs = null;
        UINode Comp(Props _)
        {
            vs = UseVirtualScroll(items, itemHeight: 40f, containerH: 200f, overscan: 0);
            return UI.Box();
        }

        var rec = new R();
        rec.Mount(UI.Component(Comp));

        Assert.NotNull(vs);
        // firstVisible=0, lastVisible=ceil(200/40)=5, so indices 0..5 inclusive = 6 items
        Assert.Equal(6, vs!.VisibleItems.Count);
    }

    [Fact]
    public void UseVirtualScroll_TotalHeightIsCorrect()
    {
        var items = Enumerable.Range(0, 100).Select(i => i).ToArray();
        VirtualScrollState<int>? vs = null;
        UINode Comp(Props _)
        {
            vs = UseVirtualScroll(items, itemHeight: 36f, containerH: 200f, overscan: 0);
            return UI.Box();
        }

        var rec = new R();
        rec.Mount(UI.Component(Comp));

        Assert.NotNull(vs);
        Assert.Equal(100 * 36f, vs!.TotalHeight, 1f);
    }

    [Fact]
    public void UseVirtualScroll_PaddingTopIsZeroAtScrollOrigin()
    {
        var items = Enumerable.Range(0, 50).Select(i => i).ToArray();
        VirtualScrollState<int>? vs = null;
        UINode Comp(Props _)
        {
            vs = UseVirtualScroll(items, itemHeight: 40f, containerH: 200f, overscan: 0);
            return UI.Box();
        }

        var rec = new R();
        rec.Mount(UI.Component(Comp));

        Assert.Equal(0f, vs!.PaddingTop, 0.1f);
    }

    [Fact]
    public void UseVirtualScroll_OnWheel_ScrollsDown()
    {
        // Simulate a wheel event to scroll the list downward.
        var items = Enumerable.Range(0, 50).Select(i => i).ToArray();
        Action<Paper.Core.Events.PointerEvent>? wheel = null;
        VirtualScrollState<int>? vs = null;

        UINode Comp(Props _)
        {
            vs = UseVirtualScroll(items, itemHeight: 40f, containerH: 200f, overscan: 0);
            wheel = vs.OnWheel;
            return UI.Box();
        }

        var rec = new R();
        var root = UI.Component(Comp);
        rec.Mount(root);

        // Scroll down by 4 notches (4 × 24 = 96px → just over 2 items)
        wheel!.Invoke(new Paper.Core.Events.PointerEvent { WheelDeltaY = -4f });
        rec.Update(root);

        Assert.NotNull(vs);
        // After scrolling 96px down: first visible item should be at index 2 (96/40=2.4)
        Assert.True(vs!.VisibleItems.First().index >= 2,
            $"Expected first visible index >= 2 after scroll, got {vs.VisibleItems.First().index}");
        Assert.True(vs.PaddingTop > 0f, "Expected non-zero PaddingTop after scrolling");
    }

    [Fact]
    public void VirtualList_DefaultRenderItem_RendersStringItems()
    {
        // Uses the overload with no renderItem — items rendered as Text nodes.
        var items = new[] { "Alpha", "Beta", "Gamma" };
        var rec = new R();
        rec.Mount(UI.List(items, itemHeight: 40f, containerH: 300f));

        Assert.NotNull(rec.Root);
        // All 3 items fit in the viewport — verify some text fibers are rendered.
        int textCount = CountFibers(rec.Root, "text");
        Assert.True(textCount >= 3, $"Expected at least 3 text nodes, got {textCount}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int CountFibers(Paper.Core.Reconciler.Fiber? f, string type)
    {
        if (f == null) return 0;
        int count = (f.Type is string t && t == type) ? 1 : 0;
        return count + CountFibers(f.Child, type) + CountFibers(f.Sibling, type);
    }

    private static bool FindFiberWithHeight(Paper.Core.Reconciler.Fiber? f, float targetHeight)
    {
        if (f == null) return false;
        var h = f.Props.Style?.Height;
        if (h != null && h.Value == Paper.Core.Styles.Length.Px(targetHeight)) return true;
        return FindFiberWithHeight(f.Child, targetHeight) || FindFiberWithHeight(f.Sibling, targetHeight);
    }
}
