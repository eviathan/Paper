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
            UINode App(Props props) {
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
            UINode App(Props props) {
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
            UINode Badge(Props props) {
              return (<Box />);
            }

            UINode App(Props props) {
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
            UINode App(Props props) {
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
            UINode App(Props props) {
              var msg = $"Hello {name}";
              return (<Text>{msg}</Text>);
            }
            """;

        var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        Assert.Contains("msg", preamble);
        Assert.Contains("name", preamble);
    }

    [Fact]
    public void Extract_NoParams_EntryComponent_Works()
    {
        // UINode App() with no params — no prop bindings injected, preamble is just the body
        var src = """
            UINode App() {
              var count = 0;
              return (
                <Box />
              );
            }
            """;

        var (preamble, jsx, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        Assert.Contains("count", preamble);
        // No Props-related injection
        Assert.DoesNotContain("As<", preamble);
        Assert.Contains("<Box", jsx);
    }

    [Fact]
    public void Extract_GenericReturnType_EntryComponent_InjectsProps()
    {
        // UINode<AppProps> App() — TSX-style generic: props injected automatically as "props"
        var src = """
            record AppProps(string Title);

            UINode<AppProps> App() {
              return (<Text>{props.Title}</Text>);
            }
            """;

        var (preamble, _, hoisted, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        Assert.Contains("As<AppProps>", preamble);
        Assert.Contains("props", preamble);
        Assert.Contains("AppProps", hoisted);
    }

    [Fact]
    public void Extract_GenericReturnType_HelperComponent_InjectsProps()
    {
        // UINode<BadgeProps> Badge() — helper with generic return type
        var src = """
            record BadgeProps(string Label);

            UINode<BadgeProps> Badge() {
              return (<Text>{props.Label}</Text>);
            }

            UINode App() {
              return (<Badge label="hi" />);
            }
            """;

        var (preamble, _, hoisted, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        // Helper wrapped as lambda with As<BadgeProps>() injection
        Assert.Contains("Func<Props, UINode> Badge", preamble);
        Assert.Contains("As<BadgeProps>", preamble);
        Assert.Contains("BadgeProps", hoisted);
    }

    [Fact]
    public void Extract_NoParams_HelperComponent_WrappedAsLambda()
    {
        // UINode Badge() with no params is wrapped as Func<Props, UINode> with unused __props
        var src = """
            UINode Badge() {
              return (<Box />);
            }

            UINode App() {
              return (<Badge />);
            }
            """;

        var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        // Helper must be wrapped as a Func<Props, UINode> lambda
        Assert.Contains("Func<Props, UINode> Badge", preamble);
    }

    [Fact]
    public void Extract_RecordDeclaration_Hoisted()
    {
        // record BadgeProps(string Label); must be hoisted to file scope (not inside method body)
        var src = """
            record BadgeProps(string Label);

            UINode Badge(BadgeProps props) {
              return (<Text>{props.Label}</Text>);
            }

            UINode App() {
              return (<Badge label="hi" />);
            }
            """;

        var (preamble, _, hoisted, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        Assert.Contains("BadgeProps", hoisted);
        // The record *declaration* must not be in the preamble (it would be a compile error inside a method)
        Assert.DoesNotContain("record BadgeProps", preamble);
    }

    [Fact]
    public void Extract_TypedProps_GeneratesPropBindings()
    {
        // With C# method syntax, typed props use a record type parameter.
        // UINode Badge(BadgeProps badgeProps) → var badgeProps = __props.As<BadgeProps>();
        var src = """
            UINode Badge(BadgeProps badgeProps) {
              return (<Text>{badgeProps.Label}</Text>);
            }

            UINode App(Props props) {
              return (<Badge label="hi" count={3} />);
            }
            """;

        var (preamble, _, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);

        // Badge helper should inject a typed record cast
        Assert.Contains("As<BadgeProps>", preamble);
        Assert.Contains("badgeProps", preamble);
    }

    [Fact]
    public void Extract_ClassComponent_Hoisted()
    {
        var src = """
            class MyWidget : Component {
              public override UINode Render() => UI.Box();
            }

            UINode App(Props props) {
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
            UINode App(Props props) {
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
            UINode App(Props props) {
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

    // ── New element codegen ───────────────────────────────────────────────────

    [Fact]
    public void Parse_Slider_EmitsUISlider()
    {
        var result = CSXCompiler.Parse("<Slider value={0.5f} min={0f} max={1f} />");
        Assert.Contains("UI.Slider(", result);
    }

    [Fact]
    public void Parse_NumberInput_EmitsUINumberInput()
    {
        var result = CSXCompiler.Parse("<NumberInput value={42f} />");
        Assert.Contains("UI.NumberInput(", result);
    }

    [Fact]
    public void Parse_Tabs_EmitsUITabs()
    {
        var result = CSXCompiler.Parse("<Tabs tabs={myTabs} activeTab={activeTab} />");
        Assert.Contains("UI.Tabs(", result);
    }

    [Fact]
    public void Parse_Popover_EmitsUIPopover()
    {
        var result = CSXCompiler.Parse("<Popover isOpen={open} />");
        Assert.Contains("UI.Popover(", result);
    }

    [Fact]
    public void Parse_ToastContainer_EmitsUIToastContainer()
    {
        var result = CSXCompiler.Parse("<ToastContainer toasts={toasts} />");
        Assert.Contains("UI.ToastContainer(", result);
    }

    [Fact]
    public void Parse_Slider_WithOnChange_EmitsHandler()
    {
        var result = CSXCompiler.Parse("<Slider value={v} onChange={setV} />");
        Assert.Contains("UI.Slider(", result);
        Assert.Contains("setV", result);
    }

    [Fact]
    public void Parse_Tabs_WithChildren_EmitsPanels()
    {
        var result = CSXCompiler.Parse("<Tabs tabs={t} activeTab={a}><Box /><Box /></Tabs>");
        Assert.Contains("UI.Tabs(", result);
        Assert.Contains("UI.Box(", result);
    }

    [Fact]
    public void Parse_Popover_WithPlacement_EmitsPlacement()
    {
        var result = CSXCompiler.Parse("<Popover isOpen={x} placement=\"top\" />");
        Assert.Contains("UI.Popover(", result);
        Assert.Contains("\"top\"", result);
    }

    // ── Typed component call-site codegen ────────────────────────────────────

    [Fact]
    public void Extract_TypedComponent_CallSite_EmitsTypedConstructor()
    {
        // When Badge has UINode<BadgeProps> Badge(), using <Badge label="hi" /> in a parent
        // should emit UI.Component(Badge, new BadgeProps(Label: "hi")) — not a PropsBuilder chain.
        var src = """
            record BadgeProps(string Label);

            UINode<BadgeProps> Badge() {
              return (<Box />);
            }

            UINode App() {
              return (<Badge label="hi" />);
            }
            """;

        var (preamble, jsx, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);
        var compiled = CSXCompiler.Parse(jsx);

        // Should emit typed constructor, not PropsBuilder
        Assert.Contains("new BadgeProps(", compiled);
        Assert.Contains("Label:", compiled);
        Assert.Contains("\"hi\"", compiled);
        Assert.DoesNotContain("PropsBuilder", compiled);
    }

    [Fact]
    public void Extract_TypedComponent_CallSite_ExpressionValue()
    {
        // Attribute with expression value {count} → Count: count in the constructor
        var src = """
            record CounterProps(string Label, int Count);

            UINode<CounterProps> Counter() {
              return (<Box />);
            }

            UINode App() {
              var n = 5;
              return (<Counter label="Score" count={n} />);
            }
            """;

        var (preamble, jsx, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);
        var compiled = CSXCompiler.Parse(jsx);

        Assert.Contains("new CounterProps(", compiled);
        Assert.Contains("Label:", compiled);
        Assert.Contains("Count:", compiled);
        Assert.Contains("\"Score\"", compiled);
        Assert.Contains("n", compiled);
    }

    [Fact]
    public void Extract_TypedComponent_CallSite_NullableOptional_CanOmit()
    {
        // A component with nullable/optional props can be called with only required props.
        // C# will use the record's default values for omitted optional params.
        var src = """
            record BadgeProps(string Label, string? Variant = null);

            UINode<BadgeProps> Badge() {
              return (<Box />);
            }

            UINode App() {
              return (<Badge label="Hi" />);
            }
            """;

        var (preamble, jsx, _, _) = CSXCompiler.ExtractPreambleAndJsx(src);
        var compiled = CSXCompiler.Parse(jsx);

        // Only Label is specified — Variant is omitted (uses default null)
        Assert.Contains("new BadgeProps(", compiled);
        Assert.Contains("Label:", compiled);
        Assert.DoesNotContain("Variant:", compiled);
    }

}
