# Paper GUI Framework — Production Roadmap

> Ordered by user impact. Testing, documentation, and build pipelines are out of scope until the framework itself is feature-complete.

---

## Phase 0 — CSX Developer Experience ⚠️ Do first

These two issues make CSX feel like a second-class citizen compared to C#. Fix them before anything else because they affect every hour of development.

### 0.1 Roslyn-backed Hover (real type information everywhere)

**Root cause**: `Hover.Compute` in `Program.cs` is a dictionary lookup against hardcoded docs for elements/hooks/CSS props. It has no Roslyn integration. `color` appears to "work" only because it matches the CSS `color` property name. Lambda parameters, local variables, method return types — none of them resolve.

**Fix**:
1. **Character-level source map** — during codegen (`CSXCodegen.cs`), emit a list of `(csxStart, csxEnd, genStart, genEnd)` spans for every token written. Every transformation (JSX → `UI.Box(...)`, `const [a,b]=useState` → `var (a,b,_)=Hooks.UseState`, helper function → lambda) records the mapping.
2. **Roslyn semantic hover** — on hover request: map CSX cursor position → generated C# position via source map, find the `SyntaxNode` at that position in the Roslyn tree, call `model.GetSymbolInfo()` / `model.GetTypeInfo()`, return `ISymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)`.
3. **Fallback** — if no Roslyn symbol found (e.g. cursor on a JSX tag name or CSS property), fall back to the existing hardcoded docs.

**Result**: hovering over `i` in `.Select((color, i) =>` shows `(parameter) int i`. Hovering over any local variable, method, or expression shows its real C# type — indistinguishable from native C# files.

### 0.2 Typed Props — Components as first-class C# types

**Root cause**: `Props` is `Dictionary<string, object?>`. There is no static type for what a component accepts. No intellisense, no compile errors for wrong prop types, no hover info on prop names.

**Fix — inline typed props in function signature** (mirrors TSX destructuring):

```jsx
// CSX author writes:
function Badge({ string label, PaperColour colour, bool active = false }) {
  return (
    <Box style={{ background: active ? colour : '#555' }}>
      <Text>{label}</Text>
    </Box>
  );
}

// Codegen emits:
public record BadgeProps(string Label, PaperColour Colour, bool Active = false);
// component receives BadgeProps, caller gets compile-time checked props
<Badge label="Admin" colour={accentColour} active={true} />
```

**Implementation steps**:
- `CSXParser.cs`: parse `{ Type name, Type name = default }` in function signature
- `CSXCodegen.cs`: synthesise a `record <ComponentName>Props(...)` and a typed `props.As<T>()` call at function entry
- `PropsBuilder`: generate a typed `Set(BadgeProps p)` overload so the call site emits typed props
- `IntelliSense`: completion provider reads the synthesised record type from Roslyn for prop name + type suggestions on `<Badge |`

**Simpler interim** (no parser changes, 80% of value): add `Props.As<T>()` method that deserialises the bag into any record using reflection, and document the pattern `var p = props.As<MyProps>()`. Authors declare props as plain C# records above the function.

### 0.3 Completion — Props names and types for custom components

Currently completion inside `<MyComponent |` offers nothing. With typed props (§0.2) in place:

- [ ] When cursor is inside a JSX opening tag for a non-intrinsic element, find the component's props record via the Roslyn semantic model
- [ ] Offer each record property as a completion item with its type
- [ ] Mark required vs optional (optional = has default value in record)

### 0.4 Go-to-definition for CSX identifiers

- [ ] Map CSX cursor → generated C# position → Roslyn `GetDefinitionAsync()` → map result back to CSX position (or to the `.cs` file if defined there)
- [ ] Enables Cmd+Click on `Hooks.UseState`, component names, imported C# types

---

## Phase 1 — Critical Rendering Gaps

### 1.1 Text System Overhaul ⚠️ Highest priority
The single 16px ASCII raster atlas is the most visible limitation in the entire framework.

- [ ] **Font size rendering** — `fontSize` is parsed but ignored at draw time. Implement either:
  - Multi-size atlas (16px, 24px, 32px, 48px generated at startup), or
  - SDF (Signed Distance Field) rendering — single atlas, correct at all scales, requires GLSL shader change in `TextBatch`
- [ ] **Unicode / extended charset** — atlas currently covers ASCII 32–127 only. Extend to Latin Extended, common symbols, at minimum
- [ ] **Multiple font weights / styles** — `FontWeight` is stored in `StyleSheet` but has no effect. Need separate atlas per weight/style variant (bold, italic)
- [ ] **Multiple font families** — `FontFamily` prop parsed but ignored. Need per-family atlas management
- [ ] **Placeholder text** — `Input` and `Textarea` have no placeholder support. Render `Props.Placeholder` in muted colour when value is empty
- [ ] **Text decoration** — underline, strikethrough. Both style-side (`TextDecoration` property) and render-side (draw line segments in `FiberRenderer`)

### 1.2 `box-shadow` Rendering
`BoxShadow[]` is fully modelled in `StyleSheet.cs` and parsed from CSSS/inline styles. `FiberRenderer` ignores it entirely.

- [ ] Render box shadows as blurred rects drawn *before* the element background (use Z-order / painter ordering)
- [ ] Support `inset` shadows
- [ ] Multiple shadows per element (already modelled as array)

### 1.3 Transition — Expand Animated Properties
Infrastructure exists in `FiberRenderer` (`TransitionState`, `ParseTransitionDurations`). Only `background` and `opacity` animate.

- [ ] Add: `color`, `border-color`, `transform` (translate/scale/rotate), `width`, `height`
- [ ] Ensure all animated values use the same `1 - exp(-dt * 4/duration)` lerp already in use

### 1.4 Scrollbar Stylability
Thumb colour is hardcoded as `(0.9, 0.9, 0.9, 0.55 * opacity)` in `DrawScrollbar`. Users need to override.

- [ ] Add `StyleSheet` properties: `ScrollbarThumbColor`, `ScrollbarTrackColor` (or use existing custom prop mechanism)
- [ ] Pass through into `DrawScrollbar` / `RecordScrollbarGeometry`
- [ ] Expose in `InlineStyleTranslator` and CSSS mapper

---

## Phase 2 — Input & Interaction Completeness

### 2.1 Clipboard (Copy / Paste) ⚠️ Blocking for real text editing
GLFW exposes `glfwGetClipboardString` / `glfwSetClipboardString`. Silk.NET wraps these.

- [ ] Wire `Cmd+C` / `Ctrl+C` → copy selected text to clipboard in `PaperSurface.OnKeyDown`
- [ ] Wire `Cmd+X` / `Ctrl+X` → cut (copy + delete selection)
- [ ] Wire `Cmd+V` / `Ctrl+V` → paste at caret, replace selection
- [ ] Works for both `Input` and `Textarea` elements

### 2.2 Input Attributes
- [ ] `placeholder` — render muted hint text when value is empty (see §1.1)
- [ ] `maxLength` — clamp value length during `OnKeyChar` in `PaperSurface`
- [ ] `readOnly` — block edit events, allow selection/copy
- [ ] `disabled` — block all events, apply dimmed style via `StyleResolver` default
- [ ] `type="number"` — filter non-numeric keystrokes; optional increment/decrement arrow buttons

### 2.3 Drag and Drop
No infrastructure exists.

- [ ] Add `onDragStart`, `onDragOver`, `onDrop`, `onDragEnd` to `Props`
- [ ] Track `_dragging` state in `PaperSurface` (path + payload object)
- [ ] Dispatch drag events through existing `DispatchPointer` mechanism
- [ ] Visual feedback: opacity / cursor change on drag source

### 2.4 Right-click / Context Menus
- [ ] Dispatch `onContextMenu` event on right-mouse-down (event type already partially modelled)
- [ ] Build a `ContextMenu` component (positioned box, z-index overlay, dismiss on outside click)

---

## Phase 3 — Layout Engine Completeness

### 3.1 `position: sticky`
`Sticky` is in the `Position` enum; `LayoutEngine` ignores it.

- [ ] Detect sticky elements during layout pass
- [ ] In `FiberRenderer`, clamp visual Y offset so element sticks within its scroll ancestor
- [ ] Requires renderer to know scroll offset of ancestor (already available via `GetScrollOffset`)

### 3.2 `position: fixed`
Currently behaves identically to `absolute`.

- [ ] Fixed elements must anchor to viewport regardless of scroll
- [ ] In `FiberRenderer.Render()`, suppress accumulated `scrollX`/`scrollY` for fixed fibers

### 3.3 Flex `wrap-reverse`
Parses correctly; `FlexLayout.BuildLines` does not reverse cross-axis stacking.

- [ ] In `FlexLayout.PositionLines()`, reverse line order when `FlexWrap == WrapReverse`

### 3.4 CSS Grid — Missing Features
- [ ] `grid-auto-flow: column` — currently only row auto-placement in `GridLayout.PlaceItems`
- [ ] `grid-auto-columns` / `grid-auto-rows` — implicit track sizing (currently uses fixed fallback)
- [ ] Named grid areas — `grid-template-areas: "header header" "sidebar content"` + `grid-area: header`

### 3.5 `calc()` in Lengths
- [ ] Add `Length.Calc(expression)` variant
- [ ] Parser in `InlineStyleTranslator` for `calc(100% - 24px)` → `Calc("100% - 24px")`
- [ ] `ResolveLength` evaluates Calc against container size

### 3.6 `aspect-ratio`
- [ ] Add `AspectRatio: float?` to `StyleSheet`
- [ ] `LayoutEngine`: when one axis is unconstrained, derive it from the other via the ratio
- [ ] Images already infer ratio from pixel dimensions — unify with this

---

## Phase 4 — Styling & Visual Polish

### 4.1 CSS Custom Properties (`--var`)
CSSS has CSSS compile-time `$vars`. Runtime CSS custom properties (cascade-aware, overridable per subtree) do not exist.

- [ ] Add `CustomProperties: Dictionary<string, string>?` to `StyleSheet`
- [ ] `StyleResolver` merges custom properties down the tree (similar to CSS cascade)
- [ ] `InlineStyleTranslator` resolves `var(--name)` references at render time against the inherited map
- [ ] CSSS parser: support `:root { --primary: #58a6ff; }` syntax

### 4.2 Gradients
`Background` only supports `PaperColour` (solid).

- [ ] Add `BackgroundGradient` type (stops: list of `(float position, PaperColour)`)
- [ ] `RectBatch` or a new `GradientBatch`: pass stops as uniforms or generate per-vertex colours
- [ ] `InlineStyleTranslator`: parse `linear-gradient(...)` and `radial-gradient(...)`

### 4.3 `border-style` Variants
Only `solid` renders. `BorderStyle` enum has `Dashed` / `Dotted`.

- [ ] In `FiberRenderer` border-strip rendering: apply dash/dot stipple pattern based on `BorderStyle`

### 4.4 `opacity` Cascade
`inheritedOpacity` is passed into `Render()` and multiplied — verify it reaches *all* draw calls including text, images, and borders. Ensure child opacity compounds correctly with parent.

### 4.5 `background-image` Rendering
`BackgroundImage: string?` and `BackgroundSize: ObjectFit?` are in `StyleSheet` but unused.

- [ ] In `FiberRenderer`: if `style.BackgroundImage != null`, load via `ImageTextureLoader` and draw as textured rect with `BackgroundSize` fit mode
- [ ] Support `cover`, `contain`, `fill` ObjectFit modes (same code path as `<Image>`)

### 4.6 CSSS `@media` Queries
- [ ] Add `@media (max-width: Npx)` / `@media (min-width: Npx)` support in `CSSSParser`
- [ ] `CSSSSheet.Match()` receives current window dimensions and evaluates media conditions
- [ ] `PaperSurface` passes window size into `StyleResolver` each frame

---

## Phase 5 — Component Library

The framework ships only primitive elements. Production apps need:

### 5.1 Core Missing Controls
- [ ] **Modal / Dialog** — full-viewport overlay (`position: fixed`, z-index top), traps Tab focus, Escape dismisses
- [ ] **Tooltip** — triggered by hover delay, absolute-positioned near target, auto-flips at viewport edge
- [ ] **Tabs** — tab bar + content panel; stateful active tab; keyboard left/right navigation
- [ ] **Accordion** — collapsible sections with animated height (needs transition on `height`)
- [ ] **Slider / Range** — draggable thumb on track, `min`/`max`/`step`, mouse-drag in `PaperSurface`
- [ ] **NumberInput** — `<Input type="number">` + up/down arrow buttons
- [ ] **Progress bar** — determinate (`value`/`max`) and indeterminate (animated)
- [ ] **Spinner / Loading indicator** — CSS rotate transition on a partial-arc rect

### 5.2 Virtual / Windowed List
Critical for performance with large datasets (> ~200 items). All items currently render.

- [ ] `UseVirtualScroll(items, itemHeight)` hook — computes `visibleRange` from `_scrollOffsets`
- [ ] Scroll container renders only visible items + overscan buffer
- [ ] Supports variable item heights (requires measuring pass)

### 5.3 Notification / Toast System
- [ ] Global `ToastContext` provider at root
- [ ] `UseToast()` hook returns `show(message, options)`
- [ ] Toast renders in a fixed overlay layer (z-index above everything)
- [ ] Auto-dismiss after configurable duration with fade-out transition

### 5.4 Portal / Overlay Rendering
Needed by Modal, Tooltip, Dropdown, Toast. Elements rendered as children of one fiber but visually parented to the root (escape clipping/stacking contexts).

- [ ] Add a `Portal` component type that appends its children to a root-level overlay list
- [ ] `FiberRenderer` collects portal children and renders them last (after z-index deferred pass)

---

## Phase 6 — Architecture & Correctness

### 6.1 Event Bubbling ⚠️ Composability blocker
Events currently do not bubble. `onClick` on a child should propagate to ancestor handlers unless stopped.

- [ ] `SyntheticEvent.StopPropagation()` method + `_propagationStopped` flag
- [ ] `DispatchPointer` in `PaperSurface`: after dispatching to target, walk parent chain and dispatch to each ancestor's handler (unless stopped)
- [ ] `StopPropagation` and `PreventDefault` on `PointerEvent` and `KeyEvent`
- [ ] Capture phase (`onClickCapture`) already in `Props` — wire it to pre-target dispatch

### 6.2 `UseEffect` Cleanup Ordering
- [ ] Verify deletions run cleanup *before* new-mount effects of siblings (React spec: unmount effects fire before mount effects of new nodes at same level)
- [ ] Review `FlushEffects` traversal in `Reconciler.cs`

### 6.3 Key Reconciliation — Type Change Edge Case
When a keyed child changes element type (e.g. `<Box key="a">` → `<Text key="a">`), current O(1) map may reuse the fiber instead of unmount + remount.

- [ ] In `ReconcileChildren`: if key matches but `fiber.Type != newNode.Type`, treat as deletion + insertion, not update

### 6.4 Layout Caching / Subtree Invalidation
Every frame does a full layout pass regardless of what changed.

- [ ] Mark fibers dirty on state change (already tracked via `IsDirty`)
- [ ] In `LayoutEngine`, skip subtrees where neither the fiber nor any descendant is dirty
- [ ] Invalidate ancestor chain when a descendant changes size

### 6.5 `UseEffect` Dependencies — Referential Equality
Currently `DepsEqual` compares deps with `Equals()`. For object/array deps this may trigger on every render.

- [ ] Document the contract: deps should be primitives or stable references (UseStable/UseRef)
- [ ] Consider adding a `UseDeepEffect` variant with structural comparison

---

## Phase 7 — Platform & Integration

### 7.1 Renderer Abstraction
All rendering is hard-coupled to Silk.NET / OpenGL.

- [ ] Define `IRenderBackend` interface: `DrawRect`, `DrawGlyph`, `PushScissor`, `PopScissor`, `PushStencil`, `PopStencil`, `DrawTexture`
- [ ] Move `RectBatch`, `TextBatch` behind the interface
- [ ] Enables: Vulkan backend, software/CPU backend for testing, WebGL via WASM

### 7.2 Accessibility (A11y)
- [ ] Add `role`, `ariaLabel`, `ariaExpanded`, `ariaDisabled` to `Props`
- [ ] Tab focus traversal already works — surface it to macOS NSAccessibility / Windows UI Automation
- [ ] Focus ring styling (`focusStyle` already exists — ensure it's always visible for keyboard navigation)

### 7.3 Internationalisation
- [ ] RTL layout (`direction: rtl` reverses flex row direction, text alignment)
- [ ] IME (Input Method Editor) support for CJK input — requires platform-specific composition events from GLFW
- [ ] Unicode text shaping (HarfBuzz or similar) for correct rendering of complex scripts

### 7.4 Multi-window
- [ ] Allow multiple `PaperSurface` instances sharing a GL context
- [ ] Window management: create, close, focus, move, resize from application code

---

## Priority Summary

| # | Item | Why now |
|---|------|---------|
| 1 | Font size rendering (SDF or multi-atlas) | Single-size text is immediately obvious to any user |
| 2 | Clipboard (copy/paste) | Blocking for any real text editing workflow |
| 3 | `box-shadow` rendering | Already fully modelled — just not drawn |
| 4 | `placeholder` + `disabled` + `readOnly` | Table stakes for form inputs |
| 5 | Transition property expansion | Cheap — infrastructure exists, add more properties |
| 6 | Virtual / windowed list | Hard performance cliff with real data |
| 7 | Event bubbling | Composability blocker for complex components |
| 8 | `position: sticky` + `fixed` | Very common layout patterns |
| 9 | Portal / overlay rendering | Prerequisite for Modal, Tooltip, Toast |
| 10 | Modal, Tooltip, Tabs, Slider | Most apps need these immediately |
| 11 | Gradients | Major visual gap |
| 12 | CSSS `@media` queries | Responsive layout support |
| 13 | CSSS custom properties (`--var`) | Real-world theming |
| 14 | Drag and drop | Complex interactions |
| 15 | Renderer abstraction | Future-proofing / testability |
| 16 | A11y / IME / RTL | Polish + compliance |
