using Paper.Core.Hooks;
using Paper.Core.VirtualDom;
using Xunit;
using PaperReconciler = Paper.Core.Reconciler.Reconciler;
using static Paper.Core.Hooks.Hooks;

namespace Paper.Core.Tests;

/// <summary>
/// Tests for the Hooks API: UseState, UseEffect, UseMemo, UseRef, UseReducer.
/// All hooks must run inside a reconciled component, so each test mounts a
/// function component and inspects observable side-effects.
///
/// Tests that need the component to re-render use forceReconcile:true on Update,
/// because ShouldSkipReconciliation will skip re-rendering when props are unchanged
/// and there is no pending state — which is equivalent to React.memo wrapping.
/// </summary>
[Collection("Sequential")]
public sealed class HooksTests
{
    // ── UseState ──────────────────────────────────────────────────────────────

    [Fact]
    public void UseState_ReturnsInitialValue()
    {
        string? rendered = null;

        UINode Comp(Props _)
        {
            var (value, _, _) = UseState("hello");
            rendered = value;
            return UI.Box();
        }

        new PaperReconciler().Mount(UI.Component(Comp));

        Assert.Equal("hello", rendered);
    }

    [Fact]
    public void UseState_SetState_UpdatesOnNextRender()
    {
        Action<int>? set = null;

        UINode Comp(Props _)
        {
            var (v, setState, _) = UseState(0);
            set = setState;
            return UI.Text(v.ToString());
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);

        reconciler.Mount(root);
        Assert.Equal("0", reconciler.Root!.Child!.Props.Text);

        set!(99);
        reconciler.Update(root);
        Assert.Equal("99", reconciler.Root!.Child!.Props.Text);
    }

    [Fact]
    public void UseState_UpdateState_AppliesUpdaterFunction()
    {
        Action<Func<int, int>>? update = null;

        UINode Comp(Props _)
        {
            var (v, _, updateState) = UseState(10);
            update = updateState;
            return UI.Text(v.ToString());
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);

        update!(x => x * 2);
        reconciler.Update(root);
        Assert.Equal("20", reconciler.Root!.Child!.Props.Text);
    }

    [Fact]
    public void UseState_MultipleStates_AreIndependent()
    {
        Action<string>? setA = null;
        Action<string>? setB = null;

        UINode Comp(Props _)
        {
            var (a, sa, _) = UseState("a0");
            var (b, sb, _) = UseState("b0");
            setA = sa;
            setB = sb;
            return UI.Text($"{a}:{b}");
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);

        setA!("a1");
        reconciler.Update(root);
        Assert.Equal("a1:b0", reconciler.Root!.Child!.Props.Text);

        setB!("b1");
        reconciler.Update(root);
        Assert.Equal("a1:b1", reconciler.Root!.Child!.Props.Text);
    }

    // ── UseEffect ─────────────────────────────────────────────────────────────

    [Fact]
    public void UseEffect_NoDepsMeansRunEveryRender()
    {
        int runCount = 0;

        UINode Comp(Props _)
        {
            UseEffect(() => { runCount++; return null; }); // null deps = every render
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);
        Assert.Equal(1, runCount);

        // forceReconcile:true bypasses ShouldSkipReconciliation so the component actually re-runs
        reconciler.Update(root, forceReconcile: true);
        Assert.Equal(2, runCount);
    }

    [Fact]
    public void UseEffect_EmptyDeps_RunsOnlyOnMount()
    {
        int runCount = 0;

        UINode Comp(Props _)
        {
            UseEffect(() => { runCount++; return null; }, Array.Empty<object>());
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);
        Assert.Equal(1, runCount);

        // Even with forced re-render, empty-deps effect should only run once
        reconciler.Update(root, forceReconcile: true);
        Assert.Equal(1, runCount); // should NOT run again
    }

    [Fact]
    public void UseEffect_Deps_RerunsWhenDepChanges()
    {
        int runCount = 0;
        string dep = "v1";

        UINode Comp(Props _)
        {
            UseEffect(() => { runCount++; return null; }, new object[] { dep });
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);
        Assert.Equal(1, runCount);

        // Same dep value → effect should not re-run
        reconciler.Update(root, forceReconcile: true);
        Assert.Equal(1, runCount);

        // Dep changed → effect should re-run
        dep = "v2";
        reconciler.Update(root, forceReconcile: true);
        Assert.Equal(2, runCount);
    }

    [Fact]
    public void UseEffect_Cleanup_RunsBeforeNextEffect()
    {
        var log = new List<string>();
        string dep = "v1";

        UINode Comp(Props _)
        {
            UseEffect(() =>
            {
                var captured = dep;
                log.Add($"run:{captured}");
                return () => log.Add($"cleanup:{captured}");
            }, new object[] { dep });
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);
        Assert.Equal(["run:v1"], log);

        dep = "v2";
        reconciler.Update(root, forceReconcile: true);

        // cleanup from v1 runs before v2's effect
        Assert.Equal(["run:v1", "cleanup:v1", "run:v2"], log);
    }

    [Fact]
    public void UseEffect_Cleanup_RunsOnUnmount()
    {
        bool cleanupRan = false;

        UINode Comp(Props _)
        {
            UseEffect(() => () => { cleanupRan = true; }, Array.Empty<object>());
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        reconciler.Mount(UI.Box(UI.Component(Comp, key: "c")));

        // Remove the component — should trigger unmount cleanup
        reconciler.Update(UI.Box());

        Assert.True(cleanupRan);
    }

    // ── UseMemo ───────────────────────────────────────────────────────────────

    [Fact]
    public void UseMemo_ComputesOnlyOnce_WithEmptyDeps()
    {
        int computeCount = 0;

        UINode Comp(Props _)
        {
            UseMemo(() => { computeCount++; return 42; }, Array.Empty<object>());
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);
        Assert.Equal(1, computeCount);

        reconciler.Update(root, forceReconcile: true);
        Assert.Equal(1, computeCount);
    }

    [Fact]
    public void UseMemo_Recomputes_WhenDepChanges()
    {
        int computeCount = 0;
        string dep = "a";

        UINode Comp(Props _)
        {
            UseMemo(() => { computeCount++; return dep.Length; }, new object[] { dep });
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);
        Assert.Equal(1, computeCount);

        dep = "ab";
        reconciler.Update(root, forceReconcile: true);
        Assert.Equal(2, computeCount);
    }

    [Fact]
    public void UseMemo_ReturnsSameValue_WhenDepUnchanged()
    {
        object? captured1 = null;
        object? captured2 = null;

        UINode Comp(Props _)
        {
            var result = UseMemo(() => new object(), Array.Empty<object>());
            if (captured1 == null) captured1 = result;
            else                   captured2 = result;
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);
        reconciler.Update(root, forceReconcile: true);

        Assert.NotNull(captured1);
        Assert.Same(captured1, captured2);
    }

    // ── UseRef ────────────────────────────────────────────────────────────────

    [Fact]
    public void UseRef_PersistsAcrossRenders()
    {
        Ref<int>? ref1 = null;
        Ref<int>? ref2 = null;

        UINode Comp(Props _)
        {
            var r = UseRef(0);
            if (ref1 == null) ref1 = r;
            else              ref2 = r;
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);
        reconciler.Update(root, forceReconcile: true);

        Assert.Same(ref1, ref2);
    }

    [Fact]
    public void UseRef_MutationDoesNotTriggerRerender()
    {
        Ref<int>? refHandle = null;

        UINode Comp(Props _)
        {
            refHandle = UseRef(0);
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);

        refHandle!.Current = 99;

        Assert.False(reconciler.NeedsUpdate());
    }

    // ── UseReducer ────────────────────────────────────────────────────────────

    [Fact]
    public void UseReducer_Dispatch_UpdatesState()
    {
        Action<string>? dispatch = null;

        UINode Comp(Props _)
        {
            var (state, d) = UseReducer(
                (string s, string action) => action == "upper" ? s.ToUpper() : s,
                "hello");
            dispatch = d;
            return UI.Text(state);
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);
        Assert.Equal("hello", reconciler.Root!.Child!.Props.Text);

        dispatch!("upper");
        reconciler.Update(root);
        Assert.Equal("HELLO", reconciler.Root!.Child!.Props.Text);
    }

    // ── UseStable ─────────────────────────────────────────────────────────────

    [Fact]
    public void UseStable_ReturnsSameReference_AcrossRenders()
    {
        object? ref1 = null;
        object? ref2 = null;

        UINode Comp(Props _)
        {
            var stable = UseStable(() => new object());
            if (ref1 == null) ref1 = stable;
            else              ref2 = stable;
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);
        reconciler.Update(root, forceReconcile: true);

        Assert.Same(ref1, ref2);
    }

    // ── UseCallback ───────────────────────────────────────────────────────────

    [Fact]
    public void UseCallback_EmptyDeps_ReturnsSameDelegate()
    {
        Action? d1 = null;
        Action? d2 = null;

        UINode Comp(Props _)
        {
            var cb = UseCallback(() => { }, Array.Empty<object>());
            if (d1 == null) d1 = cb;
            else            d2 = cb;
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        var root = UI.Component(Comp);
        reconciler.Mount(root);
        reconciler.Update(root, forceReconcile: true);

        Assert.Same(d1, d2);
    }

    [Fact]
    public void UseCallback_ChangedDep_ReturnsNewDelegate()
    {
        int renderCount = 0;
        Action? first = null;
        Action? last = null;
        Action<int>? setDep = null;

        UINode Comp(Props _)
        {
            var (dep, set, _) = UseState(0);
            setDep = set;
            // Use a capturing lambda so each render produces a distinct delegate instance
            int captured = dep;
            var cb = UseCallback(() => { int x = captured; }, new object[] { dep });
            renderCount++;
            if (renderCount == 1) first = cb;
            else                  last  = cb;
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        reconciler.Mount(UI.Component(Comp));
        setDep!(42);
        reconciler.Update(UI.Component(Comp));

        Assert.NotSame(first, last);
    }

    // ── UseContext ────────────────────────────────────────────────────────────

    [Fact]
    public void UseContext_ReturnsDefaultValue_WithoutProvider()
    {
        var ctx = Paper.Core.Context.PaperContext.Create("default");
        string? read = null;

        UINode Comp(Props _)
        {
            read = UseContext(ctx);
            return UI.Box();
        }

        new PaperReconciler().Mount(UI.Component(Comp));

        Assert.Equal("default", read);
    }

    [Fact]
    public void UseContext_ReturnsProvidedValue_InsideProvider()
    {
        var ctx = Paper.Core.Context.PaperContext.Create("default");
        string? read = null;

        UINode Consumer(Props _)
        {
            read = UseContext(ctx);
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        reconciler.Mount(ctx.Provider("injected", UI.Component(Consumer)));

        Assert.Equal("injected", read);
    }

    [Fact]
    public void UseContext_NestedProviders_InnerWins()
    {
        var ctx = Paper.Core.Context.PaperContext.Create("outer");
        string? read = null;

        UINode Consumer(Props _)
        {
            read = UseContext(ctx);
            return UI.Box();
        }

        var reconciler = new PaperReconciler();
        reconciler.Mount(
            ctx.Provider("outer",
                ctx.Provider("inner",
                    UI.Component(Consumer))));

        Assert.Equal("inner", read);
    }
}
