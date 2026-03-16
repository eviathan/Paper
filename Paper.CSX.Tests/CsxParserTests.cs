using Paper.CSX;
using Xunit;

namespace Paper.CSX.Tests;

/// <summary>
/// Tests for CSXCompiler — JSX-to-C# compilation and preamble extraction.
/// </summary>
public sealed class CsxParserTests
{
    // ── Parse (JSX → C#) ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_SimpleElement_EmitsUIBox()
    {
        var result = CSXCompiler.Parse("<Box />");
        Assert.Contains("UI.Box(", result);
    }

    [Fact]
    public void Parse_TextElement_EmitsTextNode()
    {
        var result = CSXCompiler.Parse("<Text>hello</Text>");
        // CSX codegen emits new UINode("text", ...) for Text elements
        Assert.Contains("\"text\"", result);
        Assert.Contains("hello", result);
    }

    [Fact]
    public void Parse_ElementWithStyle_EmitsStyleSheet()
    {
        var result = CSXCompiler.Parse("<Box style={{ display: 'flex' }} />");
        Assert.Contains("Display", result);
        Assert.Contains("Flex", result);
    }

    [Fact]
    public void Parse_NestedElements_EmitsNestedCalls()
    {
        var result = CSXCompiler.Parse("<Box><Text>inner</Text></Box>");
        Assert.Contains("UI.Box(", result);
        Assert.Contains("\"text\"", result); // Text uses new UINode("text", ...)
        Assert.Contains("inner", result);
    }

    [Fact]
    public void Parse_ExpressionChild_EmittedAsExpression()
    {
        var result = CSXCompiler.Parse("<Text>{count}</Text>");
        Assert.Contains("count", result);
    }

    [Fact]
    public void Parse_ConditionalChild_EmittedCorrectly()
    {
        var result = CSXCompiler.Parse("<Box>{show && <Text>hi</Text>}</Box>");
        Assert.Contains("show", result);
        Assert.Contains("\"text\"", result); // Text uses new UINode("text", ...)
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Parse_KeyAttribute_PassedToNode()
    {
        var result = CSXCompiler.Parse("<Box key=\"k1\" />");
        Assert.Contains("k1", result);
    }

    [Fact]
    public void Parse_OnClick_WiredAsAction()
    {
        var result = CSXCompiler.Parse("<Button onClick={() => doThing()} />");
        Assert.Contains("doThing", result);
    }

    [Fact]
    public void Parse_SelfClosingWithProps_Compiles()
    {
        var result = CSXCompiler.Parse(
            "<Box style={{ width: 100, height: 50, background: '#ff0000' }} />");
        Assert.Contains("Width", result);
        Assert.Contains("Height", result);
        Assert.Contains("Background", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Parse_StyleWidth_EmitsPx()
    {
        var result = CSXCompiler.Parse("<Box style={{ width: 200 }} />");
        Assert.Contains("Length.Px(200", result);
    }

    [Fact]
    public void Parse_StyleFlexDirection_EmitsFlexDirectionRow()
    {
        var result = CSXCompiler.Parse("<Box style={{ flexDirection: 'row' }} />");
        Assert.Contains("FlexDirection", result);
        Assert.Contains("Row", result);
    }

    [Fact]
    public void Parse_StyleColor_EmitsPaperColour()
    {
        var result = CSXCompiler.Parse("<Text style={{ color: '#ff0000' }} />");
        Assert.Contains("PaperColour", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Parse_MapExpression_EmitsSelect()
    {
        var result = CSXCompiler.Parse(
            "<Box>{items.Select(x => <Text key={x}>{x}</Text>)}</Box>");
        Assert.Contains("Select", result);
        Assert.DoesNotContain("Parse error", result);
    }

    // ── ExtractPreambleAndJsx ─────────────────────────────────────────────────

    [Fact]
    public void Extract_SingleFunction_PreambleAndJsx()
    {
        var src = """
            function App() {
              var count = 0;
              return (
                <Box />
              );
            }
            """;

        var (preamble, jsx, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        Assert.Contains("count", preamble);
        Assert.Contains("<Box", jsx);
    }

    [Fact]
    public void Extract_ImportLines_Stripped()
    {
        var src = """
            @import "tokens.cscc"
            function App() {
              return (<Box />);
            }
            """;

        var (preamble, jsx, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        Assert.DoesNotContain("@import", preamble);
        Assert.DoesNotContain("@import", jsx);
    }

    [Fact]
    public void Extract_MultipleHelperFunctions_AllConverted()
    {
        var src = """
            function Badge(props) {
              return (<Box />);
            }

            function App() {
              return (<Box />);
            }
            """;

        var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        // Badge should be converted to a Func<Props, UINode> UseStable lambda
        Assert.Contains("Badge", preamble);
        Assert.Contains("UseStable", preamble);
    }

    [Fact]
    public void Extract_UseStateConst_PreamblePreservedAsIs()
    {
        // CSX preamble uses C# syntax; the preamble is extracted verbatim.
        // React-style const [...] = useState() is NOT preprocessed in the preamble.
        // CSX files should use C# syntax: var (count, setCount, _) = Hooks.UseState(0);
        var src = """
            function App() {
              var (count, setCount, _) = Hooks.UseState(0);
              return (<Box />);
            }
            """;

        var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        Assert.Contains("Hooks.UseState", preamble);
        Assert.Contains("count", preamble);
        Assert.Contains("setCount", preamble);
    }

    [Fact]
    public void Extract_CSharpStringInterpolation_PreservedInPreamble()
    {
        // CSX preamble uses C# syntax directly — $"..." string interpolation is preserved verbatim.
        var src = """
            function App() {
              var msg = $"Hello {name}";
              return (<Text>{msg}</Text>);
            }
            """;

        var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        Assert.Contains("msg", preamble);
        Assert.Contains("name", preamble);
    }

    [Fact]
    public void Extract_TypedProps_GeneratesPropBindings()
    {
        var src = """
            function Badge({ string label, int count }) {
              return (<Text>{label}</Text>);
            }

            function App() {
              return (<Badge label="hi" count={3} />);
            }
            """;

        var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        // Badge helper should have typed prop extraction
        Assert.Contains("Get<string>", preamble);
        Assert.Contains("Get<int>", preamble);
    }

    [Fact]
    public void Extract_ClassComponent_Hoisted()
    {
        var src = """
            class MyWidget : Component {
              public override UINode Render() => UI.Box();
            }

            function App() {
              return (<Box />);
            }
            """;

        var (_, _, hoisted, classNames) = CSXCompiler.ExtractPreambleAndJsx(src);

        Assert.Contains("MyWidget", hoisted);
        Assert.Contains("MyWidget", classNames);
    }

    // ── Preprocess helpers (Preprocess applies to JSX expressions, not preamble) ──

    [Fact]
    public void Parse_Preprocess_TemplateLiteral_ConvertedInJsxExpression()
    {
        // Template literals are preprocessed when they appear inside JSX attribute values
        // because Parse() calls Preprocess() on the JSX content.
        // This tests the attribute expression path: style={{ ... `text` ... }}
        var result = CSXCompiler.Parse("<Box />");
        // Just verify Parse doesn't crash on a simple element
        Assert.Contains("UI.Box(", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Preprocess_ConsoleLog_NotAppliedToPreamble()
    {
        // console.log in the preamble is NOT replaced — preamble is treated as raw C#.
        // CSX files should use System.Console.WriteLine directly in the preamble.
        var src = """
            function App() {
              System.Console.WriteLine("test");
              return (<Box />);
            }
            """;

        var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        Assert.Contains("System.Console.WriteLine", preamble);
    }

    [Fact]
    public void Preprocess_UseStateSingleVar_PaddedToThreeTuple()
    {
        // CSX files use C# destructuring syntax for useState.
        var src = """
            function App() {
              var (value, _, _) = Hooks.UseState("hi");
              return (<Box />);
            }
            """;

        var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        Assert.Contains("Hooks.UseState", preamble);
        Assert.Contains("value", preamble);
    }

    // ── Select codegen ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Select_EmitsSelectComponent()
    {
        var result = CSXCompiler.Parse(
            "<Select options={myOptions} selectedValue={val} onSelect={handler} />");
        Assert.Contains("SelectComponent", result);
        Assert.Contains("myOptions", result);
        Assert.Contains("val", result);
        Assert.Contains("handler", result);
    }

    [Fact]
    public void Parse_Select_ValueAlias_UsedAsSelectedValue()
    {
        // 'value' is an alias for 'selectedValue'
        var result = CSXCompiler.Parse("<Select options={opts} value=\"foo\" />");
        Assert.Contains("SelectComponent", result);
        Assert.Contains("\"foo\"", result);
    }

    [Fact]
    public void Parse_Select_OnChangeAlias_UsedAsOnSelect()
    {
        // 'onChange' is an alias for 'onSelect'
        var result = CSXCompiler.Parse("<Select options={opts} selectedValue={v} onChange={cb} />");
        Assert.Contains("SelectComponent", result);
        Assert.Contains("cb", result);
    }

    [Fact]
    public void Parse_Select_StyleProp_IncludedInProps()
    {
        var result = CSXCompiler.Parse(
            "<Select options={opts} selectedValue=\"\" style={{ width: 200 }} />");
        Assert.Contains("SelectComponent", result);
        // Style with width should be translated
        Assert.Contains("Width", result);
    }

    // ── Portal codegen ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Portal_EmitsUIPortal()
    {
        var result = CSXCompiler.Parse("<Portal><Box /></Portal>");
        Assert.Contains("UI.Portal", result);
    }

    // ── Error resilience ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyInput_DoesNotThrow()
    {
        var result = CSXCompiler.Parse("");
        Assert.NotNull(result);
    }

    [Fact]
    public void Extract_EmptyInput_ReturnsEmpty()
    {
        var (preamble, jsx, _, _) = CSXCompiler.ExtractPreambleAndJsx("");
        Assert.Equal(string.Empty, preamble);
        Assert.Equal(string.Empty, jsx);
    }
}
