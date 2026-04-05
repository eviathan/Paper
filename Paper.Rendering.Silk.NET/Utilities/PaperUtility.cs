using Paper.Core.Reconciler;
using System.Numerics;

namespace Paper.Rendering.Silk.NET.Utilities
{
    /// <summary>Miscellaneous rendering-level utilities that don't belong to a more specific category.</summary>
    public static class PaperUtility
    {
        /// <summary>Convert input position (window coords) to layout space (logical pixels).</summary>
        public static (float x, float y) ToLayoutCoords(Vector2 position) =>
            (position.X, position.Y);

        /// <summary>Resolve relative image paths (e.g. "Assets/test.png") relative to the app base
        /// directory so they load from the output folder.</summary>
        public static string? ResolveImagePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (Path.IsPathRooted(path))
                return path;

            return Path.Combine(AppContext.BaseDirectory, path);
        }

        /// <summary>Mark <paramref name="fiber"/> and every node in its subtree as style-dirty so that
        /// <see cref="Paper.Core.Styles.StyleResolver"/> recomputes on the next frame.</summary>
        public static void InvalidateStyleTree(Fiber? fiber)
        {
            while (fiber != null)
            {
                fiber.StyleDirty = true;
                InvalidateStyleTree(fiber.Child);
                fiber = fiber.Sibling;
            }
        }
    }
}
