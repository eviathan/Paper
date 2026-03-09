function ControlsApp() {
  var (check1, setCheck1, _) = Hooks.UseState(false);
  var (check2, setCheck2, _) = Hooks.UseState(true);
  var (inputVal, setInputVal, _) = Hooks.UseState("");
  var (textareaVal, setTextareaVal, _) = Hooks.UseState("Line 1\nLine 2");
  var (radioVal, setRadioVal, _) = Hooks.UseState("two");
  var (selectVal, setSelectVal, _) = Hooks.UseState("two");
  var options = new (string, string)[] { ("one", "One"), ("two", "Two"), ("three", "Three") };
  var listItems = new string[] { "Apple", "Banana", "Cherry" };
  var listNodes = UI.Map(listItems, x => x, x => UI.Box(UI.Text(x, new StyleSheet { Padding = new Thickness(Length.Px(6)), Background = new PaperColour(0.2f, 0.2f, 0.25f, 1f), BorderRadius = 4 })));

  return (
    <Scroll style={{
      display: 'flex',
      flexDirection: 'column',
      height: '100%',
      padding: 24,
      gap: 24,
      background: '#1e293b',
      overflow: 'scroll'
    }}>
      <Box style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <Text style={{ color: '#94a3b8', fontSize: 16 }}>Image</Text>
        <Box style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 12, background: '#334155', borderRadius: 8 }}>
          <Image src="Assets/test.png" style={{ width: 100, height: 80 }} />
          <Text style={{ color: '#94a3b8', fontSize: 12 }}>Placeholder if Assets/test.png missing</Text>
        </Box>
      </Box>

      <Box style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <Text style={{ color: '#94a3b8', fontSize: 16 }}>Checkbox</Text>
        <Box style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 12, background: '#334155', borderRadius: 8 }}>
          <Checkbox checked={check1} onCheckedChange={setCheck1} label="Option A" />
          <Checkbox checked={check2} onCheckedChange={setCheck2} label="Option B" />
        </Box>
      </Box>

      <Box style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <Text style={{ color: '#94a3b8', fontSize: 16 }}>RadioGroup</Text>
        <Box style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 12, background: '#334155', borderRadius: 8 }}>
          <RadioGroup options={options} selectedValue={radioVal} onSelect={setRadioVal} />
        </Box>
      </Box>

      <Box style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <Text style={{ color: '#94a3b8', fontSize: 16 }}>Text input (single line)</Text>
        <Box style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 12, background: '#334155', borderRadius: 8 }}>
          <Input value={inputVal} onChange={setInputVal} style={{ width: 200 }} />
          <Text style={{ color: '#64748b', fontSize: 12 }}>Click here then type</Text>
        </Box>
      </Box>

      <Box style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <Text style={{ color: '#94a3b8', fontSize: 16 }}>Textarea (multi-line)</Text>
        <Box style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 12, background: '#334155', borderRadius: 8 }}>
          <Textarea value={textareaVal} onChange={setTextareaVal} rows={3} style={{ width: 200, minHeight: 60 }} />
          <Text style={{ color: '#64748b', fontSize: 12 }}>Click here then type (Enter for new line)</Text>
        </Box>
      </Box>

      <Box style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <Text style={{ color: '#94a3b8', fontSize: 16 }}>Table</Text>
        <Box style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 12, background: '#334155', borderRadius: 8 }}>
          <Table>
            <TableRow>
              <TableCell><Text style={{ color: '#fff' }}>A1</Text></TableCell>
              <TableCell><Text style={{ color: '#fff' }}>A2</Text></TableCell>
            </TableRow>
            <TableRow>
              <TableCell><Text style={{ color: '#fff' }}>B1</Text></TableCell>
              <TableCell><Text style={{ color: '#fff' }}>B2</Text></TableCell>
            </TableRow>
          </Table>
        </Box>
      </Box>

      <Box style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <Text style={{ color: '#94a3b8', fontSize: 16 }}>List (Scroll + Map)</Text>
        <Box style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 12, background: '#334155', borderRadius: 8 }}>
          <Scroll style={{ maxHeight: 120, overflow: 'scroll' }}>
            <Box style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              { listNodes }
            </Box>
          </Scroll>
        </Box>
      </Box>

      <Box style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <Text style={{ color: '#94a3b8', fontSize: 16 }}>Select (dropdown — click to open, pick option)</Text>
        <Box style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 12, background: '#334155', borderRadius: 8 }}>
          <Select options={options} value={selectVal} onChange={setSelectVal} />
        </Box>
      </Box>
    </Scroll>
  );
}
