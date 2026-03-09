using System.Runtime.CompilerServices;
using Paper.Core.VirtualDom;

namespace Paper.Core.Context
{
    /// <summary>
    /// Non-generic base so the reconciler can push/pop without knowing the type parameter.
    /// </summary>
    public abstract class ContextProviderBase
    {
        public abstract void Push();
        public abstract void Pop();
    }

    /// <summary>
    /// A Paper context — analogous to React.createContext.
    /// Create with <see cref="PaperContext.Create{T}"/>.
    /// </summary>
    public sealed class PaperContext<T>
    {
        private readonly T _defaultValue;
        private readonly Stack<T> _stack = new();

        internal PaperContext(T defaultValue) => _defaultValue = defaultValue;

        /// <summary>The current value provided by the nearest ancestor Provider, or the default.</summary>
        public T Current => _stack.Count > 0 ? _stack.Peek() : _defaultValue;

        internal void Push(T value) => _stack.Push(value);
        internal void Pop() { if (_stack.Count > 0) _stack.Pop(); }

        /// <summary>
        /// Creates a Provider node that supplies <paramref name="value"/> to all descendant consumers.
        /// Usage in CSX: <c>{MyCtx.Provider(value, child1, child2)}</c>
        /// </summary>
        public UINode Provider(T value, params UINode[] children)
        {
            var props = new Props(new Dictionary<string, object?>
            {
                { "children", (IReadOnlyList<UINode>)children },
            });
            return new UINode(new ContextProviderNode<T>(this, value), props);
        }

        private sealed class ContextProviderNode<TVal> : ContextProviderBase
        {
            private readonly PaperContext<TVal> _ctx;
            private readonly TVal _value;

            public ContextProviderNode(PaperContext<TVal> ctx, TVal value)
            {
                _ctx   = ctx;
                _value = value;
            }

            public override void Push() => _ctx.Push(_value);
            public override void Pop()  => _ctx.Pop();

            // Two provider nodes for the same context are the "same type" for reconciliation —
            // this ensures the fiber is updated (not replaced) when the value changes.
            public override bool Equals(object? obj) =>
                obj is ContextProviderNode<TVal> other && ReferenceEquals(_ctx, other._ctx);

            public override int GetHashCode() => RuntimeHelpers.GetHashCode(_ctx);
        }
    }

    /// <summary>Static factory — mirrors <c>React.createContext(defaultValue)</c>.</summary>
    public static class PaperContext
    {
        public static PaperContext<T> Create<T>(T defaultValue) => new(defaultValue);
    }
}
