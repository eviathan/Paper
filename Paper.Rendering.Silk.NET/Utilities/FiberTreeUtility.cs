using Paper.Core.Reconciler;

namespace Paper.Rendering.Silk.NET.Utilities
{
    /// <summary>Fiber tree navigation and identity helpers.</summary>
    public static class FiberTreeUtility
    {
        /// <summary>Returns the path from the root down to <paramref name="target"/> (root first).</summary>
        public static List<Fiber> PathToRoot(Fiber target)
        {
            var path = new List<Fiber>();

            Fiber? current = target;
            while (current != null)
            {
                path.Add(current);
                current = current.Parent;
            }

            path.Reverse();

            return path;
        }

        /// <summary>Stable path string (dot-separated indices from root) used to match the same
        /// control after the tree is replaced by reconciliation.</summary>
        public static string GetPathString(Fiber? fiber)
        {
            if (fiber == null)
                return "";

            var path = PathToRoot(fiber);

            return string.Join(".", path.Select(pathFiber => pathFiber.Index));
        }

        /// <summary>Find the fiber at the given path (e.g. "0.2.0.1") in the current tree so focus
        /// points at the live fiber after re-render. Path is from <see cref="PathToRoot"/> (root first).</summary>
        public static Fiber? GetFiberByPath(Fiber? root, string? path)
        {
            if (root == null || string.IsNullOrEmpty(path))
                return null;

            var parts = path.Split('.');

            Fiber? current = root;

            // First part is root's index; descend using the rest.
            for (int partIndex = 1; partIndex < parts.Length; partIndex++)
            {
                if (current == null || !int.TryParse(parts[partIndex], out int childIndex))
                    return null;

                var child = current.Child;
                for (int siblingIndex = 0; child != null && siblingIndex < childIndex; siblingIndex++)
                    child = child.Sibling;

                current = child;
            }

            return current;
        }

        /// <summary>True if <paramref name="a"/> and <paramref name="b"/> are the same fiber or one
        /// is an ancestor of the other (treated as the same "control" for click purposes).</summary>
        public static bool IsSameControl(Fiber? a, Fiber? b)
        {
            if (a == null || b == null)
                return false;

            if (ReferenceEquals(a, b))
                return true;

            return IsDescendantOf(a, b) || IsDescendantOf(b, a);
        }

        /// <summary>True if <paramref name="node"/> has <paramref name="ancestor"/> anywhere in its parent chain.</summary>
        public static bool IsDescendantOf(Fiber? node, Fiber? ancestor)
        {
            for (var parent = node?.Parent; parent != null; parent = parent.Parent)
                if (ReferenceEquals(parent, ancestor)) return true;

            return false;
        }
    }
}
