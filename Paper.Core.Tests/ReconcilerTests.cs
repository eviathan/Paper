using Paper.Core.Components;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Xunit;
using Fiber      = Paper.Core.Reconciler.Fiber;
using R = Paper.Core.Reconciler.Reconciler;
using static Paper.Core.Hooks.Hooks;

namespace Paper.Core.Tests;

/// <summary>
/// Tests for the virtual-DOM reconciler: mounting, updating, keyed children,
/// error boundaries, and the stale-Parent pointer regression.
/// </summary>
[Collection("Sequential")]
public sealed class ReconcilerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Collect all fibers depth-first (child before sibling).</summary>
    private static List<Fiber> CollectAll(Fiber? root)
    {
        var list = new List<Fiber>();
        Collect(root, list);
        return list;
    }

    private static void Collect(Fiber? fiber, List<Fiber> list)
    {
        if (fiber == null) return;
        list.Add(fiber);
        Collect(fiber.Child, list);
        Collect(fiber.Sibling, list);
    }

    private static List<string> ChildTypes(Fiber parent)
    {
        var types = new List<string>();
        var child = parent.Child;
        while (child != null)
        {
            types.Add(child.Type as string ?? child.Type.ToString()!);
            child = child.Sibling;
        }
        return types;
    }

    // ── Mount ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Mount_Box_CreatesRootFiber()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Box());

        Assert.NotNull(reconciler.Root);
        Assert.Equal(ElementTypes.Box, reconciler.Root!.Type);
        Assert.Null(reconciler.Root.Parent);
    }

    [Fact]
    public void Mount_WithChildren_BuildsChildTree()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Box(
            UI.Text("A"),
            UI.Text("B")
        ));

        var root = reconciler.Root!;
        var types = ChildTypes(root);

        Assert.Equal(2, types.Count);
        Assert.All(types, t => Assert.Equal(ElementTypes.Text, t));
    }

    [Fact]
    public void Mount_NestedBoxes_ParentLinksAreCorrect()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Box(UI.Box(UI.Text("deep"))));

        var root  = reconciler.Root!;
        var inner = root.Child!;
        var text  = inner.Child!;

        Assert.Equal(root, inner.Parent);
        Assert.Equal(inner, text.Parent);
    }

    // ── Update / Reconcile ────────────────────────────────────────────────────

    [Fact]
    public void Update_SameTypeAndProps_RootFiberIsReused()
    {
        var reconciler = new R();
        // Use Props.Empty so both mount and update share the same props reference,
        // making ShallowEqual return true and triggering ShouldSkipReconciliation.
        reconciler.Mount(UI.Box(Props.Empty));
        var originalRoot = reconciler.Root;

        reconciler.Update(UI.Box(Props.Empty));

        Assert.Same(originalRoot, reconciler.Root);
    }

    [Fact]
    public void Update_DifferentType_RootFiberIsReplaced()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Box());
        var originalRoot = reconciler.Root;

        reconciler.Update(UI.Text("hello"));

        Assert.NotSame(originalRoot, reconciler.Root);
        Assert.Equal(ElementTypes.Text, reconciler.Root!.Type);
    }

    [Fact]
    public void Update_PropsChange_ChildFiberIsUpdated()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Box(UI.Text("old")));

        reconciler.Update(UI.Box(UI.Text("new")));

        var textFiber = reconciler.Root!.Child!;
        Assert.Equal("new", textFiber.Props.Text);
    }

    // ── Keyed children ────────────────────────────────────────────────────────

    [Fact]
    public void Update_KeyedChildren_Reorders_PreservingIdentity()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Box(
            UI.Text("A", key: "a"),
            UI.Text("B", key: "b")
        ));

        var fiberA = reconciler.Root!.Child!;         // first
        var fiberB = reconciler.Root!.Child!.Sibling!; // second

        // Swap order
        reconciler.Update(UI.Box(
            UI.Text("B", key: "b"),
            UI.Text("A", key: "a")
        ));

        var newFirst  = reconciler.Root!.Child!;
        var newSecond = reconciler.Root!.Child!.Sibling!;

        // Keyed fibers should be reused at their new positions
        Assert.Same(fiberB, newFirst);
        Assert.Same(fiberA, newSecond);
    }

    [Fact]
    public void Update_RemovedChild_IsUnmounted()
    {
        bool cleanupRan = false;

        UINode Component(Props _)
        {
            UseEffect(() => () => { cleanupRan = true; }, Array.Empty<object>());
            return UI.Box();
        }

        var reconciler = new R();
        reconciler.Mount(UI.Box(
            UI.Component(Component, key: "c")
        ));

        // Remove the child — Component should be unmounted and cleanup should run
        reconciler.Update(UI.Box());

        Assert.True(cleanupRan);
    }

    [Fact]
    public void Update_AddedChild_IsPlaced()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Box(UI.Text("A")));

        reconciler.Update(UI.Box(UI.Text("A"), UI.Text("B")));

        var types = ChildTypes(reconciler.Root!);
        Assert.Equal(2, types.Count);
    }

    // ── Stale-Parent regression ───────────────────────────────────────────────

    /// <summary>
    /// Regression: when ShouldSkipReconciliation reuses a fiber, its Parent must be
    /// updated to the current parent. Without the fix, a fiber reused under a new parent
    /// retains a stale Parent pointer to the old (uncommitted) parent fiber.
    /// </summary>
    [Fact]
    public void Update_ReusedFiber_HasCorrectParent()
    {
        // Build a component that always renders the same child so it gets reused.
        var style = new StyleSheet { Width = Length.Px(100) };

        var reconciler = new R();
        reconciler.Mount(UI.Box(UI.Text("hello", style: style)));

        var originalText = reconciler.Root!.Child!;

        // Update with props change that forces Box to re-create, but Text props unchanged
        // so Text fiber gets reused via ShouldSkipReconciliation.
        reconciler.Update(UI.Box(UI.Text("hello", style: style)));

        var newRoot = reconciler.Root!;
        var reusedText = newRoot.Child!;

        // The reused Text fiber's Parent must point to the live (new) root,
        // not to the old root fiber that was replaced during reconciliation.
        Assert.Same(newRoot, reusedText.Parent);
    }

    // ── Function components ───────────────────────────────────────────────────

    [Fact]
    public void FunctionComponent_Renders_IntoFiberTree()
    {
        UINode MyComp(Props _) => UI.Text("from-func");

        var reconciler = new R();
        reconciler.Mount(UI.Component(MyComp));

        // The function component fiber has the function as its type
        var compFiber = reconciler.Root!;
        Assert.Equal((Func<Props, UINode>)MyComp, compFiber.Type);

        // Its child is the Text returned by the function
        var child = compFiber.Child!;
        Assert.Equal(ElementTypes.Text, child.Type);
        Assert.Equal("from-func", child.Props.Text);
    }

    [Fact]
    public void FunctionComponent_WithState_UpdatesOnRerender()
    {
        Action<string>? capturedSet = null;

        UINode Counter(Props _)
        {
            var (value, setValue, _) = UseState("initial");
            capturedSet = setValue;
            return UI.Text(value);
        }

        var reconciler = new R();
        reconciler.Mount(UI.Component(Counter));

        Assert.Equal("initial", reconciler.Root!.Child!.Props.Text);

        capturedSet!("updated");
        reconciler.Update(UI.Component(Counter));

        Assert.Equal("updated", reconciler.Root!.Child!.Props.Text);
    }

    [Fact]
    public void FunctionComponent_WithUpdateState_AppliesUpdater()
    {
        Action<Func<int, int>>? capturedUpdate = null;

        UINode Counter(Props _)
        {
            var (count, _, updateCount) = UseState(0);
            capturedUpdate = updateCount;
            return UI.Text(count.ToString());
        }

        var reconciler = new R();
        reconciler.Mount(UI.Component(Counter));
        Assert.Equal("0", reconciler.Root!.Child!.Props.Text);

        capturedUpdate!(prev => prev + 1);
        reconciler.Update(UI.Component(Counter));
        Assert.Equal("1", reconciler.Root!.Child!.Props.Text);

        capturedUpdate!(prev => prev + 1);
        reconciler.Update(UI.Component(Counter));
        Assert.Equal("2", reconciler.Root!.Child!.Props.Text);
    }

    // ── Class components ──────────────────────────────────────────────────────

    private sealed class LabelComponent : Component
    {
        public override UINode Render() => UI.Text(Props.Get<string>("label") ?? "");
    }

    [Fact]
    public void ClassComponent_Renders_ViaInstance()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Component<LabelComponent>(
            new PropsBuilder().Set("label", "hello").Build()));

        var text = reconciler.Root!.Child!;
        Assert.Equal("hello", text.Props.Text);
    }

    [Fact]
    public void ClassComponent_SameInstance_ReusedAcrossUpdates()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Component<LabelComponent>(
            new PropsBuilder().Set("label", "v1").Build()));

        var instance1 = reconciler.Root!.Instance;

        reconciler.Update(UI.Component<LabelComponent>(
            new PropsBuilder().Set("label", "v2").Build()));

        var instance2 = reconciler.Root!.Instance;

        Assert.Same(instance1, instance2);
        Assert.Equal("v2", reconciler.Root!.Child!.Props.Text);
    }

    // ── Error boundaries ──────────────────────────────────────────────────────

    private sealed class ErrorBoundary : Component, IErrorBoundary
    {
        public UINode RenderFallback(Exception ex) => UI.Text($"Error: {ex.Message}");

        public override UINode Render() =>
            UI.Box(Props.Children?.Cast<UINode>().ToArray() ?? []);
    }

    private sealed class ThrowingComponent : Component
    {
        public override UINode Render() => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void ErrorBoundary_CatchesDescendantException_ShowsFallback()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Component<ErrorBoundary>(
            new PropsBuilder().Children([UI.Component<ThrowingComponent>()]).Build()));

        // Boundary's CaughtError should be set
        var boundaryFiber = reconciler.Root!;
        Assert.NotNull(boundaryFiber.CaughtError);

        // Its rendered child should be the fallback Text
        var fallback = boundaryFiber.Child!;
        Assert.Equal(ElementTypes.Text, fallback.Type);
        Assert.Contains("boom", fallback.Props.Text ?? "");
    }

    [Fact]
    public void TopLevel_UnhandledError_DoesNotCrashHost()
    {
        // Without an error boundary, top-level errors are caught internally
        // and replaced with an error Text node — the host should not throw.
        UINode Boom(Props _) => throw new Exception("top-level failure");

        var reconciler = new R();
        // Should not throw
        reconciler.Mount(UI.Component(Boom));
        Assert.NotNull(reconciler.Root);
    }

    // ── Keyed children (extended) ─────────────────────────────────────────────

    [Fact]
    public void Update_KeyedChildren_ThreeItems_RotatedOrder_PreservesIdentity()
    {
        // Rotate [a, b, c] → [c, a, b] — tests that all three fibers keep identity.
        var reconciler = new R();
        reconciler.Mount(UI.Box(
            UI.Text("A", key: "a"),
            UI.Text("B", key: "b"),
            UI.Text("C", key: "c")
        ));

        var fA = reconciler.Root!.Child!;
        var fB = fA.Sibling!;
        var fC = fB.Sibling!;

        reconciler.Update(UI.Box(
            UI.Text("C", key: "c"),
            UI.Text("A", key: "a"),
            UI.Text("B", key: "b")
        ));

        Assert.Same(fC, reconciler.Root!.Child!);
        Assert.Same(fA, reconciler.Root!.Child!.Sibling!);
        Assert.Same(fB, reconciler.Root!.Child!.Sibling!.Sibling!);
    }

    [Fact]
    public void Update_KeyedChildren_AddItemAtStart_PreservesExisting()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Box(
            UI.Text("B", key: "b"),
            UI.Text("C", key: "c")
        ));

        var fB = reconciler.Root!.Child!;
        var fC = fB.Sibling!;

        reconciler.Update(UI.Box(
            UI.Text("A", key: "a"), // new at start
            UI.Text("B", key: "b"),
            UI.Text("C", key: "c")
        ));

        var newA = reconciler.Root!.Child!;
        var newB = newA.Sibling!;
        var newC = newB.Sibling!;

        // A is new (not one of the original fibers); B and C are reused.
        Assert.NotSame(fB, newA);
        Assert.NotSame(fC, newA);
        Assert.Same(fB, newB);
        Assert.Same(fC, newC);
    }

    [Fact]
    public void Update_KeyedChildren_RemoveMiddle_SiblingChainCorrect()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Box(
            UI.Text("A", key: "a"),
            UI.Text("B", key: "b"),
            UI.Text("C", key: "c")
        ));

        var fA = reconciler.Root!.Child!;
        var fC = fA.Sibling!.Sibling!;

        // Remove B
        reconciler.Update(UI.Box(
            UI.Text("A", key: "a"),
            UI.Text("C", key: "c")
        ));

        var root = reconciler.Root!;
        Assert.Same(fA, root.Child!);
        Assert.Same(fC, root.Child!.Sibling!);
        Assert.Null(root.Child!.Sibling!.Sibling); // chain terminated
    }

    // ── ShouldComponentUpdate ─────────────────────────────────────────────────

    private sealed class AlwaysSkipComp : Component
    {
        public int Renders;
        public override bool ShouldComponentUpdate(Props nextProps) => false;
        public override UINode Render() { Renders++; return UI.Box(); }
    }

    private sealed class DefaultComp : Component
    {
        public int Renders;
        public override UINode Render() { Renders++; return UI.Box(); }
    }

    [Fact]
    public void ShouldComponentUpdate_ReturnFalse_SkipsRender()
    {
        var reconciler = new R();
        reconciler.Mount(UI.Component<AlwaysSkipComp>());
        var instance = (AlwaysSkipComp)reconciler.Root!.Instance!;
        int rendersAfterMount = instance.Renders;

        // Update twice — ShouldComponentUpdate=false must prevent re-render.
        reconciler.Update(UI.Component<AlwaysSkipComp>());
        reconciler.Update(UI.Component<AlwaysSkipComp>());

        Assert.Equal(rendersAfterMount, instance.Renders);
    }

    [Fact]
    public void ShouldComponentUpdate_DefaultImpl_TrueAlways_AllowsRerender()
    {
        // Default ShouldComponentUpdate returns true — component re-renders each update.
        var reconciler = new R();
        reconciler.Mount(UI.Component<DefaultComp>());
        var instance = (DefaultComp)reconciler.Root!.Instance!;
        int after1 = instance.Renders;

        reconciler.Update(UI.Component<DefaultComp>());
        int after2 = instance.Renders;

        Assert.True(after2 > after1, "Expected re-render after Update with default ShouldComponentUpdate");
    }

    // ── Portal ────────────────────────────────────────────────────────────────

    private static UINode Portal(params UINode[] children)
    {
        var props = new Paper.Core.VirtualDom.PropsBuilder().Children(children).Build();
        return new UINode(Paper.Core.VirtualDom.ElementTypes.Portal, props);
    }

    [Fact]
    public void Portal_ChildrenCollected_InPortalRoots()
    {
        var rec = new R();
        rec.Mount(UI.Box(Portal(UI.Text("overlay"))));

        // Portal fibers should be collected in PortalRoots, not in the main tree.
        Assert.NotEmpty(rec.PortalRoots);
    }

    [Fact]
    public void UI_Portal_Helper_ProducesPortalType()
    {
        var node = UI.Portal(UI.Text("overlay"), UI.Text("tooltip"));
        Assert.Equal(Paper.Core.VirtualDom.ElementTypes.Portal, node.Type);
    }

    [Fact]
    public void UI_Portal_Helper_PopulatesPortalRoots()
    {
        var rec = new R();
        rec.Mount(UI.Box(UI.Portal(UI.Text("overlay"))));
        Assert.NotEmpty(rec.PortalRoots);
    }

    // ── OnError event ─────────────────────────────────────────────────────────────

    [Fact]
    public void OnError_TopLevelMountFailure_InvokedWithCorrectPhase()
    {
        UINode Boom(Props _) => throw new InvalidOperationException("mount-fail");

        Paper.Core.Reconciler.ReconcilerError? received = null;
        var rec = new R();
        rec.OnError += e => received = e;
        rec.Mount(UI.Component(Boom));

        Assert.NotNull(received);
        Assert.Equal(Paper.Core.Reconciler.ReconcilerErrorPhase.Mount, received!.Value.Phase);
        Assert.False(received.Value.IsBoundary);
        Assert.IsType<InvalidOperationException>(received.Value.Exception);
    }

    [Fact]
    public void OnError_TopLevelUpdateFailure_InvokedWithCorrectPhase()
    {
        int renderCount = 0;
        UINode Comp(Props _)
        {
            renderCount++;
            if (renderCount > 1) throw new InvalidOperationException("update-fail");
            return UI.Box();
        }

        Paper.Core.Reconciler.ReconcilerError? received = null;
        var rec = new R();
        rec.OnError += e => received = e;
        var root = UI.Component(Comp);
        rec.Mount(root);
        rec.Update(root, forceReconcile: true);

        Assert.NotNull(received);
        Assert.Equal(Paper.Core.Reconciler.ReconcilerErrorPhase.Update, received!.Value.Phase);
        Assert.False(received.Value.IsBoundary);
    }

    [Fact]
    public void OnError_BoundaryCaughtError_InvokedWithIsBoundaryTrue()
    {
        UINode Boom(Props _) => throw new InvalidOperationException("child-fail");

        Paper.Core.Reconciler.ReconcilerError? received = null;
        var rec = new R();
        rec.OnError += e => received = e;
        rec.Mount(UI.Component<ErrorBoundary>(
            new PropsBuilder().Children(UI.Component(Boom)).Build()));

        Assert.NotNull(received);
        Assert.True(received!.Value.IsBoundary);
        Assert.Equal(Paper.Core.Reconciler.ReconcilerErrorPhase.Update, received.Value.Phase);
    }

    [Fact]
    public void Portal_MainTree_DoesNotContainPortalChildren()
    {
        static bool HasTextFiber(Fiber? f, string text)
        {
            if (f == null) return false;
            if (f.Props.Text == text && f.Type is string t && t == Paper.Core.VirtualDom.ElementTypes.Text) return true;
            return HasTextFiber(f.Child, text) || HasTextFiber(f.Sibling, text);
        }

        var rec = new R();
        rec.Mount(UI.Box(Portal(UI.Text("portal-only"))));

        Assert.False(HasTextFiber(rec.Root, "portal-only"));
    }

    [Fact]
    public void NestedChildState_WithoutParentStateChange_TriggersChildRerender()
    {
        // Simulates: App (no state) wraps Child (has state).
        // When only the child's setState fires, Update(forceReconcile:false) should
        // still re-render the child — this is the hover-state-not-propagating bug.
        Action<string>? capturedSet = null;

        UINode Child(Props _)
        {
            var (val, setVal, _) = UseState("default");
            capturedSet = setVal;
            return UI.Text(val);
        }

        UINode App(Props _) => UI.Box(UI.Component(Child));

        var reconciler = new R();
        var appNode = UI.Component(App);
        reconciler.Mount(appNode);

        // Tree: App → Box → Child (Func) → Text
        // Verify initial render
        var textFiber = reconciler.Root!.Child!.Child!.Child!;
        Assert.Equal("default", textFiber.Props.Text);

        // Simulate OnMouseEnter calling child setState — no parent state change
        capturedSet!("hovered");

        // Update without forceReconcile, as the real render loop does
        reconciler.Update(appNode, forceReconcile: false);

        var updatedText = reconciler.Root!.Child!.Child!.Child!;
        Assert.Equal("hovered", updatedText.Props.Text);
    }
}
