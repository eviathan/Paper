using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paper.Core.Reconciler;

namespace Paper.Core.Hooks
{
    /// <summary>
    /// Per-slot hook state stored on a reconciler fiber.
    /// </summary>
    internal sealed class HookSlot
    {
        /// <summary>The fiber that owns this slot; set by HookContext.Next() during rendering.</summary>
        internal Fiber? OwnerFiber { get; set; }

        public object? State { get; set; }
        public object?[] Deps { get; set; } = Array.Empty<object?>();
        public bool HasDeps { get; set; }
        /// <summary>For UseEffect: the cleanup action returned by the previous effect.</summary>
        public Action? Cleanup { get; set; }
        /// <summary>For UseEffect: the effect to run after commit.</summary>
        public Func<Action?>? PendingEffect { get; set; }
        /// <summary>For UseLayoutEffect: runs synchronously after reconcile, before paint.</summary>
        public Func<Action?>? PendingLayoutEffect { get; set; }

        /// <summary>
        /// Queued state updaters applied in order when state is read during the next render.
        /// Guarded by <see cref="_updatersLock"/> so background threads (e.g. UseAsync) can enqueue safely.
        /// </summary>
        private List<Func<object?, object?>>? _pendingStateUpdaters;
        private readonly object _updatersLock = new();

        internal bool HasPendingUpdaters
        {
            get { lock (_updatersLock) return _pendingStateUpdaters != null && _pendingStateUpdaters.Count > 0; }
        }

        internal void EnqueueStateUpdater(Func<object?, object?> updater)
        {
            lock (_updatersLock)
            {
                _pendingStateUpdaters ??= new List<Func<object?, object?>>();
                _pendingStateUpdaters.Add(updater);
            }
            // Mark all ancestors so the reconciler knows to traverse down to this dirty component.
            var ancestor = OwnerFiber?.Parent;
            while (ancestor != null)
            {
                ancestor.HasDirtyDescendant = true;
                ancestor = ancestor.Parent;
            }
            // Wake the render loop so the reconciler actually runs this frame.
            RenderScheduler.RequestRender();
        }

        /// <summary>Apply all queued state updaters and clear the queue. Call when reading state at start of render.</summary>
        internal void DrainStateUpdaters()
        {
            List<Func<object?, object?>>? snapshot;
            lock (_updatersLock)
            {
                if (_pendingStateUpdaters == null || _pendingStateUpdaters.Count == 0) return;
                snapshot = new List<Func<object?, object?>>(_pendingStateUpdaters);
                _pendingStateUpdaters.Clear();
            }
            foreach (var u in snapshot)
                State = u(State);
        }
    }
}