using Paper.Core.Context;
using Paper.Core.Enums;
using Paper.Core.Hooks;
using Paper.Core.VirtualDom;

namespace Paper.Core.Reconciler
{
    /// <summary>The phase of the reconciler lifecycle in which an error occurred.</summary>
    public enum ReconcilerErrorPhase { Mount, Update, Effect }

    /// <summary>Structured error information delivered via <see cref="Reconciler.OnError"/>.</summary>
    public readonly struct ReconcilerError
    {
        /// <summary>The exception that was thrown.</summary>
        public Exception Exception { get; init; }
        /// <summary>Which lifecycle phase the error occurred in.</summary>
        public ReconcilerErrorPhase Phase { get; init; }
        /// <summary>
        /// True when the error was caught by an <see cref="IErrorBoundary"/> component;
        /// false when it reached the top-level fallback.
        /// </summary>
        public bool IsBoundary { get; init; }
    }

    public sealed class Reconciler : IDisposable
    {
        private Fiber? _current;
        private bool   _renderRequested;
        private readonly List<Fiber> _pendingDeletions = new();
        private readonly Action _requestRender;

        public Fiber? Root => _current;

        /// <summary>
        /// Fibers collected from <see cref="ElementTypes.Portal"/> elements during reconciliation.
        /// The renderer flushes these after the main tree pass so portals always appear on top.
        /// Reset at the start of each <see cref="Update"/> / <see cref="Mount"/> call.
        /// </summary>
        public List<Fiber> PortalRoots { get; } = new();

        public event Action? AfterCommit;

        /// <summary>
        /// Raised whenever the reconciler catches an exception. Subscribe to receive structured
        /// error information for logging, telemetry, or custom crash UI.
        /// The default <see cref="Console.Error"/> output is preserved regardless of subscribers.
        /// </summary>
        public event Action<ReconcilerError>? OnError;

        public Reconciler()
        {
            _requestRender = () => _renderRequested = true;
            RenderScheduler.AddListener(_requestRender);
        }

        public void Dispose()
        {
            RenderScheduler.RemoveListener(_requestRender);
        }

        public void Mount(UINode root)
        {
            PortalRoots.Clear();
            _pendingDeletions.Clear();
            try
            {
                _current = Render(root, null, null);
                FlushLayoutEffects(_current);
                FlushEffects(_current);
                AfterCommit?.Invoke();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(new ReconcilerError { Exception = ex, Phase = ReconcilerErrorPhase.Mount, IsBoundary = false });
                Console.Error.WriteLine("[Paper] Mount error: " + ex.ToString());
                var errorProps = new Props(new Dictionary<string, object?> { { "text", $"Error: {ex.Message}" } });
                var errorNode = new UINode("Text", errorProps);
                _current = Render(errorNode, null, null);
                AfterCommit?.Invoke();
            }
        }

        /// <param name="forceReconcile">When true, always re-run all components (e.g. for hot reload).</param>
        public void Update(UINode root, bool forceReconcile = false)
        {
            PortalRoots.Clear();
            _pendingDeletions.Clear();
            try
            {
                var wip = Reconcile(_current, root, null, forceReconcile);
                Commit(wip);
                CommitDeletions();
                _current = wip;
                FlushLayoutEffects(_current);
                FlushEffects(_current);
                AfterCommit?.Invoke();
                _renderRequested = false;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(new ReconcilerError { Exception = ex, Phase = ReconcilerErrorPhase.Update, IsBoundary = false });
                Console.Error.WriteLine("[Paper] Update error: " + ex.ToString());
                var errorProps = new Props(new Dictionary<string, object?> { { "text", $"Error: {ex.Message}" } });
                var errorNode = new UINode("Text", errorProps);
                var errorFiber = Render(errorNode, null, null);
                Commit(errorFiber);
                _current = errorFiber;
                AfterCommit?.Invoke();
                _renderRequested = false;
            }
        }

        public bool NeedsUpdate() => _renderRequested;

        private Fiber Render(UINode node, Fiber? current, Fiber? parent)
        {
            var fiber = new Fiber
            {
                Type = node.Type,
                Key = node.Key,
                Props = node.Props,
                Parent = parent,
                Alternate = current,
                EffectTag = current == null ? EffectTag.Placement : EffectTag.Update,
            };

            if (current != null)
            {
                foreach (var slot in current.HookSlots)
                    fiber.HookSlots.Add(slot);

                // Transfer the class-component instance so RenderClassComponent can reuse it
                // rather than creating a new one on every update.
                fiber.Instance = current.Instance;
            }

            // Mark class components that implement IErrorBoundary.
            if (node.Type is Type t && typeof(Components.IErrorBoundary).IsAssignableFrom(t))
                fiber.IsErrorBoundary = true;

            // Context providers push their value before expanding children so that any
            // UseContext calls inside function components see the correct value.
            ContextProviderBase? provider = node.Type as ContextProviderBase;
            if (provider != null)
            {
                // When the context value changes, force all existing descendants to re-reconcile
                // so UseContext consumers get the new value. Paper has no subscriber model, so
                // this subtree-dirty mark is the only propagation mechanism.
                if (current?.Type is ContextProviderBase prevProvider && provider.HasValueChanged(prevProvider))
                    MarkSubtreeDirty(current.Child);
                provider.Push();
            }
            try
            {
                var children = ExpandNode(node, fiber);

                foreach (var (slotIndex, effect, deps) in HookContext.PendingEffects)
                {
                    if (slotIndex < fiber.HookSlots.Count)
                    {
                        fiber.HookSlots[slotIndex].PendingEffect = () =>
                        {
                            var result = effect();
                            return result;
                        };
                    }
                }

                foreach (var (slotIndex, effect, deps) in HookContext.PendingLayoutEffects)
                {
                    if (slotIndex < fiber.HookSlots.Count)
                    {
                        fiber.HookSlots[slotIndex].PendingLayoutEffect = () =>
                        {
                            var result = effect();
                            return result;
                        };
                    }
                }

                ReconcileChildren(fiber, children, current);
            }
            finally
            {
                provider?.Pop();
            }

            return fiber;
        }

        private List<UINode> ExpandNode(UINode node, Fiber fiber)
        {
            // Errors propagate up — caught by the nearest error boundary's ReconcileChildren,
            // or by the top-level Mount/Update catch.

            if (node.Type is string s2 && s2 == ElementTypes.Portal)
            {
                // Portal: reconcile children normally but attach fibers to PortalRoots so the
                // renderer can flush them in a separate top-most pass.
                foreach (var child in node.Children)
                {
                    var portalFiber = Render(child, null, null);
                    PortalRoots.Add(portalFiber);
                }
                return new List<UINode>(); // portal itself has no layout children
            }

            return node.Type switch
            {
                string s when s == ElementTypes.RadioGroup => ExpandRadioGroup(node),
                string => node.Children.ToList(),
                Type t when typeof(Components.Component).IsAssignableFrom(t)
                    => new List<UINode> { RenderClassComponent(t, node.Props, fiber) },
                Func<Props, UINode> fn
                    => new List<UINode> { RenderFunctionComponent(fn, node.Props, fiber) },
                _ => node.Children.ToList(),
            };
        }

        private static List<UINode> ExpandRadioGroup(UINode node)
        {
            var options = node.Props.Options;
            if (options == null || options.Count == 0)
                return new List<UINode>();
            var selectedValue = node.Props.SelectedValue ?? "";
            var onSelect = node.Props.OnSelect;
            var list = new List<UINode>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                var (value, label) = options[i];
                var optValue = value;
                var propsBuilder = new PropsBuilder()
                    .Set("value", optValue)
                    .Text(label)
                    .Set("checked", selectedValue == optValue);
                if (onSelect != null)
                    propsBuilder.OnClick(() => onSelect(optValue));
                list.Add(new UINode(ElementTypes.RadioOption, propsBuilder.Build(), $"${i}"));
            }
            return list;
        }

        private UINode RenderClassComponent(Type type, Props props, Fiber fiber)
        {
            Components.Component instance;
            if (fiber.Instance != null && fiber.Instance.GetType() == type)
            {
                instance = fiber.Instance;
            }
            else
            {
                instance = (Components.Component)Activator.CreateInstance(type)!;
                fiber.Instance = instance;
            }
            instance.Props = props;

            HookContext.Begin(fiber.HookSlots, fiber);
            try
            {
                var result = instance.Render();
                HookContext.End();
                return result;
            }
            catch
            {
                HookContext.End();
                throw;
            }
        }

        private UINode RenderFunctionComponent(Func<Props, UINode> fn, Props props, Fiber fiber)
        {
            HookContext.Begin(fiber.HookSlots, fiber);
            try
            {
                var result = fn(props);
                HookContext.End();
                return result;
            }
            catch
            {
                // Ensure HookContext is cleaned up before the exception propagates to the boundary.
                HookContext.End();
                throw;
            }
        }

        private Fiber Reconcile(Fiber? current, UINode node, Fiber? parent, bool forceReconcile = false)
        {
            if (current != null && IsSameType(current, node))
            {
                // Snapshot and clear the dirty-descendant flag before deciding.
                bool hadDirtyDescendant = current.HasDirtyDescendant;
                current.HasDirtyDescendant = false;

                if (ShouldSkipReconciliation(current, node, forceReconcile, hadDirtyDescendant))
                {
                    current.EffectTag = EffectTag.None;
                    current.Parent = parent; // keep Parent in sync when reusing a fiber under a new parent
                    return current;
                }

                return Render(node, current, parent);
            }
            else
            {
                if (current != null)
                {
                    current.EffectTag = EffectTag.Deletion;
                    _pendingDeletions.Add(current);
                }

                return Render(node, null, parent);
            }
        }

        /// <summary>
        /// Marks an entire fiber subtree as having dirty descendants so that
        /// <see cref="ShouldSkipReconciliation"/> will force re-render on the next pass.
        /// Called when a ContextProvider's value changes.
        /// </summary>
        private static void MarkSubtreeDirty(Fiber? fiber)
        {
            while (fiber != null)
            {
                fiber.HasDirtyDescendant = true;
                MarkSubtreeDirty(fiber.Child);
                fiber = fiber.Sibling;
            }
        }

        private bool ShouldSkipReconciliation(Fiber current, UINode node, bool forceReconcile, bool hadDirtyDescendant)
        {
            if (forceReconcile) return false;

            if (current.Instance is Components.Component component)
            {
                if (!component.ShouldComponentUpdate(node.Props))
                    return true;
            }

            if (current.Type is Func<Props, UINode>)
            {
                bool hasPendingState = current.HookSlots.Any(s => s.HasPendingUpdaters);
                if (!hasPendingState && !hadDirtyDescendant && ShallowEqual(current.Props, node.Props))
                    return true;
                return false;
            }

            // Intrinsic elements (Box, Text, etc.): skip if props are unchanged AND no dirty descendant.
            if (current.Type is string)
                return !hadDirtyDescendant && ShallowEqual(current.Props, node.Props);

            return false;
        }

        private bool ShallowEqual(Props prevProps, Props nextProps)
        {
            var prevDict = prevProps.All;
            var nextDict = nextProps.All;

            if (prevDict.Count != nextDict.Count)
                return false;

            foreach (var kvp in prevDict)
            {
                if (!nextDict.TryGetValue(kvp.Key, out var nextValue))
                    return false;

                // Children are a UINode[] — compare by reference since codegen creates fresh arrays.
                // Use ReferenceEquals for arrays; for everything else use structural equality.
                if (kvp.Value is System.Array arr && nextValue is System.Array nextArr)
                {
                    if (!ReferenceEquals(arr, nextArr)) return false;
                }
                else if (!Equals(kvp.Value, nextValue))
                    return false;
            }

            return true;
        }

        private void ReconcileChildren(Fiber parent, List<UINode> newChildren, Fiber? currentFiber)
        {
            var oldChildren = FlattenChildren(currentFiber);
            var keyedOld = BuildKeyedMap(oldChildren);

            Fiber? prevSibling = null;
            int index = 0;

            foreach (var childNode in newChildren)
            {
                string lookupKey = childNode.Key ?? $"${index}";
                Fiber? oldChild = keyedOld.TryGetValue(lookupKey, out var found) ? found : null;

                Fiber newFiber;
                if (parent.IsErrorBoundary)
                {
                    try
                    {
                        newFiber = Reconcile(oldChild, childNode, parent);
                        parent.CaughtError = null; // subtree rendered successfully; clear any prior error
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(new ReconcilerError { Exception = ex, Phase = ReconcilerErrorPhase.Update, IsBoundary = true });
                        Console.Error.WriteLine("[Paper] Error boundary caught: " + ex.ToString());
                        parent.CaughtError = ex;
                        // Render the boundary's fallback as its only child; discard remaining siblings.
                        var fallback = ((Components.IErrorBoundary)parent.Instance!).RenderFallback(ex);
                        parent.Child    = null;
                        prevSibling     = null;
                        newFiber        = Render(fallback, null, parent);
                        newFiber.Index  = 0;
                        parent.Child    = newFiber;
                        return; // skip remaining children
                    }
                }
                else
                {
                    newFiber = Reconcile(oldChild, childNode, parent);
                }

                newFiber.Index = index;

                if (prevSibling == null)
                    parent.Child = newFiber;
                else
                    prevSibling.Sibling = newFiber;

                prevSibling = newFiber;
                index++;
            }

            // Terminate the sibling chain. A reused fiber (returned by ShouldSkipReconciliation)
            // retains its old Sibling pointer from the previous render. If the child order changed
            // (e.g. keyed reorder) the old Sibling may point back to another fiber in the new list,
            // creating a cycle that causes Commit/FlushEffects to loop forever.
            // When newChildren is empty the loop body never runs and prevSibling stays null —
            // parent.Child must be explicitly cleared so stale children from the previous render
            // are not left reachable in the fiber tree (they would still have non-zero layout and
            // PointerEvents.Auto, causing hit-tests to land on invisible zones).
            if (prevSibling != null)
                prevSibling.Sibling = null;
            else
                parent.Child = null;

            var newKeySet = new HashSet<string>(
                newChildren.Select((n, i) => n.Key ?? $"${i}"));
            foreach (var kv in keyedOld)
            {
                if (!newKeySet.Contains(kv.Key))
                {
                    kv.Value.EffectTag = EffectTag.Deletion;
                    _pendingDeletions.Add(kv.Value);
                }
            }
        }

        private void Commit(Fiber? fiber)
        {
            if (fiber == null) return;

            if (fiber.EffectTag == EffectTag.Deletion)
            {
                UnmountFiber(fiber);
            }
            else
            {
                Commit(fiber.Child);
            }
            Commit(fiber.Sibling);
        }

        private void CommitDeletions()
        {
            foreach (var fiber in _pendingDeletions)
                UnmountFiber(fiber);
            _pendingDeletions.Clear();
        }

        private static void UnmountFiber(Fiber fiber)
        {
            foreach (var slot in fiber.HookSlots)
            {
                try { slot.Cleanup?.Invoke(); }
                catch (Exception ex) { Console.Error.WriteLine("[Paper] Cleanup error during unmount: " + ex); }
                slot.Cleanup = null;
            }

            var child = fiber.Child;
            while (child != null)
            {
                UnmountFiber(child);
                child = child.Sibling;
            }
        }

        private void FlushLayoutEffects(Fiber? fiber)
        {
            if (fiber == null) return;
            FlushLayoutEffects(fiber.Child);
            foreach (var slot in fiber.HookSlots)
            {
                if (slot.PendingLayoutEffect != null)
                {
                    try { slot.Cleanup?.Invoke(); }
                    catch (Exception ex) { Console.Error.WriteLine("[Paper] LayoutEffect cleanup error: " + ex); }
                    slot.Cleanup = null;
                    try
                    {
                        var cleanup = slot.PendingLayoutEffect();
                        slot.Cleanup = cleanup;
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(new ReconcilerError { Exception = ex, Phase = ReconcilerErrorPhase.Effect, IsBoundary = false });
                        Console.Error.WriteLine("[Paper] LayoutEffect error: " + ex.ToString());
                    }
                    slot.PendingLayoutEffect = null;
                }
            }
            FlushLayoutEffects(fiber.Sibling);
        }

        private void FlushEffects(Fiber? fiber)
        {
            if (fiber == null) return;

            FlushEffects(fiber.Child);
            
            foreach (var slot in fiber.HookSlots)
            {
                if (slot.PendingEffect != null)
                {
                    try { slot.Cleanup?.Invoke(); }
                    catch (Exception ex) { Console.Error.WriteLine("[Paper] Effect cleanup error: " + ex); }
                    slot.Cleanup = null;
                    try
                    {
                        var cleanup = slot.PendingEffect();
                        slot.Cleanup = cleanup;
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(new ReconcilerError { Exception = ex, Phase = ReconcilerErrorPhase.Effect, IsBoundary = false });
                        Console.Error.WriteLine("[Paper] Effect error: " + ex.ToString());
                    }
                    slot.PendingEffect = null;
                }
            }
            
            FlushEffects(fiber.Sibling);
        }

        private static bool IsSameType(Fiber fiber, UINode node)
        {
            // For function components, we need to compare the underlying method, not the delegate instance
            // because each render creates a new delegate instance even for the same function
            if (fiber.Type is Func<Props, UINode> fn && node.Type is Func<Props, UINode> fn2)
            {
                return fn.Target == fn2.Target && fn.Method == fn2.Method;
            }
            return fiber.Type.Equals(node.Type);
        }

        private static List<Fiber> FlattenChildren(Fiber? parent)
        {
            var list = new List<Fiber>();
            var child = parent?.Child;
            while (child != null)
            {
                list.Add(child);
                child = child.Sibling;
            }
            return list;
        }

        private static Dictionary<string, Fiber> BuildKeyedMap(List<Fiber> children)
        {
            var map = new Dictionary<string, Fiber>();
            for (int i = 0; i < children.Count; i++)
            {
                var key = children[i].Key ?? $"${i}";
                map[key] = children[i];
            }
            return map;
        }
    }
}