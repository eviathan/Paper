@import "App.csss"

UINode TestDock() {
  var dockLayout = new Paper.Core.Dock.DockState {
    Root = new Paper.Core.Dock.SplitNode {
      Direction = Paper.Core.Dock.DockDirection.Horizontal,
      Ratio = 0.2f,
      First = new Paper.Core.Dock.PanelNode { PanelId = "leftsidebar", Title = "Components" },
      Second = new Paper.Core.Dock.SplitNode {
        Direction = Paper.Core.Dock.DockDirection.Horizontal,
        Ratio = 0.75f,
        First = new Paper.Core.Dock.PanelNode { PanelId = "content", Title = "Content" },
        Second = new Paper.Core.Dock.PanelNode { PanelId = "rightsidebar", Title = "Info" },
      },
    },
  };

  return <Box style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
    <Box style={{ height: 40, background: '#1a1a2e' }}>
      <Text style={{ color: '#c8c8e0', padding: '0 16px' }}>Paper UI</Text>
    </Box>
    <Box style={{ flexGrow: 1, overflow: 'hidden' }}>
      {Paper.Core.Dock.DockContext.Root(
        new Dictionary<string, Func<UINode>> {
          ["leftsidebar"] = () => <Box style={{ padding: 12 }}>
            <Text style={{ color: '#e0e0f0' }}>All Demos</Text>
            <Text style={{ color: '#e0e0f0' }}>Slider</Text>
          </Box>,
          ["content"] = () => <Box style={{ padding: 16 }}>
            <Text style={{ color: '#e0e0f0', fontSize: 20 }}>All Demos</Text>
            <Text style={{ color: '#a0a0b8' }}>Click sidebar buttons to switch</Text>
          </Box>,
          ["rightsidebar"] = () => <Box style={{ padding: 12 }}>
            <Text style={{ color: '#a0a0b8' }}>Version: 1.0.0</Text>
          </Box>,
        },
        dockLayout,
        null,
        null,
        <Box></Box>
      )}
    </Box>
    <Box style={{ height: 28, background: '#1a1a2e' }}>
      <Text style={{ color: '#7878a0', padding: '0 16px' }}>Status: Ready</Text>
    </Box>
  </Box>;
}
