# Why Paper’s UI Can Feel Slower Than Electron + React

This doc summarizes **architectural** reasons Paper can feel slower than Electron + embedded React, and what would be needed to improve it.

---

## 1. Full-tree update on every state change

**What happens today**

- Any `setState` (e.g. from `OnChange` on an input) sets `_renderRequested = true`.
- On the next frame we call `_reconciler.Update(_rootFactory!())`.
- **Reconcile**: We always re-run from the **root**. For function components, `ShouldSkipReconciliation` returns `false` whenever `stateChanged` is true, so we **never** skip. The whole component tree is re-executed (root → leaves).
- **Styles**: `ApplyComputedStyles(root)` walks the **entire** tree to resolve/compute styles.
- **Layout**: `_layout.Layout(root, ...)` does a **full** layout pass over the whole tree (flex, block, etc.).
- **Draw**: We create a new `FiberRenderer` and call `renderer.Render(root)`, which walks the **entire** tree again to draw.

So **one keystroke** triggers:

- Full re-run of every component from root
- Full style resolution
- Full layout
- Full draw

**Electron + React**

- React’s reconciler is built for incremental updates (Fiber, work loop, prioritization).
- Only components whose props/state actually changed re-run; subtrees can bail out.
- The browser does incremental layout and repaint; we don’t.

**Takeaway:** Paper does “full tree reconcile + full layout + full draw” on every state change. There is no “only this subtree changed” path.

---

## 2. No incremental layout or dirty regions

- **Layout**: We have no dirty flags or invalidation. Every frame we lay out the whole tree from root. We don’t cache layout for unchanged subtrees.
- **Draw**: We redraw the whole screen every time (clear + full tree walk). There’s no “only repaint this rectangle” or retained layer tree.

So even when only an input’s text changed, we still:

- Re-layout every node (flex, text measurement, etc.).
- Re-draw every node.

---

## 3. Recreating renderer state every frame

In `LayoutAndDraw()` we do:

```csharp
var renderer = new FiberRenderer(_rects!, _viewports!, _text, ...) { ... };
renderer.Render(root);
```

So every frame we allocate a new `FiberRenderer` and pass in fresh state. There’s no reuse of renderer or cached draw state (e.g. for unchanged subtrees).

---

## 4. Text measurement and style resolution

- **Text**: Layout calls the measurer for every text-bearing node. There’s no cache keyed by (string, font, size), so the same text can be measured repeatedly.
- **Styles**: We resolve styles for every node every time; no “this node’s styles didn’t change” short-circuit.

---

## 5. Single-threaded, synchronous pipeline

- Events, reconcile, style, layout, and draw all run on the same thread, in one synchronous sequence per frame.
- Electron/Chrome use multiple processes and highly optimized, incremental rendering. We don’t have that.

---

## What would help (directionally)

| Area | Change |
|------|--------|
| **Reconcile** | When only leaf props change (e.g. input `value`), skip re-running ancestors and unchanged siblings (e.g. finer-grained `ShouldSkipReconciliation` or “input value” treated as a special case that doesn’t force full root re-run). |
| **Layout** | Invalidation/dirty: mark subtrees that need layout when their content or constraints change; only re-layout dirty nodes (and ancestors that depend on them). |
| **Draw** | Reuse a single `FiberRenderer` and only traverse/issue draw commands for dirty nodes or regions; or at least avoid reallocating the renderer every frame. |
| **Text** | Cache measure results by (text, font, size, maxWidth) so repeated measures for the same content are cheap. |
| **Input** | Keep the current optimization: don’t force a full reconcile on every keystroke; only update the displayed value from a local buffer and commit to app state on blur or on a timer (we tried debounce and reverted; a “reconcile only when input blurs” or “reconcile on idle” path could be revisited with care). |

The biggest single win would be **not re-running and re-laying-out the entire tree on every keystroke** (e.g. treat “focused input text” as a local display concern and only reconcile when that value is “committed” or when something else changes). That would require a clear contract for when app state is updated vs when we only update the displayed text.
