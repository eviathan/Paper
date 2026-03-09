using Paper.Core.Enums;
using Paper.Core.Hooks;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

namespace Paper.Core.Reconciler
{
    /// <summary>
    /// Internal representation of a mounted UI node with its state and layout result.
    /// The reconciler maintains a tree of Fibers as the "current" committed tree,
    /// and builds a "work-in-progress" tree during each render pass.
    /// </summary>
    public sealed class Fiber
    {
        // ── Identity ─────────────────────────────────────────────────────────

        /// <summary>Matches <see cref="UINode.Type"/>.</summary>
        public object Type { get; set; } = null!;

        /// <summary>Matches <see cref="UINode.Key"/>.</summary>
        public string? Key { get; set; }

        /// <summary>The current props for this fiber.</summary>
        public Props Props { get; set; } = Props.Empty;

        // ── Tree links ────────────────────────────────────────────────────────

        public Fiber? Parent { get; set; }
        public Fiber? Child { get; set; }
        public Fiber? Sibling { get; set; }
        /// <summary>Pointer to the matching fiber in the committed ("current") tree.</summary>
        public Fiber? Alternate { get; set; }

        // ── Component state ───────────────────────────────────────────────────

        /// <summary>Hook slots for this fiber's function/class component.</summary>
        internal List<HookSlot> HookSlots { get; } = new();

        /// <summary>
        /// The mounted class component instance (only set for class components).
        /// </summary>
        public Components.Component? Instance { get; set; }

        // ── Reconciler bookkeeping ────────────────────────────────────────────

        public EffectTag EffectTag { get; set; } = EffectTag.None;

        /// <summary>Index among siblings — used for keyed reconciliation.</summary>
        public int Index { get; set; }

        // ── Layout result (filled in by Paper.Layout) ─────────────────────────

        /// <summary>Computed layout box: position and size in pixels, relative to parent.</summary>
        public LayoutBox Layout { get; set; }

        /// <summary>
        /// Computed style for this fiber after resolving defaults, class styles, interaction variants, and inline props.
        /// Set by the host before layout + render.
        /// </summary>
        public StyleSheet ComputedStyle { get; set; } = StyleSheet.Empty;

        // ── Stale / dirty flag ────────────────────────────────────────────────

        /// <summary>True when this fiber's state has changed and it needs re-rendering.</summary>
        public bool IsDirty { get; set; } = false;
    }
}
