using Paper.Core.Styles;
using Paper.CSSS;
using Xunit;

namespace Paper.CSSS.Tests;

/// <summary>
/// Tests for CSSSCompiler — CSSS source-to-StyleSheet compilation.
/// Tests compilation, variable substitution, nesting, and CSS property mapping.
/// </summary>
public sealed class CsssCompilerTests
{
    // ── Compile ───────────────────────────────────────────────────────────────

    [Fact]
    public void Compile_SimpleRule_MapsSelector()
    {
        var map = CSSSCompiler.Compile(".button { background: #ff0000; }");
        Assert.True(map.ContainsKey(".button"));
    }

    [Fact]
    public void Compile_SimpleRule_SetsBackground()
    {
        var map = CSSSCompiler.Compile(".card { background: #ffffff; }");
        var style = map[".card"];
        Assert.NotNull(style.Background);
        // #ffffff → r=1 g=1 b=1
        Assert.True(Math.Abs(1f - style.Background!.Value.R) < 0.01f);
        Assert.True(Math.Abs(1f - style.Background!.Value.G) < 0.01f);
        Assert.True(Math.Abs(1f - style.Background!.Value.B) < 0.01f);
    }

    [Fact]
    public void Compile_WidthPx_SetsWidth()
    {
        var style = CSSSCompiler.CompileOne(".box { width: 200px; }", ".box");
        Assert.Equal(Length.Px(200), style.Width);
    }

    [Fact]
    public void Compile_HeightPercent_SetsHeight()
    {
        var style = CSSSCompiler.CompileOne(".box { height: 50%; }", ".box");
        Assert.Equal(Length.Percent(50), style.Height);
    }

    [Fact]
    public void Compile_Padding_SetsPadding()
    {
        var style = CSSSCompiler.CompileOne(".box { padding: 16px; }", ".box");
        Assert.NotNull(style.Padding);
    }

    [Fact]
    public void Compile_MarginShorthand_SetsMargin()
    {
        var style = CSSSCompiler.CompileOne(".box { margin: 8px 16px; }", ".box");
        Assert.NotNull(style.Margin);
    }

    [Fact]
    public void Compile_FlexDirection_SetsFlexDirection()
    {
        var style = CSSSCompiler.CompileOne(".row { flex-direction: row; }", ".row");
        Assert.Equal(FlexDirection.Row, style.FlexDirection);
    }

    [Fact]
    public void Compile_Display_SetsDisplay()
    {
        var style = CSSSCompiler.CompileOne(".flex { display: flex; }", ".flex");
        Assert.Equal(Display.Flex, style.Display);
    }

    [Fact]
    public void Compile_DisplayGrid_SetsGrid()
    {
        var style = CSSSCompiler.CompileOne(".grid { display: grid; }", ".grid");
        Assert.Equal(Display.Grid, style.Display);
    }

    [Fact]
    public void Compile_JustifyContent_SetsJustifyContent()
    {
        var style = CSSSCompiler.CompileOne(".c { justify-content: center; }", ".c");
        Assert.Equal(JustifyContent.Center, style.JustifyContent);
    }

    [Fact]
    public void Compile_AlignItems_SetsAlignItems()
    {
        var style = CSSSCompiler.CompileOne(".c { align-items: stretch; }", ".c");
        Assert.Equal(AlignItems.Stretch, style.AlignItems);
    }

    [Fact]
    public void Compile_FlexGrow_SetsFlexGrow()
    {
        var style = CSSSCompiler.CompileOne(".c { flex-grow: 1; }", ".c");
        Assert.Equal(1f, style.FlexGrow);
    }

    [Fact]
    public void Compile_Opacity_SetsOpacity()
    {
        var style = CSSSCompiler.CompileOne(".c { opacity: 0.5; }", ".c");
        Assert.True(Math.Abs(0.5f - (style.Opacity ?? 0f)) < 0.001f);
    }

    [Fact]
    public void Compile_BorderRadius_SetsBorderRadius()
    {
        var style = CSSSCompiler.CompileOne(".c { border-radius: 8px; }", ".c");
        Assert.True(Math.Abs(8f - (style.BorderRadius ?? 0f)) < 0.01f);
    }

    [Fact]
    public void Compile_ZIndex_SetsZIndex()
    {
        var style = CSSSCompiler.CompileOne(".c { z-index: 100; }", ".c");
        Assert.Equal(100, style.ZIndex);
    }

    [Fact]
    public void Compile_FontSize_SetsFontSize()
    {
        var style = CSSSCompiler.CompileOne(".c { font-size: 14px; }", ".c");
        Assert.Equal(Length.Px(14), style.FontSize);
    }

    [Fact]
    public void Compile_FontWeight_SetsFontWeight()
    {
        var style = CSSSCompiler.CompileOne(".c { font-weight: bold; }", ".c");
        Assert.Equal(FontWeight.Bold, style.FontWeight);
    }

    [Fact]
    public void Compile_Color_SetsColor()
    {
        var style = CSSSCompiler.CompileOne(".c { color: #000000; }", ".c");
        Assert.NotNull(style.Color);
        Assert.True(Math.Abs(0f - style.Color!.Value.R) < 0.01f);
    }

    [Fact]
    public void Compile_TextAlign_SetsTextAlign()
    {
        var style = CSSSCompiler.CompileOne(".c { text-align: center; }", ".c");
        Assert.Equal(TextAlign.Center, style.TextAlign);
    }

    [Fact]
    public void Compile_Overflow_SetsOverflowXAndY()
    {
        // StyleSheet.Overflow is init-only (sets OverflowX + OverflowY); read OverflowX to verify.
        var style = CSSSCompiler.CompileOne(".c { overflow: hidden; }", ".c");
        Assert.Equal(Overflow.Hidden, style.OverflowX);
        Assert.Equal(Overflow.Hidden, style.OverflowY);
    }

    [Fact]
    public void Compile_Position_SetsPosition()
    {
        var style = CSSSCompiler.CompileOne(".c { position: absolute; }", ".c");
        Assert.Equal(Position.Absolute, style.Position);
    }

    [Fact]
    public void Compile_Cursor_SetsCursor()
    {
        var style = CSSSCompiler.CompileOne(".c { cursor: pointer; }", ".c");
        Assert.Equal(Cursor.Pointer, style.Cursor);
    }

    [Fact]
    public void Compile_MissingSelector_ReturnsEmpty()
    {
        var style = CSSSCompiler.CompileOne(".button { color: red; }", ".missing");
        Assert.Equal(StyleSheet.Empty, style);
    }

    // ── Variables ($var) ──────────────────────────────────────────────────────

    [Fact]
    public void Compile_Variable_SubstitutedIntoValue()
    {
        const string src = """
            $primary: #0000ff;
            .button { background: $primary; }
            """;
        var style = CSSSCompiler.CompileOne(src, ".button");
        Assert.NotNull(style.Background);
        Assert.True(Math.Abs(0f - style.Background!.Value.R) < 0.01f);
        Assert.True(Math.Abs(0f - style.Background!.Value.G) < 0.01f);
        Assert.True(Math.Abs(1f - style.Background!.Value.B) < 0.01f);
    }

    [Fact]
    public void Compile_Variable_UsedInMultipleRules()
    {
        const string src = """
            $gap: 16px;
            .a { padding: $gap; }
            .b { margin: $gap; }
            """;
        var map = CSSSCompiler.Compile(src);
        Assert.True(map.ContainsKey(".a"));
        Assert.True(map.ContainsKey(".b"));
        Assert.NotNull(map[".a"].Padding);
        Assert.NotNull(map[".b"].Margin);
    }

    // ── Nesting (&) ───────────────────────────────────────────────────────────

    [Fact]
    public void Compile_Nesting_Hover_GeneratesHoverSelector()
    {
        const string src = """
            .button {
              background: #ffffff;
              &:hover { opacity: 0.9; }
            }
            """;
        var map = CSSSCompiler.Compile(src);
        Assert.True(map.ContainsKey(".button"));
        Assert.True(map.ContainsKey(".button:hover"), $"Expected .button:hover, got: {string.Join(", ", map.Keys)}");
        Assert.True(Math.Abs(0.9f - (map[".button:hover"].Opacity ?? 0f)) < 0.01f);
    }

    [Fact]
    public void Compile_Nesting_Child_GeneratesChildSelector()
    {
        const string src = """
            .card {
              background: #fff;
              & > .title { font-weight: bold; }
            }
            """;
        var map = CSSSCompiler.Compile(src);
        Assert.True(map.ContainsKey(".card"), $"Keys: {string.Join(", ", map.Keys)}");
        // Child selector should be present
        var childKey = map.Keys.FirstOrDefault(k => k.Contains(".title"));
        Assert.NotNull(childKey);
    }

    [Fact]
    public void Compile_MultipleSelectors_BothPresent()
    {
        const string src = ".a, .b { color: #ff0000; }";
        var map = CSSSCompiler.Compile(src);
        Assert.True(map.ContainsKey(".a"));
        Assert.True(map.ContainsKey(".b"));
        Assert.NotNull(map[".a"].Color);
        Assert.NotNull(map[".b"].Color);
    }

    // ── FromDeclaration ───────────────────────────────────────────────────────

    [Fact]
    public void FromDeclaration_Width_SetsWidth()
    {
        var style = CSSSCompiler.FromDeclaration("width", "100px");
        Assert.Equal(Length.Px(100), style.Width);
    }

    [Fact]
    public void FromDeclaration_Display_SetsDisplay()
    {
        var style = CSSSCompiler.FromDeclaration("display", "flex");
        Assert.Equal(Display.Flex, style.Display);
    }

    [Fact]
    public void FromDeclaration_Color_SetsColor()
    {
        var style = CSSSCompiler.FromDeclaration("color", "#ff0000");
        Assert.NotNull(style.Color);
        Assert.Equal(1f, style.Color!.Value.R, 2);
    }

    [Fact]
    public void FromDeclaration_UnknownProperty_ReturnsEmpty()
    {
        // Unknown properties are ignored — no crash, just empty
        var style = CSSSCompiler.FromDeclaration("unknown-prop", "value");
        Assert.Equal(StyleSheet.Empty, style);
    }
}
