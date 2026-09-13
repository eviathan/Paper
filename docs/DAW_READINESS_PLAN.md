# Paper DAW-Readiness Plan

Four focused tracks to make Paper viable as a DAW UI framework. Each track is independent enough to be worked on in isolation, but they are listed in recommended execution order.

---

## Track 1 — Dirty-Region Rendering

**Goal:** Skip layout and draw work for unchanged parts of the tree. A DAW has waveforms, meters, and playheads updating at 60 fps while most of the UI sits completely still.

### Current Situation

Every `setState` results in:
1. Full reconcile from root (`Reconciler.Update` → always walks entire tree)
2. Full style resolution (`StyleResolver.Resolve` on every fiber)
3. Full layout (`LayoutEngine.Layout` root → leaves)
4. Full draw (`FiberRenderer.Render` root → leaves, clears and redraws entire surface)

The boolean `_renderState.NeedsLayout` is the only granularity that exists.

### What Already Exists (Build On This)

`Fiber` already has three dirty flags:
- `IsDirty` — component needs re-render
- `HasDirtyDescendant` — a child enqueued a state update
- `StyleDirty` — interaction state changed (hover/focus/active)

`ShouldSkipReconciliation()` already bails out on intrinsic elements when no dirty descendant and props are equal. This is a good foundation — the problem is style/layout/draw ignore it.

### Implementation

#### Step 1 — Layout Dirty Flags

**File:** `Paper.Core/VirtualDom/Fiber.cs`

Add two fields:
```csharp
public bool LayoutDirty { get; set; }        // this fiber's own layout needs recompute
public bool HasLayoutDirtyDescendant { get; set; } // a descendant needs recompute
```

Mark `LayoutDirty = true` in `StyleResolver` when the resolved StyleSheet differs from the previously resolved one in any layout-affecting property (Width, Height, Padding, Margin, FlexGrow, Display, Gap, Position). Propagate `HasLayoutDirtyDescendant` up the parent chain (same pattern as `HasDirtyDescendant`).

**File:** `Paper.Layout/LayoutEngine.cs`

Early-exit in the layout traversal: if `!fiber.LayoutDirty && !fiber.HasLayoutDirtyDescendant`, copy the previous `LayoutBox` from `fiber.Alternate` and skip descending. Parent still needs to know the child's size, so cache it. This is a correctness-sensitive change — flex parents must still re-measure if any child's size changes, so propagation must be accurate.

#### Step 2 — Draw Dirty Rectangles

**File:** `Paper.Rendering.Silk.NET/FiberRenderer.cs`

Add a `Rectangle _accumulatedDirtyRect` (union of all fiber dirty regions in screen space). After layout, walk the dirty fibers and union their `LayoutBox.AbsoluteX/Y + Width/Height` into a screen-space dirty rect.

In `Render()`, before drawing each fiber:
```csharp
if (!_dirtyRect.Intersects(fiber.Layout.ScreenBounds))
    goto renderSiblings; // skip draw, still recurse for clip bookkeeping
```

For the OpenGL clear, replace `GL.Clear(ClearBufferMask.ColorBufferBit)` with a scissor-rect clear:
```glsl
glScissor(dirtyX, dirtyY, dirtyW, dirtyH);
glClear(GL_COLOR_BUFFER_BIT);
glScissor(0, 0, screenW, screenH); // restore
```

Then only flush batches that have accumulated geometry inside the dirty rect.

**Important edge cases:**
- CSS transitions run every frame even on non-dirty fibers — these must always contribute their fiber's bounds to the dirty rect while the transition is active.
- Scrollbar fade-out animations: same — flag with `HasActiveAnimation` on the fiber.
- Portal roots (`Reconciler.PortalRoots`) are rendered separately; each needs its own dirty rect accumulation.

#### Step 3 — Style Dirty Short-Circuit

**File:** `Paper.Core/Styles/StyleResolver.cs`

Cache the last resolved `StyleSheet` hash per fiber (add `uint ComputedStyleHash` to Fiber). Skip full resolution if none of the inputs changed: fiber has no `StyleDirty`, CSSS registry version unchanged, and props hash unchanged. Only needs to check `StyleRegistry.Version` (already exists) and `Props.GetHashCode`.

### Expected Gains

| Scenario | Before | After |
|---|---|---|
| Playhead position update (1 fiber dirty) | Full tree | ~1 fiber + ancestors |
| Meter bar height change (N fibers dirty) | Full tree | N fibers + union dirty rect |
| Completely idle frame | Full tree if any animation | Skip layout + draw entirely |

---

## Track 2 — Low-Level Custom Draw API

**Goal:** Let DAW components (waveform editor, piano roll, spectrum analyser) issue arbitrary draw commands — including raw OpenGL — that compose correctly within the Paper fiber tree (clipping, z-index, opacity, layout).

### Current Situation

`ICanvas2DContext` + `Canvas2D` exist and handle simple primitives (lines, rects, text). The limitation is:
- No mesh/indexed geometry
- No texture upload or custom shaders
- No access to batch internals (can't issue a waveform as 10,000 rects efficiently)
- No framebuffer/render-target (can't pre-render a waveform and composite it)

### Design

#### Layer 1 — Enriched ICanvas2DContext

**File:** `Paper.Core/VirtualDom/ICanvas2DContext.cs`

Extend the existing interface (keep all current methods, add):

```csharp
public interface ICanvas2DContext {
    // Existing...

    // Batch-efficient primitives (good for meters, grids)
    void DrawRects(ReadOnlySpan<PaperRect> rects);   // bulk instanced rects
    void DrawLines(ReadOnlySpan<PaperLine> lines);   // bulk anti-aliased lines

    // Texture drawing (waveform images, spectrogram tiles)
    void DrawTexture(TextureHandle texture, float x, float y, float w, float h);
    void DrawTexture(TextureHandle texture, float x, float y, float w, float h,
                     float u0, float v0, float u1, float v1);

    // Clip / transform (for zoomed views)
    void PushClip(float x, float y, float w, float h);
    void PopClip();
    void PushTransform(float translateX, float translateY, float scaleX, float scaleY);
    void PopTransform();

    // Metrics
    float Width { get; }
    float Height { get; }
    float DpiScale { get; }
}
```

`PaperRect` and `PaperLine` are simple `readonly struct` value types — no heap allocation when passed as `Span<T>`.

**File:** `Paper.Rendering.Silk.NET/Canvas2DContext.cs`

Implement the new methods by forwarding to the existing `RectBatch` and `LineBatch`. For `DrawRects`, call `_rects.Add(...)` in a loop — the batch already handles the instanced upload.

#### Layer 2 — TextureHandle (Managed GPU Textures)

**File:** `Paper.Core/Rendering/TextureHandle.cs` (new)

```csharp
public sealed class TextureHandle : IDisposable {
    // Opaque — user does not touch the GL handle
}
```

**File:** `Paper.Core/Rendering/ITextureFactory.cs` (new)

```csharp
public interface ITextureFactory {
    TextureHandle CreateRgba(int width, int height, ReadOnlySpan<byte> pixels);
    TextureHandle CreateRgba(int width, int height); // empty, update later
    void UpdateRegion(TextureHandle texture, int x, int y, int w, int h,
                      ReadOnlySpan<byte> pixels);
    void Dispose(TextureHandle texture);
}
```

`Paper.Rendering.Silk.NET` implements `ITextureFactory` using `GL.TexImage2D` / `GL.TexSubImage2D`. The DAW can create a texture once per clip, update the dirty region when audio changes, and draw it every frame with `DrawTexture`.

This is the key primitive for streaming waveform display: render waveform pixels to CPU buffer off the audio/background thread, call `UpdateRegion` from the UI thread (or queue the update), draw with `DrawTexture` in the Canvas2D callback.

#### Layer 3 — Raw Draw Escape Hatch

For components that genuinely need arbitrary OpenGL (custom GLSL shader for FFT spectrum, etc.):

**File:** `Paper.Core/VirtualDom/ICanvas2DContext.cs`

```csharp
// Opt-in escape hatch, only available at runtime via downcast or via
// a new UI.RawDraw() element type
public interface IRawDrawContext {
    int GlTextureId { get; }     // bind target for FBO reads
    float ScreenScaleX { get; }
    float ScreenScaleY { get; }
    void FlushPendingBatches();  // ensure Paper's batches are flushed before raw GL
    void RestorePaperGLState();  // call after raw GL to restore Paper's state
}
```

**File:** `Paper.Rendering.Silk.NET/FiberRenderer.Render.cs`

In the element-type switch, handle `ElementTypes.RawDraw`:
```csharp
case ElementTypes.RawDraw: {
    _rects.Flush(_screenW, _screenH);
    _lines?.Flush(_screenW, _screenH);
    _text?.Flush(_screenW, _screenH);
    var ctx = new RawDrawContext(_gl, _screenW, _screenH, _dpiScale);
    fiber.Props.Get<Action<IRawDrawContext>>("rawDraw")?.Invoke(ctx);
    RestoreGLState(); // rebind VAOs, re-enable blending etc.
    break;
}
```

`UI.RawDraw(Action<IRawDrawContext> draw, StyleSheet? style)` is the factory.

### Data Binding to Custom Draw Components

The natural Paper pattern already works: Canvas2D captures state via closure.

```csharp
var (zoom, setZoom) = UseState(1.0f);
var waveformTex = UseRef<TextureHandle?>(null);

// Load/update texture when clip changes
UseEffect(() => {
    waveformTex.Current = textureFactory.CreateRgba(w, h, clipPixels);
    return () => waveformTex.Current?.Dispose();
}, new[] { clipId });

// Draw every frame (closure captures zoom, waveformTex)
return UI.Canvas2D(ctx => {
    if (waveformTex.Current is { } tex)
        ctx.DrawTexture(tex, 0, 0, ctx.Width, ctx.Height);
    ctx.DrawLine(zoom * playheadX, 0, zoom * playheadX, ctx.Height,
                 Colors.White, 1.5f);
}, style);
```

When `setZoom` is called, Paper reconciles, the Canvas2D callback closure captures the new `zoom` value, and FiberRenderer calls it during render. Zero special binding machinery needed — it's just closures over component state.

For continuous updates (moving playhead at 60 fps), use `UseLayoutEffect` or drive updates from the audio engine via `RenderScheduler.RequestRender()` (already thread-safe).

---

## Track 3 — Threading Model

**Goal:** Keep the audio engine on a hard-realtime thread, allow background work (file I/O, audio decode, analysis) to post results to the UI thread safely, and parallelise layout where it buys something.

### Current Architecture

Everything runs on one thread: events → reconcile → style → layout → draw.

The only existing thread-safe surface is `HookSlot.EnqueueStateUpdater()` + `RenderScheduler.RequestRender()`, which correctly wakes the render loop from a background thread.

### What NOT to Do

Do not attempt to parallelise reconciliation or make the fiber tree concurrent. React-like reconcilers are inherently serial due to hook state (each hook slot is positionally matched; running components on multiple threads would require per-component locking with no real gain).

### What to Do

#### 3.1 — Formalise the Thread Boundary

**File:** `Paper.Core/Threading/UiThread.cs` (new)

```csharp
public static class UiThread {
    // Post an action to run on the UI thread before the next reconcile.
    // Safe to call from any thread (audio, disk I/O, analysis).
    public static void Post(Action action);

    // Post and wait (blocks calling thread until action runs on UI thread).
    public static void Send(Action action);

    [Conditional("DEBUG")]
    public static void AssertOnUiThread();
}
```

Internally this is a `ConcurrentQueue<Action>` drained at the start of each `Canvas.DoFrame()`. This replaces ad-hoc uses of `RenderScheduler.RequestRender()` with a proper dispatch queue.

Background threads (audio analysis, file load) call `UiThread.Post(() => setClipData(result))` — this is the only correct way to update Paper state from off the UI thread.

#### 3.2 — Audio Thread Isolation

The audio engine must never touch any Paper object. The interface is one-directional: audio thread writes to lock-free ring buffers or atomic values; UI thread reads them on its own schedule.

```csharp
// Audio-safe shared state (no GC, no locks in hot path)
public sealed class AudioTransportState {
    public volatile float PlayheadSeconds;   // written by audio, read by UI
    public volatile bool IsPlaying;
    public volatile float[] MeterPeaks;      // one float per channel
}
```

UI reads this in a `UseEffect` or directly in a Canvas2D draw callback. Audio engine writes it lock-free.

#### 3.3 — Parallel Layout (Optional, Later)

**File:** `Paper.Layout/LayoutEngine.cs`

Flex containers with many children (e.g., a track list with 200 tracks) can measure children in parallel since each child's measurement is independent (no cross-sibling dependency at the measure phase).

```csharp
if (children.Count > ParallelLayoutThreshold) {
    Parallel.ForEach(children, child => MeasureChild(child, availableSize));
} else {
    foreach (var child in children) MeasureChild(child, availableSize);
}
```

The size-pass (where the parent distributes space to children) remains serial. This is a modest win but non-trivial correctness risk — leave it until Tracks 1 and 2 are stable.

#### 3.4 — Background Work Queue

For CPU-heavy work (audio file decode, waveform pixel generation, FFT):

**File:** `Paper.Core/Threading/BackgroundQueue.cs` (new)

```csharp
public static class BackgroundQueue {
    // Run work on thread pool; post result back to UI thread automatically.
    public static Task<T> Run<T>(Func<T> work, Action<T> onComplete);
    public static Task Run(Action work, Action onComplete);
}
```

Implemented as `Task.Run(work).ContinueWith(t => UiThread.Post(() => onComplete(t.Result)))`.

This is a thin wrapper — the real value is the convention: background work always completes by posting to `UiThread`, never by touching Paper state directly.

---

## Track 4 — Canvas2D Performance for Streaming Data

**Goal:** Waveforms, meters, and spectra update every frame with large amounts of geometry. The existing `ICanvas2DContext` flushes batches in the normal FiberRenderer pass, which is fine for moderate loads but needs care for DAW-scale geometry.

### Waveform Strategy

Do **not** draw waveforms as thousands of `DrawLine` calls per frame. Instead:

1. Maintain a `TextureHandle` per audio clip.
2. On audio load / zoom change: compute waveform pixels on a background thread, upload via `ITextureFactory.UpdateRegion` on UI thread.
3. Draw as a single `DrawTexture` call per clip per frame.

For scrolling (the waveform view panning), update only the newly visible pixel columns rather than regenerating the full texture. This is O(scrollDelta * height) pixels per frame instead of O(width * height).

### Meter Strategy

VU/peak meters: 1 rect per channel. With Track 1's dirty rects, only the meter fibers are dirty each frame, so the full tree is not re-drawn. For 64 channels that is 64 rect draw calls per frame — trivially fast.

### Piano Roll Strategy

Horizontal lines (pitch grid): draw once into a `TextureHandle`, tile with UV coordinates.
Notes: draw as rects via `DrawRects(ReadOnlySpan<PaperRect>)` with one bulk call.
Playhead: single `DrawLine`.

---

## Execution Order

| Phase | Track | Key Deliverable |
|---|---|---|
| 1 | Track 1 — Step 1 | `LayoutDirty` / `HasLayoutDirtyDescendant` on Fiber; layout skips clean subtrees |
| 2 | Track 1 — Step 2 | Screen-space dirty rect; draw skips non-dirty fibers |
| 3 | Track 1 — Step 3 | Style resolution cached per fiber |
| 4 | Track 2 — Layer 1 | `DrawRects`, `DrawLines` bulk methods on ICanvas2DContext |
| 5 | Track 2 — Layer 2 | `TextureHandle` + `ITextureFactory`; `DrawTexture` on ICanvas2DContext |
| 6 | Track 3 — 3.1/3.2 | `UiThread.Post`, `AudioTransportState` pattern |
| 7 | Track 2 — Layer 3 | `IRawDrawContext` escape hatch (only if needed) |
| 8 | Track 3 — 3.3 | Parallel child measure in layout (only if profiling shows it helps) |

Phases 1–3 are the highest leverage: they remove the O(tree) tax on every frame and make the framework viable for any continuously-updating UI, not just a DAW.

---

## Files to Touch (Summary)

| File | Change |
|---|---|
| `Paper.Core/VirtualDom/Fiber.cs` | Add `LayoutDirty`, `HasLayoutDirtyDescendant`, `ComputedStyleHash`, `HasActiveAnimation`, `ScreenBounds` |
| `Paper.Layout/LayoutEngine.cs` | Skip clean subtrees; propagate layout dirty |
| `Paper.Core/Styles/StyleResolver.cs` | Cache resolved style hash; skip if inputs unchanged |
| `Paper.Rendering.Silk.NET/FiberRenderer.cs` | Accumulate dirty rect; skip draw for clean fibers; scissor clear |
| `Paper.Core/VirtualDom/ICanvas2DContext.cs` | Add `DrawRects`, `DrawLines`, `DrawTexture`, `PushClip`, `PushTransform` |
| `Paper.Rendering.Silk.NET/Canvas2DContext.cs` | Implement new methods |
| `Paper.Core/Rendering/TextureHandle.cs` | New — opaque GPU texture handle |
| `Paper.Core/Rendering/ITextureFactory.cs` | New — create/update/dispose textures |
| `Paper.Rendering.Silk.NET/GlTextureFactory.cs` | New — GL implementation |
| `Paper.Core/Threading/UiThread.cs` | New — `Post` / `Send` dispatch queue |
| `Paper.Core/Threading/BackgroundQueue.cs` | New — `Run<T>` with UI-thread completion |
| `Paper.Core/VirtualDom/UI.cs` | Add `UI.RawDraw(...)` factory |
| `Canvas.cs` (host) | Drain `UiThread` queue at start of `DoFrame` |
