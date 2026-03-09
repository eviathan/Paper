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
