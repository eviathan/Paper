using Paper.Core.Context;

namespace Paper.Core.Hooks
{
    /// <summary>
    /// Paper hook API — call these inside function components or <see cref="Components.Component.Render"/>.
    /// Rules (same as React):
    /// <list type="bullet">
    ///   <item>Only call hooks at the top level — not inside loops or conditionals.</item>
    ///   <item>Only call hooks during rendering.</item>
    /// </list>
    /// </summary>
    public static class Hooks
    {
        // ── UseState ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current state value and a setter. Calling the setter triggers a re-render.
        /// Pass a value to set state directly, or pass a function (e.g. <c>prev => prev + 1</c>) so rapid
        /// updates from events are applied in sequence and the UI stays correct when spamming.
        /// </summary>
        public static (T value, Action<T> setState, Action<Func<T, T>> updateState) UseState<T>(T initial)
        {
            var slot = HookContext.Next();

            slot.DrainStateUpdaters();

            if (slot.State is null)
                slot.State = initial;

            T current = slot.State is T typed ? typed : initial;

            void SetState(T next)
            {
                slot.EnqueueStateUpdater(_ => (object?)next);
                RenderScheduler.RequestRender();
            }

            void UpdateState(Func<T, T> updater)
            {
                T FromState(object? s) => s is T t ? t : default!;
                slot.EnqueueStateUpdater(s => (object?)updater(FromState(s)));
                RenderScheduler.RequestRender();
            }

            return (current, SetState, UpdateState);
        }

        // ── UseEffect ────────────────────────────────────────────────────────

        /// <summary>
        /// Schedules a side-effect to run after render.
        /// If <paramref name="deps"/> is null, runs after every render.
        /// If <paramref name="deps"/> is an empty array, runs only once (on mount).
        /// Otherwise, runs when any dep changes (reference equality).
        /// The <paramref name="effect"/> may return a cleanup <see cref="Action"/> that runs before
        /// the next effect or on unmount.
        /// </summary>
        public static void UseEffect(Func<Action?> effect, object[]? deps = null)
        {
            var slot = HookContext.Next();
            var slotIndex = HookContext.CurrentIndex - 1;
            bool shouldRun;

            if (!slot.HasDeps || deps == null)
            {
                shouldRun = true;
            }
            else
            {
                shouldRun = !DepsEqual(slot.Deps, deps);
            }

            if (shouldRun)
            {
                slot.HasDeps = true;
                slot.Deps = deps ?? Array.Empty<object?>();
                HookContext.EnqueueEffect(slotIndex, effect, deps);
            }
        }

        // ── UseLayoutEffect ───────────────────────────────────────────────────

        /// <summary>
        /// Like <see cref="UseEffect"/> but fires synchronously after reconciliation, before the
        /// next paint. Use for measure-then-reposition patterns that must not flicker.
        /// The <paramref name="effect"/> may return a cleanup <see cref="Action"/>.
        /// </summary>
        public static void UseLayoutEffect(Func<Action?> effect, object[]? deps = null)
        {
            var slot = HookContext.Next();
            var slotIndex = HookContext.CurrentIndex - 1;
            bool shouldRun;

            if (!slot.HasDeps || deps == null)
                shouldRun = true;
            else
                shouldRun = !DepsEqual(slot.Deps, deps);

            if (shouldRun)
            {
                slot.HasDeps = true;
                slot.Deps = deps ?? Array.Empty<object?>();
                HookContext.EnqueueLayoutEffect(slotIndex, effect, deps);
            }
        }

        // ── UseMemo ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a memoised value. Re-computes only when <paramref name="deps"/> change.
        /// </summary>
        public static T UseMemo<T>(Func<T> factory, object[]? deps = null)
        {
            var slot = HookContext.Next();

            bool shouldRecompute = !slot.HasDeps || deps == null || !DepsEqual(slot.Deps, deps!);

            if (shouldRecompute)
            {
                slot.State = factory();
                slot.HasDeps = true;
                slot.Deps = deps ?? Array.Empty<object?>();
            }

            return slot.State is T typed ? typed : factory();
        }

        // ── UseRef ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a stable <see cref="Ref{T}"/> that persists across renders.
        /// Mutating <see cref="Ref{T}.Current"/> does NOT trigger a re-render.
        /// </summary>
        public static Ref<T> UseRef<T>(T initial)
        {
            var slot = HookContext.Next();
            if (slot.State == null)
                slot.State = new Ref<T>(initial);
            return (Ref<T>)slot.State!;
        }

        // ── UseReducer ────────────────────────────────────────────────────────

        /// <summary>
        /// Manages state via a reducer function. Returns the current state and a dispatch function.
        /// Calling dispatch with an action runs <paramref name="reducer"/>(state, action) and triggers a re-render.
        /// </summary>
        public static (TState state, Action<TAction> dispatch) UseReducer<TState, TAction>(
            Func<TState, TAction, TState> reducer, TState initialState)
        {
            var slot = HookContext.Next();

            slot.DrainStateUpdaters();

            if (slot.State is null)
                slot.State = initialState;

            TState current = slot.State is TState typed ? typed : initialState;

            void Dispatch(TAction action)
            {
                slot.EnqueueStateUpdater(s =>
                {
                    TState prev = s is TState t ? t : initialState;
                    return (object?)reducer(prev, action);
                });
                RenderScheduler.RequestRender();
            }

            return (current, Dispatch);
        }

        // ── UseStable ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the same value every render, computing it only on the first render.
        /// Use for module-level objects (e.g. PaperContext, helper component delegates) that must
        /// retain a stable reference across re-renders so reconciliation can match fibers correctly.
        /// </summary>
        public static T UseStable<T>(Func<T> factory) => UseMemo(factory, Array.Empty<object>());

        // ── UseCallback ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns a stable delegate reference that only changes when <paramref name="deps"/> change.
        /// Equivalent to <c>UseMemo(() => callback, deps)</c>.
        /// </summary>
        public static TDelegate UseCallback<TDelegate>(TDelegate callback, object[]? deps = null)
            where TDelegate : Delegate =>
            UseMemo(() => callback, deps);

        // ── UseContext ────────────────────────────────────────────────────────

        /// <summary>
        /// Reads the current value from <paramref name="context"/>, provided by the nearest ancestor
        /// <c>context.Provider(value, ...)</c> node. Falls back to the context's default value.
        /// </summary>
        public static T UseContext<T>(PaperContext<T> context) => context.Current;

        // ── UseAsync ─────────────────────────────────────────────────────────

        /// <summary>
        /// Runs an async operation and returns its current state.
        /// The operation re-runs whenever <paramref name="deps"/> change (same rules as <see cref="UseEffect"/>).
        /// While the task is running, <see cref="AsyncState{T}.IsLoading"/> is true.
        /// When it completes (or throws), the component re-renders automatically.
        /// </summary>
        /// <example><code>
        /// var state = Hooks.UseAsync(async ct => await FetchUserAsync(userId), new object[] { userId });
        /// if (state.IsLoading) return (&lt;Spinner /&gt;);
        /// if (state.IsError)   return (&lt;Text&gt;{state.Error!.Message}&lt;/Text&gt;);
        /// return (&lt;Text&gt;{state.Value!.Name}&lt;/Text&gt;);
        /// </code></example>
        public static AsyncState<T> UseAsync<T>(
            Func<System.Threading.CancellationToken, Task<T>> fetcher,
            object[]? deps = null)
        {
            var stateSlot  = HookContext.Next(); // slot 0: AsyncState<T>
            var cancelSlot = HookContext.Next(); // slot 1: CancellationTokenSource

            stateSlot.DrainStateUpdaters();

            if (stateSlot.State is not AsyncState<T>)
                stateSlot.State = AsyncState<T>.Loading();

            var currentState = (AsyncState<T>)stateSlot.State;

            // Capture the slot reference for the async callback (must not capture `stateSlot` directly
            // as it may be a different object after re-renders; capture via closure over the local var).
            var capturedSlot = stateSlot;

            UseEffect(() =>
            {
                // Cancel any in-flight request from the previous deps run.
                (cancelSlot.State as System.Threading.CancellationTokenSource)?.Cancel();
                var cts = new System.Threading.CancellationTokenSource();
                cancelSlot.State = cts;

                capturedSlot.EnqueueStateUpdater(_ => AsyncState<T>.Loading());
                RenderScheduler.RequestRender();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await fetcher(cts.Token).ConfigureAwait(false);
                        if (!cts.IsCancellationRequested)
                        {
                            capturedSlot.EnqueueStateUpdater(_ => AsyncState<T>.Success(result));
                            RenderScheduler.RequestRender();
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        if (!cts.IsCancellationRequested)
                        {
                            capturedSlot.EnqueueStateUpdater(_ => AsyncState<T>.Failure(ex));
                            RenderScheduler.RequestRender();
                        }
                    }
                });

                return () => cts.Cancel();
            }, deps);

            return currentState;
        }

        // ── UseVirtualScroll ─────────────────────────────────────────────────

        /// <summary>
        /// Virtualizes a large list so only the visible slice is rendered each frame.
        /// Returns the visible items plus padding heights for the top/bottom spacers.
        /// <para>
        /// Usage: wrap the result in a fixed-height container with <c>overflow:'hidden'</c>
        /// and wire <see cref="VirtualScrollState{T}.OnWheel"/> to the container's <c>onWheel</c> prop.
        /// The spacer box total height is <see cref="VirtualScrollState{T}.TotalHeight"/>.
        /// </para>
        /// <example><code>
        /// var vs = Hooks.UseVirtualScroll(myItems, itemHeight: 48f, containerH: 400f);
        /// return (
        ///   &lt;Box style={{ height: 400, overflow: 'hidden' }} onWheel={vs.OnWheel}&gt;
        ///     &lt;Box style={{ height: vs.TotalHeight }}&gt;
        ///       &lt;Box style={{ paddingTop: vs.PaddingTop }}&gt;
        ///         {vs.VisibleItems.Select((item, i) => &lt;Row key={i} data={item} /&gt;)}
        ///       &lt;/Box&gt;
        ///     &lt;/Box&gt;
        ///   &lt;/Box&gt;
        /// );
        /// </code></example>
        /// </summary>
        /// <param name="items">The full item list (may have any length).</param>
        /// <param name="itemHeight">Fixed row height in pixels. For variable heights use the overload with <c>itemHeightProvider</c>.</param>
        /// <param name="containerH">Visible container height in pixels.</param>
        /// <param name="overscan">Extra rows rendered above and below the visible range to avoid pop-in during fast scrolling.</param>
        public static VirtualScrollState<T> UseVirtualScroll<T>(
            IReadOnlyList<T> items,
            float itemHeight,
            float containerH,
            int overscan = 3)
        {
            // Use UpdateState (not UseRef) so the VirtualList fiber is marked dirty and re-renders
            // when the scroll position changes. UseRef mutation bypasses per-component dirty tracking.
            var (scrollY, _, updateScrollY) = UseState(0f);

            float totalHeight = items.Count * itemHeight;
            float maxScroll   = Math.Max(0f, totalHeight - containerH);
            float scrollYClamped = Math.Clamp(scrollY, 0f, maxScroll);

            int firstVisible = (int)Math.Floor(scrollYClamped / itemHeight);
            int lastVisible  = (int)Math.Ceiling((scrollYClamped + containerH) / itemHeight);

            int start = Math.Max(0, firstVisible - overscan);
            int end   = Math.Min(items.Count - 1, lastVisible + overscan);

            var visible = new List<(int index, T item)>(end - start + 1);
            for (int i = start; i <= end; i++)
                visible.Add((i, items[i]));

            float paddingTop    = start * itemHeight;
            float paddingBottom = Math.Max(0f, (items.Count - 1 - end) * itemHeight);

            const float ScrollStep = 24f; // pixels per wheel notch — matches outer scroll containers
            void OnWheel(Events.PointerEvent e)
            {
                // WheelDeltaY > 0 = scroll up (see less content) — invert to get scroll offset direction
                updateScrollY(prev => Math.Clamp(prev - e.WheelDeltaY * ScrollStep, 0f, maxScroll));
            }

            return new VirtualScrollState<T>(visible, paddingTop, paddingBottom, totalHeight, OnWheel);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool DepsEqual(object?[] prev, object?[] next)
        {
            if (prev.Length != next.Length) return false;
            for (int i = 0; i < prev.Length; i++)
                if (!Equals(prev[i], next[i])) return false;
            return true;
        }
    }
}
