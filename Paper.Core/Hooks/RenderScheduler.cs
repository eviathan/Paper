namespace Paper.Core.Hooks
{
    /// <summary>
    /// Allows reconcilers and host surfaces to subscribe to render-request notifications.
    /// Multiple subscribers are supported; each receives the notification independently.
    /// </summary>
    public static class RenderScheduler
    {
        private static Action? _handlers;

        public static void AddListener(Action handler) => _handlers += handler;
        public static void RemoveListener(Action handler) => _handlers -= handler;
        public static void RequestRender() => _handlers?.Invoke();
    }
}