using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Paper.Core.Hooks
{
    /// <summary>
    /// Allows the reconciler to hook into state updates to schedule re-renders.
    /// Set by <see cref="Reconciler.Reconciler"/> on startup; host may wrap OnRenderRequested.
    /// </summary>
    public static class RenderScheduler
    {
        public static Action? OnRenderRequested;
        public static void RequestRender() => OnRenderRequested?.Invoke();
    }
}