using Paper.Core.Context;
using Paper.Core.Enums;
using Paper.Core.Hooks;
using Paper.Core.VirtualDom;

namespace Paper.Core.Reconciler
{
    public sealed class Reconciler
    {
        private Fiber? _current;     
        private bool   _renderRequested;

        public Fiber? Root => _current;

        public event Action? AfterCommit;

        public Reconciler()
        {
            RenderScheduler.OnRenderRequested = () => _renderRequested = true;
        }

        public void Mount(UINode root)
        {
            try
            {
                _current = Render(root, null, null);
                FlushEffects(_current);
                AfterCommit?.Invoke();
            }
            catch (Exception ex)
            {
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
            try
            {
                var wip = Reconcile(_current, root, null, forceReconcile);
                Commit(wip);
                _current = wip;
                FlushEffects(_current);
                AfterCommit?.Invoke();
                _renderRequested = false;
            }
            catch (Exception ex)
            {
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
            try
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
                    foreach (var slot in current.HookSlots)
                        fiber.HookSlots.Add(slot);

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

                // Context providers push their value before child reconciliation so that any
                // UseContext calls inside child component functions see the correct value.
                ContextProviderBase? provider = node.Type as ContextProviderBase;
                provider?.Push();
                try
                {
                    ReconcileChildren(fiber, children, current);
                }
                finally
                {
                    provider?.Pop();
                }

                return fiber;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Paper] Render error: " + ex.ToString());
                var errorProps = new Props(new Dictionary<string, object?> { { "text", $"Error: {ex.Message}" } });
                var errorNode = new UINode("Text", errorProps);
                return Render(errorNode, null, parent);
            }
        }

        private List<UINode> ExpandNode(UINode node, Fiber fiber)
        {
            try
            {
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
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Paper] ExpandNode error: " + ex.ToString());
                var errorProps = new Props(new Dictionary<string, object?> { { "text", $"Error: {ex.Message}" } });
                var errorNode = new UINode("Text", errorProps);
                return new List<UINode> { errorNode };
            }
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
                var b = new PropsBuilder()
                    .Set("value", optValue)
                    .Text(label)
                    .Set("checked", selectedValue == optValue);
                if (onSelect != null)
                    b.OnClick(() => onSelect(optValue));
                list.Add(new UINode(ElementTypes.RadioOption, b.Build(), $"${i}"));
            }
            return list;
        }

        private UINode RenderClassComponent(Type type, Props props, Fiber fiber)
        {
            try
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

                HookContext.Begin(fiber.HookSlots);
                var result = instance.Render();
                HookContext.End();
                return result;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Paper] Class component error: " + ex.ToString());
                var errorProps = new Props(new Dictionary<string, object?> { { "text", $"Component Error: {ex.Message}" } });
                return new UINode("Text", errorProps);
            }
        }

        private UINode RenderFunctionComponent(Func<Props, UINode> fn, Props props, Fiber fiber)
        {
            try
            {
                HookContext.Begin(fiber.HookSlots);
                var result = fn(props);
                HookContext.End();
                return result;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Paper] Function component error: " + ex.ToString());
                var errorProps = new Props(new Dictionary<string, object?> { { "text", $"Function Component Error: {ex.Message}" } });
                return new UINode("Text", errorProps);
            }
        }

        private Fiber Reconcile(Fiber? current, UINode node, Fiber? parent, bool forceReconcile = false)
        {
            if (current != null && IsSameType(current, node))
            {
                if (ShouldSkipReconciliation(current, node, forceReconcile))
                {
                    current.EffectTag = EffectTag.None;
                    return current;
                }

                return Render(node, current, parent);
            }
            else
            {
                if (current != null)
                    current.EffectTag = EffectTag.Deletion;

                return Render(node, null, parent);
            }
        }

        private bool ShouldSkipReconciliation(Fiber current, UINode node, bool forceReconcile)
        {
            if (forceReconcile) return false;

            if (current.Instance is Components.Component component)
            {
                if (!component.ShouldComponentUpdate(node.Props))
                    return true;
            }

            if (current.Type is Func<Props, UINode>)
            {
                // Re-render if any hook slot has pending state updates for this specific fiber.
                // This avoids the global stateChanged flag causing all components to re-render
                // when only one component's state changed.
                bool hasPendingState = current.HookSlots.Any(s => s.HasPendingUpdaters);
                if (!hasPendingState && ShallowEqual(current.Props, node.Props))
                    return true;
                return false;
            }

            if (current.Type is string)
                return false;

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

                // Special handling for children - they're always considered different
                if (kvp.Key == "children")
                    return false;

                if (!Equals(kvp.Value, nextValue))
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

                var newFiber = Reconcile(oldChild, childNode, parent);
                newFiber.Index = index;

                if (prevSibling == null)
                    parent.Child = newFiber;
                else
                    prevSibling.Sibling = newFiber;

                prevSibling = newFiber;
                index++;
            }

            var newKeySet = new HashSet<string>(
                newChildren.Select((n, i) => n.Key ?? $"${i}"));
            foreach (var kv in keyedOld)
            {
                if (!newKeySet.Contains(kv.Key))
                    kv.Value.EffectTag = EffectTag.Deletion;
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

        private static void CommitDeletions(Fiber fiber)
        {
            if (fiber.EffectTag == EffectTag.Deletion)
                UnmountFiber(fiber);
        }

        private static void UnmountFiber(Fiber fiber)
        {
            foreach (var slot in fiber.HookSlots)
                slot.Cleanup?.Invoke();

            var child = fiber.Child;
            while (child != null)
            {
                UnmountFiber(child);
                child = child.Sibling;
            }
        }

        private static void FlushEffects(Fiber? fiber)
        {
            if (fiber == null) return;

            FlushEffects(fiber.Child);
            
            foreach (var slot in fiber.HookSlots)
            {
                if (slot.PendingEffect != null)
                {
                    slot.Cleanup?.Invoke();
                    
                    try
                    {
                        var cleanup = slot.PendingEffect();
                        slot.Cleanup = cleanup;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[Paper] Effect error: " + ex.ToString());
                        slot.Cleanup = null;
                    }
                    slot.PendingEffect = null;
                }
            }
            
            FlushEffects(fiber.Sibling);
        }

        private static bool IsSameType(Fiber fiber, UINode node) =>
            fiber.Type.Equals(node.Type);

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