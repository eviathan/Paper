using Paper.Core.VirtualDom;

namespace Paper.Core.Components
{
    /// <summary>
    /// Base class for Paper class-based components, analogous to React's <c>Component&lt;Props&gt;</c>.
    /// Subclass this and override <see cref="Render"/> to return the component's UI tree.
    /// </summary>
    /// <remarks>
    /// Hooks (<c>UseState</c>, <c>UseEffect</c>, etc.) are available inside <see cref="Render"/>
    /// via <see cref="Hooks.Hooks"/> — they are wired to the current reconciler fiber automatically.
    /// </remarks>
    public abstract class Component
    {
        /// <summary>Props passed to this component by its parent.</summary>
        public Props Props { get; internal set; } = Props.Empty;

        /// <summary>
        /// Determines whether the component should update when its props change.
        /// Return false to prevent re-rendering.
        /// </summary>
        /// <param name="nextProps">The new props that would be passed to the component</param>
        /// <returns>True if the component should update, false otherwise</returns>
        public virtual bool ShouldComponentUpdate(Props nextProps) => true;

        /// <summary>
        /// Render the component. Return the UI subtree this component produces.
        /// Called by the reconciler on every render triggered by state changes.
        /// </summary>
        public abstract UINode Render();

        // ── Hook forwarding ───────────────────────────────────────────────────
        // Expose hook methods as protected so class components can call them
        // without needing to import the Hooks namespace.

        protected (T value, Action<T> setState, Action<Func<T, T>> updateState) UseState<T>(T initial) =>
            Hooks.Hooks.UseState(initial);

        protected void UseEffect(Func<Action?> effect, object[]? deps = null) =>
            Hooks.Hooks.UseEffect(effect, deps);

        protected T UseMemo<T>(Func<T> factory, object[]? deps = null) =>
            Hooks.Hooks.UseMemo(factory, deps);

        protected Hooks.Ref<T> UseRef<T>(T initial) =>
            Hooks.Hooks.UseRef(initial);
    }
}
