@import "App.csss"

function App() {
  List<string> test = ["test"];

  return (
    <Box className="root">
      {/* 1. Row: flexGrow (equal vs weighted) */}
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
          <Box style={{ width: 70, height: 36, background: '#f59e0b' }} />
          <Box style={{ width: 70, height: 36, background: '#d97706' }} />
          <Box style={{ width: 70, height: 36, background: '#b45309' }} />
          <Box style={{ width: 70, height: 36, background: '#92400e' }} />
          <Box style={{ width: 70, height: 36, background: '#78350f' }} />
          <Box style={{ width: 70, height: 36, background: '#f59e0b' }} />
          <Box style={{ width: 70, height: 36, background: '#d97706' }} />
          <Box style={{ width: 70, height: 36, background: '#b45309' }} />
          <Box style={{ width: 70, height: 36, background: '#92400e' }} />
          <Box style={{ width: 70, height: 36, background: '#78350f' }} />
          <Box style={{ width: 70, height: 36, background: '#f59e0b' }} />
          <Box style={{ width: 70, height: 36, background: '#d97706' }} />
          <Box style={{ width: 70, height: 36, background: '#b45309' }} />
          <Box style={{ width: 70, height: 36, background: '#92400e' }} />
          <Box style={{ width: 70, height: 36, background: '#78350f' }} />
          <Box style={{ width: 70, height: 36, background: '#f59e0b' }} />
          <Box style={{ width: 70, height: 36, background: '#d97706' }} />
          <Box style={{ width: 70, height: 36, background: '#b45309' }} />
          <Box style={{ width: 70, height: 36, background: '#92400e' }} />
          <Box style={{ width: 70, height: 36, background: '#78350f' }} />
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
              <Box key={$"swatch-{i}"} style={{ width: 36, height: 36, background: color }} />
            )
          }
        </Box>
      </Box>

      {/* 7. Column: fixed header + flexGrow - minHeight so the 120px example is not cropped when window is small */}
      <Box className="section" style={{ flexGrow: 1, minHeight: 170 }}>
        <Text className="section-label">Column: fixed header/footer + flexGrow middle</Text>
        <Box className="demo-col-panel" style={{ gap: 6, height: 120, padding: 8, flexGrow: 1, minHeight: 20 }}>
          <Box style={{ height: 28, background: '#dc2626' }} />
          <Box className="demo-item" style={{ flexGrow: 1, background: '#2563eb', minHeight: 20 }} />
          <Box style={{ height: 28, background: '#16a34a' }} />
        </Box>
      </Box>

      {/* 8. Virtualized List — 200 items, only visible rows rendered */}
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
    </Box>
  );
}
