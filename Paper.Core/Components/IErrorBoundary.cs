using Paper.Core.VirtualDom;

namespace Paper.Core.Components
{
    /// <summary>
    /// Implement this interface on a class component to make it an error boundary.
    /// When any descendant throws during reconciliation, <see cref="RenderFallback"/> is called
    /// instead of crashing the whole tree. The boundary can recover by calling <c>setState</c>
    /// to clear its error state and re-render normally on the next update.
    /// </summary>
    /// <example><code>
    /// public class SafePanel : Component, IErrorBoundary
    /// {
    ///     public UINode RenderFallback(Exception error)
    ///         => UI.Box(style: new { background = "#fee2e2", padding = 12 },
    ///                   children: [UI.Text($"Something went wrong: {error.Message}")]);
    ///
    ///     public override UINode Render(Props props)
    ///         => UI.Box(children: props.Children.ToArray());
    /// }
    /// </code></example>
    public interface IErrorBoundary
    {
        /// <summary>
        /// Called when a descendant component throws during rendering.
        /// Return a fallback UI to display in place of the failed subtree.
        /// </summary>
        UINode RenderFallback(Exception error);
    }
}
