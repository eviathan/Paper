namespace Paper.Core.Threading
{
    /// <summary>
    /// Runs CPU-bound work on the thread pool and automatically posts the result back to the
    /// UI thread via <see cref="UiThread.Post"/> when done.
    ///
    /// This is the correct pattern for audio file decode, waveform pixel generation, FFT
    /// analysis, and any other heavyweight off-thread work that needs to update Paper state.
    ///
    /// Usage:
    /// <code>
    ///   BackgroundQueue.Run(
    ///       work: () => GenerateWaveformPixels(clip),
    ///       onComplete: pixels => setPixels(pixels));
    /// </code>
    ///
    /// The <paramref name="onComplete"/> callback runs on the UI thread before the next frame, so
    /// it can safely call Paper hooks such as <c>setState</c>.
    ///
    /// Exceptions thrown by <paramref name="work"/> are caught and written to
    /// <see cref="Console.Error"/>; <paramref name="onComplete"/> is not invoked on failure.
    /// </summary>
    public static class BackgroundQueue
    {
        /// <summary>
        /// Run <paramref name="work"/> on the thread pool, then post <paramref name="onComplete"/>
        /// (with the result) to the UI thread.
        /// </summary>
        public static void Run<T>(Func<T> work, Action<T> onComplete)
        {
            Task.Run(() =>
            {
                T result;
                try
                {
                    result = work();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[Paper] BackgroundQueue.Run<T> error: " + ex);
                    return;
                }
                UiThread.Post(() => onComplete(result));
            });
        }

        /// <summary>
        /// Run <paramref name="work"/> on the thread pool, then post <paramref name="onComplete"/>
        /// to the UI thread when it finishes.
        /// </summary>
        public static void Run(Action work, Action onComplete)
        {
            Task.Run(() =>
            {
                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[Paper] BackgroundQueue.Run error: " + ex);
                    return;
                }
                UiThread.Post(onComplete);
            });
        }
    }
}
