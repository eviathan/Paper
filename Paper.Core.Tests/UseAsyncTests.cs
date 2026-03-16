using Paper.Core.Hooks;
using Paper.Core.VirtualDom;
using Xunit;
using static Paper.Core.Hooks.Hooks;
using R = Paper.Core.Reconciler.Reconciler;

namespace Paper.Core.Tests;

/// <summary>
/// Tests for the UseAsync hook.
/// UseAsync fires Task.Run internally; tests complete the TCS explicitly then
/// call rec.Update(root) to pick up the enqueued state change.
/// </summary>
[Collection("Sequential")]
public sealed class UseAsyncTests
{
    // ── AsyncState static factories ───────────────────────────────────────────

    [Fact]
    public void AsyncState_Loading_CorrectFlags()
    {
        var s = AsyncState<int>.Loading();
        Assert.True(s.IsLoading);
        Assert.False(s.IsSuccess);
        Assert.False(s.IsError);
        Assert.Null(s.Error);
    }

    [Fact]
    public void AsyncState_Success_CorrectFlags()
    {
        var s = AsyncState<int>.Success(99);
        Assert.False(s.IsLoading);
        Assert.True(s.IsSuccess);
        Assert.False(s.IsError);
        Assert.Equal(99, s.Value);
        Assert.Null(s.Error);
    }

    [Fact]
    public void AsyncState_Failure_CorrectFlags()
    {
        var ex = new InvalidOperationException("boom");
        var s = AsyncState<int>.Failure(ex);
        Assert.False(s.IsLoading);
        Assert.False(s.IsSuccess);
        Assert.True(s.IsError);
        Assert.Same(ex, s.Error);
        Assert.Equal(default, s.Value);
    }

    // ── UseAsync hook behaviour ───────────────────────────────────────────────

    [Fact]
    public void UseAsync_InitialState_IsLoading()
    {
        // On first render the task hasn't completed, so state must be Loading.
        AsyncState<int>? state = null;
        var tcs = new TaskCompletionSource<int>();

        UINode Comp(Props _)
        {
            state = UseAsync(async ct => await tcs.Task.WaitAsync(ct), []);
            return UI.Box();
        }

        var rec = new R();
        rec.Mount(UI.Component(Comp));

        Assert.NotNull(state);
        Assert.True(state!.IsLoading, "Expected Loading on first render before task completes");

        tcs.TrySetCanceled(); // cleanup background task
    }

    [Fact]
    public async Task UseAsync_OnSuccess_TransitionsToSuccess()
    {
        // Empty deps → effect runs once on mount, never again.
        // After completing the TCS and waiting for Task.Run to finish, a manual
        // rec.Update picks up the enqueued Success state.
        AsyncState<int>? state = null;
        var tcs = new TaskCompletionSource<int>();

        UINode Comp(Props _)
        {
            state = UseAsync(async ct => await tcs.Task.WaitAsync(ct), []);
            return UI.Box();
        }

        var rec = new R();
        var root = UI.Component(Comp);
        rec.Mount(root);
        Assert.True(state!.IsLoading);

        tcs.SetResult(42);
        await Task.Delay(100); // let Task.Run enqueue Success state

        rec.Update(root);

        Assert.True(state!.IsSuccess, $"Expected IsSuccess. IsLoading={state.IsLoading}, IsError={state.IsError}, Error={state.Error?.Message}");
        Assert.Equal(42, state.Value);
        Assert.False(state.IsLoading);
        Assert.Null(state.Error);
    }

    [Fact]
    public async Task UseAsync_OnFailure_TransitionsToError()
    {
        AsyncState<string>? state = null;
        var tcs = new TaskCompletionSource<string>();

        UINode Comp(Props _)
        {
            state = UseAsync(async ct => await tcs.Task.WaitAsync(ct), []);
            return UI.Box();
        }

        var rec = new R();
        var root = UI.Component(Comp);
        rec.Mount(root);
        Assert.True(state!.IsLoading);

        var ex = new InvalidOperationException("fetch failed");
        tcs.SetException(ex);
        await Task.Delay(100);

        rec.Update(root);

        Assert.True(state!.IsError, $"Expected IsError. IsLoading={state.IsLoading}, IsSuccess={state.IsSuccess}");
        Assert.Equal("fetch failed", state.Error!.Message);
        Assert.False(state.IsLoading);
        Assert.Null(state.Value);
    }

    [Fact]
    public async Task UseAsync_EmptyDeps_TaskRunsOnlyOnce()
    {
        // With empty deps the effect is a mount-only effect — re-renders must not re-invoke the fetcher.
        int callCount = 0;

        UINode Comp(Props _)
        {
            UseAsync(async _ =>
            {
                Interlocked.Increment(ref callCount);
                await Task.CompletedTask;
                return callCount;
            }, []); // mount-only
            return UI.Box();
        }

        var rec = new R();
        var root = UI.Component(Comp);
        rec.Mount(root);

        await Task.Delay(50); // let the task complete
        rec.Update(root);
        rec.Update(root);
        await Task.Delay(50); // give any spurious re-runs time to show up

        Assert.Equal(1, callCount); // fetcher called exactly once
    }

    [Fact]
    public async Task UseAsync_DepChange_RerunsWithNewDep()
    {
        // When a dep changes (via UseState so the reconciler knows to re-render),
        // the effect re-runs and the new result is picked up.
        AsyncState<int>? state = null;
        Action<int>? setDep = null;

        UINode Comp(Props _)
        {
            var (dep, setD, _) = UseState(1);
            setDep = setD;
            int capturedDep = dep;
            state = UseAsync(async _ =>
            {
                await Task.CompletedTask;
                return capturedDep;
            }, [dep]);
            return UI.Box();
        }

        var rec = new R();
        var root = UI.Component(Comp);
        rec.Mount(root);

        await Task.Delay(100); // let dep=1 fetch complete
        rec.Update(root);      // drain Success(1) state
        Assert.Equal(1, state!.Value);

        // Change dep through state — marks fiber dirty so reconciler re-renders
        setDep!(2);
        rec.Update(root); // re-renders with dep=2, starts new fetch

        await Task.Delay(100); // let dep=2 fetch complete
        rec.Update(root);      // drain Loading() + Success(2)

        Assert.True(state!.IsSuccess, $"Expected IsSuccess. IsLoading={state.IsLoading}, IsError={state.IsError}");
        Assert.Equal(2, state.Value);
    }

    [Fact]
    public async Task UseAsync_OldFetchResultDiscarded_AfterCancellation()
    {
        // If a slow first fetch completes after a dep change (which cancelled its CTS),
        // the result should NOT overwrite the Success(2) state.
        AsyncState<int>? state = null;
        Action<int>? setDep = null;
        var slowFetchCanProceed = new TaskCompletionSource<bool>();

        UINode Comp(Props _)
        {
            var (dep, setD, _) = UseState(1);
            setDep = setD;
            int capturedDep = dep;
            state = UseAsync(async ct =>
            {
                if (capturedDep == 1)
                    await slowFetchCanProceed.Task.WaitAsync(ct); // blocks — cancelled before completing
                return capturedDep;
            }, [dep]);
            return UI.Box();
        }

        var rec = new R();
        var root = UI.Component(Comp);
        rec.Mount(root); // starts dep=1 fetch (blocked)

        // Change dep before first fetch can finish → old CTS is cancelled
        setDep!(2);
        rec.Update(root); // re-renders with dep=2, cancels dep=1 fetch, starts dep=2 fetch

        await Task.Delay(100); // let dep=2 fetch (immediate) complete
        rec.Update(root);      // picks up Success(2)

        Assert.True(state!.IsSuccess, $"Expected Success(2). IsLoading={state.IsLoading}, IsError={state.IsError}");
        Assert.Equal(2, state.Value);

        // Allow the blocked dep=1 fetch to proceed — its result must be discarded
        // because the CTS was cancelled (cts.IsCancellationRequested check in UseAsync)
        slowFetchCanProceed.TrySetResult(true); // signal, but the CTS is already cancelled
        await Task.Delay(100);
        rec.Update(root);

        Assert.Equal(2, state!.Value); // still 2 — dep=1 result was discarded
    }
}
