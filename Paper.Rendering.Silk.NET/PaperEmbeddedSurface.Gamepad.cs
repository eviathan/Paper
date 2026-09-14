using Paper.Core.Reconciler;
using Paper.Rendering.Silk.NET.Utilities;

namespace Paper.Rendering.Silk.NET
{
    public enum NavDirection { Up, Down, Left, Right }

    // Spatial focus navigation — genuinely new, no equivalent exists in Canvas or anywhere else
    // in Paper. A D-pad/stick (or any other directional input source) has no notion of "next in
    // tab order", so this finds the nearest focusable element in the requested direction
    // geometrically instead, using each fiber's already-computed screen position (Fiber.Layout)
    // rather than tree order. Paper itself has no concept of a gamepad — these methods take only
    // an abstract NavDirection; the host (e.g. Prism's PaperOverlay) owns mapping any real input
    // device to these calls.
    public sealed partial class PaperEmbeddedSurface
    {
        // Weights how much a candidate's sideways drift counts against it relative to how far it
        // is in the intended direction — higher favours staying aligned over moving efficiently.
        // 2.5 is a common starting point for this kind of 2D/TV-remote-style navigation.
        private const float PerpendicularNavPenalty = 2.5f;

        /// <summary>Move focus to the nearest focusable element in the given direction, using
        /// on-screen position rather than tab order. If nothing is focused, focuses the first
        /// focusable element instead of navigating. No candidate in that direction = no-op.</summary>
        public void NavigateFocus(NavDirection direction)
        {
            if (_reconciler?.Root == null) return;

            var candidates = new List<Fiber>();
            HitTestUtility.CollectFocusable(_reconciler.Root, candidates);
            if (candidates.Count == 0) return;

            var current = _inputState.Focused;
            if (current == null || !candidates.Any(f => ReferenceEquals(f, current)))
            {
                SetFocus(candidates[0]);
                return;
            }

            var (cx, cy) = Center(current);
            Fiber? best = null;
            float bestScore = float.MaxValue;

            foreach (var candidate in candidates)
            {
                if (ReferenceEquals(candidate, current)) continue;

                var (fx, fy) = Center(candidate);
                float dx = fx - cx, dy = fy - cy;
                float primary, perpendicular;

                switch (direction)
                {
                    case NavDirection.Up:    if (dy >= 0) continue; primary = -dy; perpendicular = MathF.Abs(dx); break;
                    case NavDirection.Down:  if (dy <= 0) continue; primary = dy;  perpendicular = MathF.Abs(dx); break;
                    case NavDirection.Left:  if (dx >= 0) continue; primary = -dx; perpendicular = MathF.Abs(dy); break;
                    case NavDirection.Right: if (dx <= 0) continue; primary = dx;  perpendicular = MathF.Abs(dy); break;
                    default: continue;
                }

                float score = primary + PerpendicularNavPenalty * perpendicular;
                if (score < bestScore) { bestScore = score; best = candidate; }
            }

            if (best != null) SetFocus(best);
        }

        /// <summary>Activates the focused element — equivalent to Enter/Space/click.</summary>
        public void ActivateFocusedElement() => ActivateFocused();

        /// <summary>Clears focus. A component wanting different "back" behaviour (closing a menu,
        /// popping a screen) should still implement that itself via a normal key/click handler —
        /// this is just the sensible default when nothing more specific is wired up.</summary>
        public void ClearFocus() => SetFocus(null);

        /// <summary>Moves focus to the first focusable element in tab order (see
        /// <see cref="BuildTabOrder"/>) — for landing focus somewhere sensible right after a menu
        /// opens or first mounts, as opposed to <see cref="NavigateFocus"/>'s relative movement from
        /// whatever's currently focused. No-op (clears focus) if nothing is focusable.</summary>
        public void ResetFocus()
        {
            var ordered = BuildTabOrder();
            SetFocus(ordered.Count > 0 ? ordered[0] : null);
        }

        private static (float x, float y) Center(Fiber fiber) =>
            (fiber.Layout.AbsoluteX + fiber.Layout.Width / 2f, fiber.Layout.AbsoluteY + fiber.Layout.Height / 2f);
    }
}
