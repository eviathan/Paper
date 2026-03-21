@import "App.csss"

UINode App() {
  List<string> test = [];

  // ── Slider state ─────────────────────────────────────────────────────────
  var (volume, setVolume, _) = Hooks.UseState(40f);
  var (opacity, setOpacity, _) = Hooks.UseState(80f);
  string volText = ((int)volume).ToString();
  string opacText = ((int)opacity).ToString() + "%";

  // ── NumberInput state ─────────────────────────────────────────────────────
  var (qty, setQty, _) = Hooks.UseState(5f);
  var (price, setPrice, _) = Hooks.UseState(9.99f);

  // ── Tabs state ────────────────────────────────────────────────────────────
  var (activeTab, setActiveTab, _) = Hooks.UseState("overview");

  // ── Popover state ─────────────────────────────────────────────────────────
  var (popOpen, setPopOpen, _) = Hooks.UseState(false);
  string popBtnLabel = popOpen ? "Close Popover" : "Open Popover";
  void TogglePop() { setPopOpen(!popOpen); }

  // ── Toast state ───────────────────────────────────────────────────────────
  var (toasts, setToasts, updateToasts) = Hooks.UseState(new List<Primitives.ToastEntry> {
    new Primitives.ToastEntry("t0", "App started successfully!", "success", 4f),
    new Primitives.ToastEntry("t1", "Welcome to Paper UI.", "info", 6f),
  });
  void AddToast(string msg, string variant) {
    updateToasts(prev => new List<Primitives.ToastEntry>(prev) {
      new Primitives.ToastEntry("t" + System.Environment.TickCount64.ToString(), msg, variant)
    });
  }
  void DismissToast(string id) {
    // Use functional update so the timer callback always filters against current state,
    // not the stale closure captured at first render.
    updateToasts(prev => prev.Where(t => t.Id != id).ToList());
  }

  // ── TextInput state ───────────────────────────────────────────────────────
  var (inputText, setInputText, _) = Hooks.UseState("Hello Paper!");
  var (passwordText, setPasswordText, _) = Hooks.UseState("");

  // ── Textarea state ────────────────────────────────────────────────────────
  var (textareaText, setTextareaText, _) = Hooks.UseState("Multi-line\ntext goes here.\nEdit me!");

  // ── MarkdownEditor state ──────────────────────────────────────────────────
  var (mdText, setMdText, _) = Hooks.UseState("# Hello Markdown\n\nThis is **bold**, *italic*, and `inline code`.\n\n> Blockquote here\n\n- Item one\n- Item two\n\n```\ncode block\n```\n\n---\n\nPlain prose paragraph.");
  var (mdPreview, setMdPreview, _) = Hooks.UseState(false);
  string editBtnBg = mdPreview ? "#1a1a28" : "#4f46e5";
  string prevBtnBg = mdPreview ? "#4f46e5" : "#1a1a28";
  var mdContent = mdPreview
    ? UI.MarkdownPreview(mdText, new Paper.Core.Styles.StyleSheet { Width = Paper.Core.Styles.Length.Px(480), MinHeight = Paper.Core.Styles.Length.Px(260) })
    : UI.MarkdownEditor(mdText, setMdText, 12, new Paper.Core.Styles.StyleSheet { Width = Paper.Core.Styles.Length.Px(480) });

  // ── Checkbox state ────────────────────────────────────────────────────────
  var (checkA, setCheckA, _) = Hooks.UseState(true);
  var (checkB, setCheckB, _) = Hooks.UseState(false);
  var (checkC, setCheckC, _) = Hooks.UseState(false);

  // ── Radio state ───────────────────────────────────────────────────────────
  var (radioVal, setRadioVal, _) = Hooks.UseState("option1");

  // ── Drag and drop state ───────────────────────────────────────────────────
  var (draggedItem, setDraggedItem, _) = Hooks.UseState<string?>(null);
  var (droppedOn, setDroppedOn, _) = Hooks.UseState<string?>(null);
  string dragStatus = draggedItem != null
    ? ("Dragging: " + draggedItem)
    : (droppedOn != null ? ("Last dropped on: " + droppedOn) : "Drag a card onto the target zone");
  string dropBg = draggedItem != null ? "#1a2a1a" : "#1a1a2a";
  string dropTextColor = draggedItem != null ? "#22c55e" : "#5a5a7a";
  string dropText = draggedItem != null ? "Drop here!" : "Target Zone";

  return (
    <Box className="root">

      {/* ═══════════════════════════════════════════════════════════════════════
          NEW COMPONENTS
          ═══════════════════════════════════════════════════════════════════════ */}

      {/* ── Slider ─────────────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">Slider</Text>
        <Box className="demo-col-panel" style={{ gap: 12, padding: 12 }}>
          <Box style={{ display: 'flex', flexDirection: 'row', gap: 12, alignItems: 'center' }}>
            <Text style={{ color: '#a0a0b8', width: 64 }}>Volume</Text>
            <Slider value={volume} min={0f} max={100f} step={1f} onChange={setVolume} style={{ width: 200 }} />
            <Text style={{ color: '#6366f1', width: 36 }}>{volText}</Text>
          </Box>
          <Box style={{ display: 'flex', flexDirection: 'row', gap: 12, alignItems: 'center' }}>
            <Text style={{ color: '#a0a0b8', width: 64 }}>Opacity</Text>
            <Slider value={opacity} min={0f} max={100f} step={5f} onChange={setOpacity} style={{ width: 200 }} />
            <Text style={{ color: '#6366f1', width: 36 }}>{opacText}</Text>
          </Box>
        </Box>
      </Box>

      {/* ── NumberInput ─────────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">NumberInput</Text>
        <Box className="demo-col-panel" style={{ gap: 12, padding: 12 }}>
          <Box style={{ display: 'flex', flexDirection: 'row', gap: 12, alignItems: 'center' }}>
            <Text style={{ color: '#a0a0b8', width: 80 }}>Quantity</Text>
            <NumberInput value={qty} min={1f} max={99f} step={1f} onChange={setQty} />
          </Box>
          <Box style={{ display: 'flex', flexDirection: 'row', gap: 12, alignItems: 'center' }}>
            <Text style={{ color: '#a0a0b8', width: 80 }}>Price ($)</Text>
            <NumberInput value={price} min={0f} step={0.5f} onChange={setPrice} />
          </Box>
        </Box>
      </Box>

      {/* ── Tabs ────────────────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">Tabs</Text>
        <Tabs
          tabs={new[] { ("overview", "Overview"), ("details", "Details"), ("logs", "Logs") }}
          activeTab={activeTab}
          onTabChange={setActiveTab}
        >
          <Box style={{ padding: 16, background: '#334155' }}>
            <Text style={{ color: '#a0a0b8' }}>Overview panel — high-level summary goes here.</Text>
          </Box>
          <Box style={{ padding: 16, background: '#334155' }}>
            <Text style={{ color: '#a0a0b8' }}>Details panel — in-depth information goes here.</Text>
          </Box>
          <Box style={{ padding: 16, background: '#334155' }}>
            <Text style={{ color: '#a0a0b8' }}>Logs panel — recent activity listed here.</Text>
          </Box>
        </Tabs>
      </Box>

      {/* ── Popover ─────────────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">Popover</Text>
        <Box className="demo-panel" style={{ padding: 16, minHeight: 140 }}>
          <Popover isOpen={popOpen} onClose={TogglePop} placement="bottom">
            <Button onClick={TogglePop}>{popBtnLabel}</Button>
            <Box style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 8, minWidth: 200 }}>
              <Text style={{ color: '#e0e0f0', fontWeight: '600' }}>Popover Title</Text>
              <Text style={{ color: '#a0a0b8' }}>Floats anchored to the trigger button.</Text>
              <Button onClick={TogglePop}>Dismiss</Button>
            </Box>
          </Popover>
        </Box>
      </Box>

      {/* ── Toast ───────────────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">Toast Notifications (two shown on start)</Text>
        <Box className="demo-panel" style={{ gap: 8, padding: 12 }}>
          <Button onClick={() => AddToast("Operation succeeded!", "success")}>Success</Button>
          <Button onClick={() => AddToast("Something went wrong.", "error")}>Error</Button>
          <Button onClick={() => AddToast("Heads up!", "info")}>Info</Button>
          <Button onClick={() => AddToast("Proceed with caution.", "warning")}>Warning</Button>
        </Box>
      </Box>

      {/* ── FontStyle & TextTransform ────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">FontStyle & TextTransform</Text>
        <Box className="demo-col-panel" style={{ gap: 10, padding: 12 }}>
          <Text style={{ color: '#e0e0f0', fontStyle: 'italic' }}>Italic — fontStyle: italic</Text>
          <Text style={{ color: '#e0e0f0', textTransform: 'uppercase' }}>uppercase text</Text>
          <Text style={{ color: '#e0e0f0', textTransform: 'lowercase' }}>LOWERCASE TEXT</Text>
          <Text style={{ color: '#e0e0f0', textTransform: 'capitalize' }}>capitalize every word</Text>
        </Box>
      </Box>

      {/* ── AspectRatio ─────────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">AspectRatio</Text>
        <Box className="demo-panel" style={{ gap: 12, padding: 12, alignItems: 'flex-start' }}>
          <Box style={{ width: 160, aspectRatio: '16/9', background: '#6366f1', borderRadius: 6, alignItems: 'center', justifyContent: 'center' }}>
            <Text style={{ color: 'white' }}>16:9</Text>
          </Box>
          <Box style={{ width: 80, aspectRatio: '1/1', background: '#22c55e', borderRadius: 6, alignItems: 'center', justifyContent: 'center' }}>
            <Text style={{ color: 'white' }}>1:1</Text>
          </Box>
          <Box style={{ width: 60, aspectRatio: '9/16', background: '#f97316', borderRadius: 6, alignItems: 'center', justifyContent: 'center' }}>
            <Text style={{ color: 'white', fontSize: 10 }}>9:16</Text>
          </Box>
        </Box>
      </Box>

      {/* ── Drag and Drop ───────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">Drag and Drop</Text>
        <Box className="demo-col-panel" style={{ gap: 12, padding: 12 }}>
          <Text style={{ color: '#a0a0b8' }}>{dragStatus}</Text>
          <Box style={{ display: 'flex', flexDirection: 'row', gap: 8 }}>
            {new[] { "Card A", "Card B", "Card C" }.Select(label =>
              <Box
                key={label}
                onDragStart={(e) => setDraggedItem(label)}
                onDragEnd={(e) => setDraggedItem(null)}
                style={{ padding: 10, background: '#2a2a3a', borderRadius: 6, cursor: 'pointer', border: '1px solid #3a3a55' }}
              >
                <Text style={{ color: '#e0e0f0' }}>{label}</Text>
              </Box>
            )}
          </Box>
          <Box
            onDrop={(e) => { setDroppedOn(draggedItem); setDraggedItem(null); }}
            onDragOver={(e) => {}}
            style={{ width: 160, height: 64, background: dropBg, border: '2px dashed #3a3a55', borderRadius: 8, alignItems: 'center', justifyContent: 'center' }}
          >
            <Text style={{ color: dropTextColor }}>{dropText}</Text>
          </Box>
        </Box>
      </Box>

      {/* ── TextInput ───────────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">TextInput</Text>
        <Box className="demo-col-panel" style={{ gap: 12, padding: 12 }}>
          <Box style={{ display: 'flex', flexDirection: 'row', gap: 12, alignItems: 'center' }}>
            <Text style={{ color: '#a0a0b8', width: 80 }}>Text</Text>
            <Input value={inputText} onChange={setInputText} style={{ width: 220 }} />
          </Box>
          <Box style={{ display: 'flex', flexDirection: 'row', gap: 12, alignItems: 'center' }}>
            <Text style={{ color: '#a0a0b8', width: 80 }}>Second</Text>
            <Input value={passwordText} onChange={setPasswordText} style={{ width: 220 }} />
          </Box>
          <Box style={{ display: 'flex', flexDirection: 'row', gap: 12, alignItems: 'center' }}>
            <Text style={{ color: '#a0a0b8', width: 80 }}>Value:</Text>
            <Text style={{ color: '#6366f1' }}>{inputText}</Text>
          </Box>
        </Box>
      </Box>

      {/* ── Textarea ─────────────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">Textarea</Text>
        <Box className="demo-col-panel" style={{ gap: 12, padding: 12 }}>
          <Textarea value={textareaText} onChange={setTextareaText} rows={3} style={{ width: 320 }} />
          <Box style={{ display: 'flex', flexDirection: 'row', gap: 8, alignItems: 'center' }}>
            <Text style={{ color: '#a0a0b8' }}>Lines:</Text>
            <Text style={{ color: '#6366f1' }}>{textareaText.Split('\n').Length.ToString()}</Text>
          </Box>
        </Box>
      </Box>

      {/* ── MarkdownEditor ───────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">Markdown Editor</Text>
        <Box className="demo-col-panel" style={{ gap: 10, padding: 12 }}>
          {/* Toggle toolbar */}
          <Box style={{ display: 'flex', flexDirection: 'row', gap: 6 }}>
            <Button
              onClick={() => setMdPreview(false)}
              style={{ padding: '4px 14px', background: editBtnBg, color: 'white', borderRadius: 4, border: '1px solid #4f46e5' }}>
              Edit
            </Button>
            <Button
              onClick={() => setMdPreview(true)}
              style={{ padding: '4px 14px', background: prevBtnBg, color: 'white', borderRadius: 4, border: '1px solid #4f46e5' }}>
              Preview
            </Button>
          </Box>
          {mdContent}
          <Text style={{ color: '#a0a0b8', fontSize: 12 }}>{$"{mdText.Length} chars · {mdText.Split('\n').Length} lines"}</Text>
        </Box>
      </Box>

      {/* ── Checkboxes ───────────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">Checkboxes</Text>
        <Box className="demo-col-panel" style={{ gap: 10, padding: 12 }}>
          <Checkbox checked={checkA} onCheckedChange={setCheckA} label="Enable notifications" />
          <Checkbox checked={checkB} onCheckedChange={setCheckB} label="Dark mode" />
          <Checkbox checked={checkC} onCheckedChange={setCheckC} label="Auto-save" />
          <Text style={{ color: '#a0a0b8', fontSize: 12 }}>
            {$"Notifications: {(checkA ? "on" : "off")}  Dark mode: {(checkB ? "on" : "off")}"}
          </Text>
        </Box>
      </Box>

      {/* ── Radio Buttons ────────────────────────────────────────────────────── */}
      <Box className="section">
        <Text className="section-label">Radio Buttons</Text>
        <Box className="demo-col-panel" style={{ gap: 12, padding: 12 }}>
          <RadioGroup
            options={new[] { ("option1", "Option One"), ("option2", "Option Two"), ("option3", "Option Three") }}
            selectedValue={radioVal}
            onSelect={setRadioVal}
          />
          <Text style={{ color: '#6366f1', fontSize: 13 }}>{$"Selected: {radioVal}"}</Text>
        </Box>
      </Box>

      {/* ═══════════════════════════════════════════════════════════════════════
          LAYOUT DEMOS
          ═══════════════════════════════════════════════════════════════════════ */}

      {/* 1. flexGrow (equal vs weighted) */}
      <Box className="section">
        <Text className="section-label">flexGrow (1, 2, 1)</Text>
        <Box className="demo-panel" style={{ gap: 8, height: 56 }}>
          <Box className="demo-item" style={{ flexGrow: 1, background: '#ef4444' }}><Text className="white-text">1</Text></Box>
          <Box className="demo-item" style={{ flexGrow: 2, background: '#f97316' }}><Text className="white-text">2</Text></Box>
          <Box className="demo-item" style={{ flexGrow: 1, background: '#22c55e' }}><Text className="white-text">1</Text></Box>
        </Box>
      </Box>

      {/* 2. justifyContent */}
      <Box className="section">
        <Text className="section-label">justifyContent: space-between</Text>
        <Box className="demo-panel" style={{ justifyContent: 'space-between', gap: 8, height: 48 }}>
          <Box style={{ width: 60, minWidth: 60, height: 48, background: '#6366f1' }} />
          <Box style={{ width: 60, minWidth: 60, height: 48, background: '#8b5cf6' }} />
          <Box style={{ width: 60, minWidth: 60, height: 48, background: '#a855f7' }} />
        </Box>
      </Box>
      <Box className="section">
        <Text className="section-label">justifyContent: center</Text>
        <Box className="demo-panel" style={{ justifyContent: 'center', gap: 8, height: 48 }}>
          <Box style={{ width: 60, minWidth: 60, height: 48, background: '#ec4899' }} />
          <Box style={{ width: 60, minWidth: 60, height: 48, background: '#f43f5e' }} />
        </Box>
      </Box>
      <Box className="section">
        <Text className="section-label">justifyContent: space-around</Text>
        <Box className="demo-panel" style={{ justifyContent: 'space-around', height: 48 }}>
          <Box style={{ width: 50, minWidth: 50, height: 48, background: '#14b8a6' }} />
          <Box style={{ width: 50, minWidth: 50, height: 48, background: '#06b6d4' }} />
        </Box>
      </Box>

      {/* 3. alignItems + alignSelf */}
      <Box className="section">
        <Text className="section-label">alignItems: stretch (default)</Text>
        <Box className="demo-panel" style={{ alignItems: 'stretch', gap: 8, height: 64 }}>
          <Box style={{ width: 40, background: '#eab308' }} />
          <Box style={{ width: 40, background: '#84cc16', alignSelf: 'center', minHeight: 32 }} />
          <Box style={{ width: 40, background: '#65a30d' }} />
        </Box>
      </Box>
      <Box className="section">
        <Text className="section-label">alignItems: center + alignSelf: flex-end on middle</Text>
        <Box className="demo-panel" style={{ alignItems: 'center', gap: 8, height: 64 }}>
          <Box style={{ width: 50, height: 40, background: '#0ea5e9' }} />
          <Box style={{ width: 50, height: 24, background: '#38bdf8', alignSelf: 'flex-end' }} />
          <Box style={{ width: 50, height: 40, background: '#0284c7' }} />
        </Box>
      </Box>

      {/* 4. flex-wrap */}
      <Box className="section">
        <Text className="section-label">flexWrap: wrap + gap</Text>
        <Box className="demo-panel" style={{ flexWrap: 'wrap', gap: 8, padding: 8, minHeight: 80 }}>
          {new[] { "#f59e0b", "#d97706", "#b45309", "#92400e", "#78350f",
                   "#f59e0b", "#d97706", "#b45309", "#92400e", "#78350f",
                   "#f59e0b", "#d97706", "#b45309", "#92400e", "#78350f",
                   "#f59e0b", "#d97706", "#b45309", "#92400e", "#78350f" }
            .Select((c, i) => <Box key={i.ToString()} style={{ width: 70, height: 36, background: c }} />)
          }
        </Box>
      </Box>

      {/* 5. row-reverse */}
      <Box className="section">
        <Text className="section-label">flexDirection: row-reverse</Text>
        <Box className="demo-panel" style={{ flexDirection: 'row-reverse', gap: 8, height: 44 }}>
          <Box className="demo-item" style={{ width: 64, background: '#7c3aed' }}><Text className="white-text text-center">A</Text></Box>
          <Box className="demo-item" style={{ width: 64, background: '#6d28d9' }}><Text className="white-text text-center">B</Text></Box>
          <Box className="demo-item" style={{ width: 64, background: '#5b21b6' }}><Text className="white-text text-center">C</Text></Box>
        </Box>
      </Box>

      {/* 6. Lambda JSX — map a list to JSX elements inline */}
      <Box className="section">
        <Text className="section-label">Lambda JSX — mapping array items to elements</Text>
        <Box className="demo-panel" style={{ flexWrap: 'wrap', gap: 6, padding: 8, minHeight: 40 }}>
          {new[] { "#ef4444", "#f97316", "#eab308", "#22c55e", "#06b6d4", "#6366f1", "#a855f7", "#ec4899" }
            .Select((color, i) =>
              <Box key={i.ToString()} style={{ width: 36, height: 36, background: color }} />
            )
          }
        </Box>
      </Box>

      {/* 7. Column: fixed header + flexGrow */}
      <Box className="section" style={{ minHeight: 170 }}>
        <Text className="section-label">Column: fixed header/footer + flexGrow middle</Text>
        <Box className="demo-col-panel" style={{ gap: 6, padding: 8, flexGrow: 1, minHeight: 120 }}>
          <Box style={{ height: 28, background: '#dc2626' }} />
          <Box className="demo-item" style={{ flexGrow: 1, background: '#2563eb', minHeight: 20 }} />
          <Box style={{ height: 28, background: '#16a34a' }} />
        </Box>
      </Box>

      {/* 8. Virtualized List */}
      <Box className="section">
        <Text className="section-label">UI.List — 200 items, virtualized</Text>
        {UI.List(
          Enumerable.Range(1, 200).ToArray(),
          itemHeight: 36,
          containerH: 180,
          renderItem: (n, i) =>
            <Box key={i.ToString()} style={{ height: 36, padding: 8, background: '#1a1a22', borderBottom: '1px solid #2a2a35' }}>
              <Text style={{ color: '#a0a0b8' }}>{$"Item {n}"}</Text>
            </Box>
        )}
      </Box>

      <Box style={{ minHeight: 48 }} />

      {/* Toast overlay — always rendered in top-right */}
      <ToastContainer toasts={toasts} onDismiss={DismissToast} />

    </Box>
  );
}
