namespace Paper.Rendering.Silk.NET
{
    // ── CanvasManager ─────────────────────────────────────────────────────────
    //
    // Drives multiple Canvas windows from a single main-thread event loop.
    // Required for multi-window dock support — GLFW / macOS demand all window
    // operations happen on the thread that called glfwInit (main thread).
    //
    // Usage:
    //
    //   var main = new Canvas("Editor", 1280, 720) { DisableVSync = true };
    //   main.DockSession = session;
    //   main.Mount(p => ...);
    //
    //   session.NewWindowRequested += (panelId, state, x, y) =>
    //   {
    //       var detached = new Canvas("Panel", 600, 400)
    //       {
    //           DisableVSync    = true,
    //           InitialPosition = new(x, y),
    //           DockSession     = session,
    //       };
    //       detached.Mount(p => DockContext.Root(panels, state, session: session, windowId: Guid.NewGuid().ToString("N")[..8]));
    //       CanvasManager.Add(detached);
    //   };
    //
    //   CanvasManager.Run(main);  // blocks until all windows closed
    //
    // ─────────────────────────────────────────────────────────────────────────

    public static class CanvasManager
    {
        private static readonly List<Canvas> _active  = new();
        private static readonly List<Canvas> _pending = new();

        /// <summary>
        /// Queue a canvas to be initialised and added to the shared loop on the next frame.
        /// Thread-safe: safe to call from within a <c>DockWindowSession.NewWindowRequested</c> handler.
        /// </summary>
        public static void Add(Canvas canvas) => _pending.Add(canvas);

        /// <summary>
        /// Run the shared main-thread event loop, driving <paramref name="primary"/> and any
        /// canvases added via <see cref="Add"/>. Blocks until all windows are closed.
        /// Disposes every canvas before returning.
        /// </summary>
        public static void Run(Canvas primary)
        {
            primary.InitializeWindow();
            _active.Add(primary);

            try
            {
                while (_active.Count > 0)
                {
                    // Initialise any windows queued since the last frame (e.g. from NewWindowRequested).
                    if (_pending.Count > 0)
                    {
                        Console.WriteLine($"[DockDbg] CanvasManager: initializing {_pending.Count} pending window(s)");
                        foreach (var pending in _pending)
                        {
                            pending.InitializeWindow();
                            _active.Add(pending);
                        }
                        _pending.Clear();
                    }

                    bool anyWantsRender = false;

                    for (int i = _active.Count - 1; i >= 0; i--)
                    {
                        var canvas = _active[i];
                        if (canvas.WantsRender) anyWantsRender = true;

                        bool alive = canvas.DoFrame();
                        if (!alive)
                        {
                            canvas.Dispose();
                            _active.RemoveAt(i);
                        }
                    }

                    // Yield CPU when no window needs a new frame.
                    if (!anyWantsRender && _pending.Count == 0)
                        Thread.Sleep(4);
                }
            }
            finally
            {
                foreach (var c in _active) c.Dispose();
                _active.Clear();
            }
        }
    }
}
