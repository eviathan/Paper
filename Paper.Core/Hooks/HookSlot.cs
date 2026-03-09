using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Paper.Core.Hooks
{
    /// <summary>
    /// Per-slot hook state stored on a reconciler fiber.
    /// </summary>
    internal sealed class HookSlot
    {
        public object? State { get; set; }
        public object?[] Deps { get; set; } = Array.Empty<object?>();
        public bool HasDeps { get; set; }
        /// <summary>For UseEffect: the cleanup action returned by the previous effect.</summary>
        public Action? Cleanup { get; set; }
        /// <summary>For UseEffect: the effect to run after commit.</summary>
        public Func<Action?>? PendingEffect { get; set; }

        /// <summary>Queued state updaters (e.g. from rapid events) applied in order when state is read during the next render.</summary>
        private List<Func<object?, object?>>? _pendingStateUpdaters;

        internal bool HasPendingUpdaters => _pendingStateUpdaters != null && _pendingStateUpdaters.Count > 0;

        internal void EnqueueStateUpdater(Func<object?, object?> updater)
        {
            _pendingStateUpdaters ??= new List<Func<object?, object?>>();
            _pendingStateUpdaters.Add(updater);
        }

        /// <summary>Apply all queued state updaters and clear the queue. Call when reading state at start of render.</summary>
        internal void DrainStateUpdaters()
        {
            if (_pendingStateUpdaters == null || _pendingStateUpdaters.Count == 0)
                return;

            foreach (var u in _pendingStateUpdaters)
                State = u(State);
                
            _pendingStateUpdaters.Clear();
        }
    }
}