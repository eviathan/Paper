// Context that carries accent colour to any descendant without prop drilling
var AccentContext = PaperContext.Create("#58a6ff");

function ThemedBadge(props) {
  var accent = Hooks.UseContext(AccentContext);
  return (
    <Box style={{ background: accent, padding: '4px 10px', borderRadius: 12 }}>
        <Text style={{ fontSize: 12, color: '#0d1117' }}>{props.Get<string>("label") ?? ""}</Text>
    </Box>
  );
}

function DemoApp() {
  var (count, setCount, updateCount) = Hooks.UseState(0);
  var (name, setName, _) = Hooks.UseState("Paper");
  var (clicks, setClicks, updateClicks) = Hooks.UseState(0);
  var (usePurple, setUsePurple, _) = Hooks.UseState(false);
  var accent = usePurple ? "#bc8cff" : "#58a6ff";

  // UseReducer demo: todo list
  var (todos, dispatch) = Hooks.UseReducer((List<string> state, string action) => {
    if (action == "add") return state.Concat(new[] { $"Todo #{state.Count + 1}" }).ToList();
    if (action == "clear") return new List<string>();
    return state;
  }, new List<string>());

  return (
    <Box style={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'flex-start',
        height: '100%',
        background: '#0d1117',
        padding: 24,
        gap: 24
    }}>
        <Text style={{ fontSize: 28, color: '#58a6ff', paddingBottom: 8 }}>
            Paper Framework Demo
        </Text>
        <Text style={{ fontSize: 14, color: '#8b949e' }}>
            Welcome, {name}. Clicks: {clicks}
        </Text>

        <Box style={{
            display: 'flex',
            flexDirection: 'column',
            background: '#161b22',
            padding: 20,
            borderRadius: 8,
            gap: 12
        }}>
            <Text style={{ fontSize: 18, color: '#c9d1d9' }}>
                Counter: {count}
            </Text>
            <Box style={{ display: 'flex', flexDirection: 'row', gap: 12 }}>
                <Button
                    onClick={() => { updateCount(prev => prev + 1); updateClicks(prev => prev + 1); }}
                    style={{ background: '#238636', color: 'white', padding: '8px 16px', borderRadius: 6, fontSize: 14, transition: 'background 0.15s' }}
                    hoverStyle={{ background: '#2ea043' }}
                >
                    +1
                </Button>
                <Button
                    onClick={() => { updateCount(prev => prev - 1); updateClicks(prev => prev + 1); }}
                    style={{ background: '#da3633', color: 'white', padding: '8px 16px', borderRadius: 6, fontSize: 14, transition: 'background 0.15s' }}
                    hoverStyle={{ background: '#f85149' }}
                >
                    -1
                </Button>
                <Button
                    onClick={() => { setCount(0); updateClicks(prev => prev + 1); }}
                    style={{ background: '#30363d', color: 'white', padding: '8px 16px', borderRadius: 6, fontSize: 14, transition: 'background 0.15s' }}
                    hoverStyle={{ background: '#484f58' }}
                >
                    Reset
                </Button>
            </Box>
        </Box>

        <Box style={{
            display: 'flex',
            flexDirection: 'column',
            background: '#161b22',
            padding: 20,
            borderRadius: 8,
            gap: 8,
            width: '100%'
        }}>
            <Text style={{ fontSize: 16, color: '#c9d1d9' }}>
                UseReducer — Todos ({todos.Count})
            </Text>
            {todos.Select((t, i) => UI.Text(t, new StyleSheet { Color = new PaperColour(0.55f, 0.65f, 0.75f, 1f), FontSize = Length.Px(13) }, $"t{i}")).ToArray()}
            <Box style={{ display: 'flex', flexDirection: 'row', gap: 8 }}>
                <Button
                    onClick={() => dispatch("add")}
                    style={{ background: '#1f6feb', color: 'white', padding: '6px 14px', borderRadius: 5, fontSize: 13, transition: 'background 0.15s' }}
                    hoverStyle={{ background: '#388bfd' }}
                >
                    Add Todo
                </Button>
                <Button
                    onClick={() => dispatch("clear")}
                    style={{ background: '#30363d', color: '#8b949e', padding: '6px 14px', borderRadius: 5, fontSize: 13, transition: 'background 0.15s' }}
                    hoverStyle={{ background: '#484f58', color: 'white' }}
                >
                    Clear
                </Button>
            </Box>
        </Box>

        <Box style={{
            display: 'flex',
            flexDirection: 'column',
            background: '#161b22',
            padding: 20,
            borderRadius: 8,
            gap: 12,
            width: '100%'
        }}>
            <Text style={{ fontSize: 16, color: '#c9d1d9' }}>
                UseContext — Accent Theme
            </Text>
            <Text style={{ fontSize: 12, color: '#8b949e' }}>
                Badge reads accent from context — no props passed
            </Text>
            {AccentContext.Provider(accent,
                UI.Component(ThemedBadge, new PropsBuilder().Set("label", "Hello Context").Build()),
                UI.Component(ThemedBadge, new PropsBuilder().Set("label", accent).Build())
            )}
            <Button
                onClick={() => setUsePurple(!usePurple)}
                style={{ background: '#30363d', color: 'white', padding: '6px 14px', borderRadius: 5, fontSize: 13, transition: 'background 0.15s' }}
                hoverStyle={{ background: '#484f58' }}
            >
                Toggle Accent ({(usePurple ? "Purple" : "Blue")})
            </Button>
        </Box>
    </Box>
  );
}
