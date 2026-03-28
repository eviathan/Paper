namespace Paper.Core.Hooks
{
    /// <summary>
    /// Thread-local context tracking which fiber is currently rendering and which hook slot
    /// index we are on. Set by the reconciler before calling a component's Render/function.
    /// </summary>
    internal static class HookContext
    {
        [ThreadStatic]
        private static List<HookSlot>? _slots;

        [ThreadStatic]
        private static int _index;

        [ThreadStatic]
        internal static Reconciler.Fiber? CurrentFiber;

        /// <summary>Effects collected during the current render pass.</summary>
        [ThreadStatic]
        private static List<(int slot, Func<Action?> effect, object[]? deps)>? _pendingEffects;

        /// <summary>Layout effects collected during the current render pass (run before paint).</summary>
        [ThreadStatic]
        private static List<(int slot, Func<Action?> effect, object[]? deps)>? _pendingLayoutEffects;

        internal static void Begin(List<HookSlot> slots, Reconciler.Fiber fiber)
        {
            _slots = slots;
            _index = 0;
            _pendingEffects = [];
            _pendingLayoutEffects = [];
            CurrentFiber = fiber;
        }

        internal static void End() { _slots = null; _index = 0; CurrentFiber = null; }

        internal static int CurrentIndex => _index;

        internal static HookSlot Next()
        {
            if (_slots == null)
                throw new InvalidOperationException(
                    "Hooks can only be called during component rendering.");

            while (_slots.Count <= _index)
                _slots.Add(new HookSlot());

            var slot = _slots[_index++];
            slot.OwnerFiber = CurrentFiber;
            return slot;
        }

        internal static List<(int slot, Func<Action?> effect, object[]? deps)> PendingEffects =>
            _pendingEffects ?? [];

        internal static List<(int slot, Func<Action?> effect, object[]? deps)> PendingLayoutEffects =>
            _pendingLayoutEffects ?? [];

        internal static void EnqueueEffect(int slot, Func<Action?> effect, object[]? deps)
        {
            _pendingEffects ??= [];
            _pendingEffects.Add((slot, effect, deps));
        }

        internal static void EnqueueLayoutEffect(int slot, Func<Action?> effect, object[]? deps)
        {
            _pendingLayoutEffects ??= [];
            _pendingLayoutEffects.Add((slot, effect, deps));
        }

        internal static bool IsRendering => _slots != null;
    }
}
