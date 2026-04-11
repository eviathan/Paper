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

  var panels = new Dictionary<string, Func<UINode>> {
    ["leftsidebar"] = () => <Box style={{ padding: 8 }}>
      <Text style={{ color: '#c8c8e0', fontSize: 13, marginBottom: 8 }}>Demos</Text>
      <Box style={{ color: '#7878a0', fontSize: 12 }}>Slider</Box>
      <Box style={{ color: '#7878a0', fontSize: 12 }}>NumberInput</Box>
      <Box style={{ color: '#7878a0', fontSize: 12 }}>Tabs</Box>
      <Box style={{ color: '#7878a0', fontSize: 12 }}>TextInput</Box>
      <Box style={{ color: '#7878a0', fontSize: 12 }}>Checkbox</Box>
      <Box style={{ color: '#7878a0', fontSize: 12 }}>List</Box>
    </Box>,
    ["content"] = () => <Box style={{ padding: 16 }}>
      <Text style={{ color: '#c8c8e0', fontSize: 20, marginBottom: 8 }}>Welcome to Paper UI</Text>
      <Text style={{ color: '#7878a0', fontSize: 13 }}>Drag panel headers to reposition</Text>
      <Text style={{ color: '#7878a0', fontSize: 13 }}>Drag edges to resize</Text>
      <Text style={{ color: '#7878a0', fontSize: 13 }}>Click buttons in headers</Text>
      <Text style={{ color: '#7878a0', fontSize: 13 }}>Tabs create TabGroups</Text>
    </Box>,
    ["rightsidebar"] = () => <Box style={{ padding: 12 }}>
      <Text style={{ color: '#c8c8e0', fontSize: 13, marginBottom: 8 }}>Info</Text>
      <Text style={{ color: '#7878a0', fontSize: 11 }}>Version: 1.0.0</Text>
      <Text style={{ color: '#7878a0', fontSize: 11 }}>Framework: Paper</Text>
    </Box>,
  };

  return <Box style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
    <Box style={{ height: 40, background: '#1a1a2e', display: 'flex', alignItems: 'center', paddingLeft: 16 }}>
      <Text style={{ color: '#c8c8e0', fontSize: 14 }}>Paper UI</Text>
    </Box>
    <Box style={{ flexGrow: 1, overflow: 'hidden' }}>
      {Paper.Core.Dock.DockContext.Root(panels, dockLayout)}
    </Box>
    <Box style={{ height: 28, background: '#1a1a2e', display: 'flex', alignItems: 'center', paddingLeft: 16 }}>
      <Text style={{ color: '#7878a0', fontSize: 11 }}>Status: Ready</Text>
    </Box>
  </Box>;
}
