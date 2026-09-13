using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Paper.Core.Hooks;

namespace Paper.Core.Threading
{
    /// <summary>
    /// Provides a safe, ordered way for background threads to execute actions on the Paper UI thread.
    ///
    /// The UI thread is the thread that calls <c>Canvas.DoFrame()</c> / <c>Canvas.Run()</c>.
    /// All Paper state, reconciler, and rendering operations must run on that thread.
    ///
    /// Usage from a background thread (e.g. audio analysis, file I/O):
    /// <code>
    ///   // Fire and forget — action runs before the next reconcile.
    ///   UiThread.Post(() => setState(result));
    ///
    ///   // Block background thread until action runs.
    ///   UiThread.Send(() => setState(result));
    /// </code>
    ///
    /// <see cref="DrainQueue"/> is called automatically by <c>Canvas.DoFrame()</c> at the start of
    /// each frame; there is no need to call it from application code.
    /// </summary>
    public static class UiThread
    {
        private static readonly ConcurrentQueue<Action> _queue = new();
        private static volatile int _uiThreadId = -1;

        // ── Host API (called by Canvas) ───────────────────────────────────────

        /// <summary>
        /// Register the calling thread as the UI thread.
        /// Called once from <c>Canvas.OnWindowLoad()</c>.
        /// </summary>
        internal static void RegisterCurrentThread()
            => _uiThreadId = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// Dequeue and execute all pending actions.
        /// Called by <c>Canvas.DoFrame()</c> at the start of each frame before reconcile/render.
        /// </summary>
        internal static void DrainQueue()
        {
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[Paper] UiThread action threw: " + ex);
                }
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Schedule <paramref name="action"/> to run on the UI thread before the next frame.
        /// Safe to call from any thread at any time.
        /// Returns immediately; the action runs asynchronously.
        /// </summary>
        public static void Post(Action action)
        {
            _queue.Enqueue(action);
            RenderScheduler.RequestRender();
        }

        /// <summary>
        /// Run <paramref name="action"/> on the UI thread and block the calling thread until it
        /// completes.  If already on the UI thread the action is executed inline.
        /// Exceptions thrown by the action are re-thrown on the calling thread.
        /// </summary>
        public static void Send(Action action)
        {
            if (_uiThreadId >= 0 && Thread.CurrentThread.ManagedThreadId == _uiThreadId)
            {
                action();
                return;
            }

            Exception? caughtException = null;
            using var done = new ManualResetEventSlim(false);

            _queue.Enqueue(() =>
            {
                try   { action(); }
                catch (Exception ex) { caughtException = ex; }
                finally { done.Set(); }
            });

            RenderScheduler.RequestRender();
            done.Wait();

            if (caughtException != null)
                ExceptionDispatchInfo.Capture(caughtException).Throw();
        }

        /// <summary>
        /// Assert that the current call is being made on the UI thread.
        /// Compiled away in Release builds; throws <see cref="InvalidOperationException"/> in Debug.
        /// </summary>
        [Conditional("DEBUG")]
        public static void AssertOnUiThread()
        {
            if (_uiThreadId >= 0 && Thread.CurrentThread.ManagedThreadId != _uiThreadId)
                throw new InvalidOperationException(
                    $"[Paper] Expected UI thread (id={_uiThreadId}) but called from thread {Thread.CurrentThread.ManagedThreadId}.");
        }
    }
}
