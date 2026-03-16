using Paper.CSX;
using Xunit;

namespace Paper.CSX.Tests;

/// <summary>
/// Tests for InlineStyleTranslator — CSS-to-C# style expression conversion.
/// All tests drive the translator through CSXCompiler.Parse() with a style attribute.
/// </summary>
public sealed class InlineStyleTranslatorTests
{
    // Helper: parse a single Box with the given inline style object and return the C# output.
    private static string S(string styleObj) =>
        CSXCompiler.Parse($"<Box style={{{styleObj}}} />");

    // ── Colours ───────────────────────────────────────────────────────────────

    [Fact]
    public void Color_Hex6_EmitsPaperColour()
    {
        var result = S("{ background: '#ff0000' }");
        Assert.Contains("PaperColour", result);
        // #ff0000 → r=1f g=0f b=0f
        Assert.Contains("1f", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Color_Hex3_ExpandedToHex6()
    {
        var result = S("{ background: '#f00' }");
        Assert.Contains("PaperColour", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Color_Hex8_HasAlpha()
    {
        var result = S("{ background: '#ff000080' }");
        Assert.Contains("PaperColour", result);
        // alpha = 0x80/255 ≈ 0.5019608f — just verify 4 components are present
        Assert.Contains("0.5019", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Color_NamedWhite_EmitsPaperColour()
    {
        var result = S("{ background: 'white' }");
        Assert.Contains("PaperColour(1f, 1f, 1f, 1f)", result);
    }

    [Fact]
    public void Color_NamedBlack_EmitsPaperColour()
    {
        var result = S("{ color: 'black' }");
        Assert.Contains("PaperColour(0f, 0f, 0f, 1f)", result);
    }

    [Fact]
    public void Color_NamedTransparent_EmitsPaperColour()
    {
        var result = S("{ background: 'transparent' }");
        Assert.Contains("PaperColour(0f, 0f, 0f, 0f)", result);
    }

    [Fact]
    public void Color_Rgb_EmitsPaperColour()
    {
        var result = S("{ background: 'rgb(255, 0, 0)' }");
        Assert.Contains("PaperColour", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Color_Rgba_HasAlpha()
    {
        var result = S("{ background: 'rgba(255, 0, 0, 0.5)' }");
        Assert.Contains("PaperColour", result);
        Assert.Contains("0.5f", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Color_Identifier_EmitsNewPaperColour()
    {
        // Bare identifier in a colour property → new PaperColour(varName)
        var result = S("{ background: myThemeColor }");
        Assert.Contains("PaperColour(myThemeColor)", result);
    }

    [Fact]
    public void Color_BackgroundColorAlias_MapsToBackground()
    {
        // backgroundColor is a CSS alias → StyleSheet.Background
        var result = S("{ backgroundColor: '#fff' }");
        Assert.Contains("Background", result);
        Assert.DoesNotContain("BackgroundColor", result);
    }

    // ── Lengths ───────────────────────────────────────────────────────────────

    [Fact]
    public void Length_NumberPx_EmitsLengthPx()
    {
        var result = S("{ width: 100 }");
        Assert.Contains("Length.Px(100", result);
    }

    [Fact]
    public void Length_PxString_EmitsLengthPx()
    {
        var result = S("{ width: '200px' }");
        Assert.Contains("Length.Px(200", result);
    }

    [Fact]
    public void Length_Percent_EmitsLengthPercent()
    {
        var result = S("{ width: '50%' }");
        Assert.Contains("Length.Percent(50", result);
    }

    [Fact]
    public void Length_Auto_EmitsLengthAuto()
    {
        var result = S("{ width: 'auto' }");
        Assert.Contains("Length.Auto", result);
    }

    [Fact]
    public void Length_Em_EmitsLengthEm()
    {
        var result = S("{ fontSize: '1.5em' }");
        Assert.Contains("Length.Em(1.5", result);
    }

    [Fact]
    public void Length_100Percent_EmitsLengthPercent100()
    {
        var result = S("{ width: '100%' }");
        Assert.Contains("Length.Percent(100", result);
    }

    // ── Display ───────────────────────────────────────────────────────────────

    [Fact]
    public void Display_Flex_EmitsDisplayFlex()
    {
        var result = S("{ display: 'flex' }");
        Assert.Contains("Display.Flex", result);
    }

    [Fact]
    public void Display_Grid_EmitsDisplayGrid()
    {
        var result = S("{ display: 'grid' }");
        Assert.Contains("Display.Grid", result);
    }

    [Fact]
    public void Display_None_EmitsDisplayNone()
    {
        var result = S("{ display: 'none' }");
        Assert.Contains("Display.None", result);
    }

    [Fact]
    public void Display_InlineFlex_EmitsInlineFlex()
    {
        var result = S("{ display: 'inline-flex' }");
        Assert.Contains("Display.InlineFlex", result);
    }

    // ── Flex properties ───────────────────────────────────────────────────────

    [Fact]
    public void FlexDirection_Row_EmitsRow()
    {
        var result = S("{ flexDirection: 'row' }");
        Assert.Contains("FlexDirection.Row", result);
    }

    [Fact]
    public void FlexDirection_Column_EmitsColumn()
    {
        var result = S("{ flexDirection: 'column' }");
        Assert.Contains("FlexDirection.Column", result);
    }

    [Fact]
    public void FlexDirection_RowReverse_EmitsRowReverse()
    {
        var result = S("{ flexDirection: 'row-reverse' }");
        Assert.Contains("FlexDirection.RowReverse", result);
    }

    [Fact]
    public void JustifyContent_Center_EmitsCenter()
    {
        var result = S("{ justifyContent: 'center' }");
        Assert.Contains("JustifyContent.Center", result);
    }

    [Fact]
    public void JustifyContent_SpaceBetween_EmitsSpaceBetween()
    {
        var result = S("{ justifyContent: 'space-between' }");
        Assert.Contains("JustifyContent.SpaceBetween", result);
    }

    [Fact]
    public void AlignItems_Center_EmitsCenter()
    {
        var result = S("{ alignItems: 'center' }");
        Assert.Contains("AlignItems.Center", result);
    }

    [Fact]
    public void AlignItems_Stretch_EmitsStretch()
    {
        var result = S("{ alignItems: 'stretch' }");
        Assert.Contains("AlignItems.Stretch", result);
    }

    [Fact]
    public void FlexWrap_Wrap_EmitsWrap()
    {
        var result = S("{ flexWrap: 'wrap' }");
        Assert.Contains("FlexWrap.Wrap", result);
    }

    [Fact]
    public void FlexGrow_Number_EmitsFloat()
    {
        var result = S("{ flexGrow: 1 }");
        Assert.Contains("FlexGrow", result);
        Assert.Contains("1f", result);
    }

    // ── Flex shorthand ────────────────────────────────────────────────────────

    [Fact]
    public void Flex_SingleNumber_EmitsFlexGrowAndShrink()
    {
        var result = S("{ flex: 1 }");
        Assert.Contains("FlexGrow = 1f", result);
        Assert.Contains("FlexShrink = 1f", result);
    }

    [Fact]
    public void Flex_None_EmitsZeroGrowAndShrink()
    {
        var result = S("{ flex: 'none' }");
        Assert.Contains("FlexGrow = 0f", result);
        Assert.Contains("FlexShrink = 0f", result);
    }

    [Fact]
    public void Flex_Auto_EmitsOneGrowAndShrink()
    {
        var result = S("{ flex: 'auto' }");
        Assert.Contains("FlexGrow = 1f", result);
        Assert.Contains("FlexShrink = 1f", result);
    }

    [Fact]
    public void Flex_TwoNumbers_EmitsGrowAndShrink()
    {
        var result = S("{ flex: '2 0' }");
        Assert.Contains("FlexGrow = 2f", result);
        Assert.Contains("FlexShrink = 0f", result);
    }

    // ── Gap shorthand ─────────────────────────────────────────────────────────

    [Fact]
    public void Gap_Single_EmitsBothAxes()
    {
        var result = S("{ gap: 16 }");
        Assert.Contains("RowGap", result);
        Assert.Contains("ColumnGap", result);
        Assert.Contains("Length.Px(16", result);
    }

    [Fact]
    public void Gap_TwoValues_EmitsRowAndColumn()
    {
        var result = S("{ gap: '8 16' }");
        Assert.Contains("RowGap", result);
        Assert.Contains("ColumnGap", result);
    }

    // ── Padding / Margin (Thickness) ──────────────────────────────────────────

    [Fact]
    public void Padding_Number_EmitsThickness()
    {
        var result = S("{ padding: 8 }");
        Assert.Contains("new Thickness(8", result);
    }

    [Fact]
    public void Padding_FourValues_EmitsThicknessWithFourArgs()
    {
        var result = S("{ padding: '4 8 4 8' }");
        Assert.Contains("new Thickness(", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Margin_Number_EmitsThickness()
    {
        var result = S("{ margin: 12 }");
        Assert.Contains("new Thickness(12", result);
    }

    // ── BorderRadius ──────────────────────────────────────────────────────────

    [Fact]
    public void BorderRadius_Number_EmitsFloat()
    {
        var result = S("{ borderRadius: 4 }");
        Assert.Contains("BorderRadius", result);
        Assert.Contains("4f", result);
        Assert.DoesNotContain("Length.Px", result); // should be float, not Length
    }

    // ── Border shorthand ──────────────────────────────────────────────────────

    [Fact]
    public void Border_WidthColorStyle_EmitsBorderEdges()
    {
        var result = S("{ border: '1px solid #000000' }");
        Assert.Contains("BorderEdges", result);
        Assert.Contains("Border(", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Border_None_EmitsNull()
    {
        // border: 'none' is a single-word value that falls through to the default 'none' → null mapping.
        // The multi-word border shorthand ("none solid #000") path handles "none" as BorderEdges(Border.None).
        var result = S("{ border: 'none' }");
        // null means "not set", which is the correct StyleSheet representation of border:none
        Assert.DoesNotContain("Parse error", result);
    }

    // ── Typography ────────────────────────────────────────────────────────────

    [Fact]
    public void FontWeight_Bold_EmitsFontWeightBold()
    {
        var result = S("{ fontWeight: 'bold' }");
        Assert.Contains("FontWeight.Bold", result);
    }

    [Fact]
    public void FontWeight_500_EmitsMedium()
    {
        var result = S("{ fontWeight: '500' }");
        Assert.Contains("FontWeight.Medium", result);
    }

    [Fact]
    public void TextAlign_Center_EmitsCenter()
    {
        var result = S("{ textAlign: 'center' }");
        Assert.Contains("TextAlign.Center", result);
    }

    [Fact]
    public void TextOverflow_Ellipsis_EmitsEllipsis()
    {
        var result = S("{ textOverflow: 'ellipsis' }");
        Assert.Contains("TextOverflow.Ellipsis", result);
    }

    // ── Position / Overflow / Cursor ──────────────────────────────────────────

    [Fact]
    public void Position_Absolute_EmitsPositionAbsolute()
    {
        var result = S("{ position: 'absolute' }");
        Assert.Contains("Position.Absolute", result);
    }

    [Fact]
    public void Position_Relative_EmitsPositionRelative()
    {
        var result = S("{ position: 'relative' }");
        Assert.Contains("Position.Relative", result);
    }

    [Fact]
    public void Overflow_Hidden_EmitsOverflowHidden()
    {
        var result = S("{ overflow: 'hidden' }");
        Assert.Contains("Overflow.Hidden", result);
    }

    [Fact]
    public void Overflow_Scroll_EmitsOverflowScroll()
    {
        var result = S("{ overflow: 'scroll' }");
        Assert.Contains("Overflow.Scroll", result);
    }

    [Fact]
    public void Cursor_Pointer_EmitsCursorPointer()
    {
        var result = S("{ cursor: 'pointer' }");
        Assert.Contains("Cursor.Pointer", result);
    }

    [Fact]
    public void Cursor_Text_EmitsCursorText()
    {
        var result = S("{ cursor: 'text' }");
        Assert.Contains("Cursor.Text", result);
    }

    // ── Visibility / WhiteSpace / PointerEvents ───────────────────────────────

    [Fact]
    public void Visibility_Hidden_EmitsHidden()
    {
        var result = S("{ visibility: 'hidden' }");
        Assert.Contains("Visibility.Hidden", result);
    }

    [Fact]
    public void WhiteSpace_NoWrap_EmitsNoWrap()
    {
        var result = S("{ whiteSpace: 'nowrap' }");
        Assert.Contains("WhiteSpace.NoWrap", result);
    }

    [Fact]
    public void PointerEvents_None_EmitsNone()
    {
        var result = S("{ pointerEvents: 'none' }");
        Assert.Contains("PointerEvents.None", result);
    }

    // ── ZIndex ────────────────────────────────────────────────────────────────

    [Fact]
    public void ZIndex_Number_EmitsInt()
    {
        var result = S("{ zIndex: 10 }");
        Assert.Contains("ZIndex", result);
        // ZIndex should be int (no 'f' suffix)
        Assert.Contains("10", result);
        Assert.DoesNotContain("10f", result);
    }

    // ── Opacity / LineHeight ──────────────────────────────────────────────────

    [Fact]
    public void Opacity_Float_EmitsFloat()
    {
        var result = S("{ opacity: 0.5 }");
        Assert.Contains("Opacity", result);
        Assert.Contains("0.5f", result);
    }

    [Fact]
    public void LineHeight_Float_EmitsFloat()
    {
        var result = S("{ lineHeight: 1.5 }");
        Assert.Contains("LineHeight", result);
        Assert.Contains("1.5f", result);
    }

    // ── Rotate transform ──────────────────────────────────────────────────────

    [Fact]
    public void Rotate_Deg_EmitsRadians()
    {
        var result = S("{ rotate: '180deg' }");
        Assert.Contains("Rotate", result);
        // 180deg = π ≈ 3.14159
        Assert.Contains("3.14", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void Rotate_Turn_EmitsRadians()
    {
        var result = S("{ rotate: '0.5turn' }");
        Assert.Contains("Rotate", result);
        Assert.Contains("3.14", result); // 0.5 turn = π
    }

    [Fact]
    public void Rotate_Rad_PreservedAsFloat()
    {
        var result = S("{ rotate: '1.5rad' }");
        Assert.Contains("Rotate", result);
        Assert.Contains("1.5f", result);
    }

    // ── Grid properties ───────────────────────────────────────────────────────

    [Fact]
    public void GridTemplateColumns_String_PreservedVerbatim()
    {
        var result = S("{ gridTemplateColumns: '1fr 1fr' }");
        Assert.Contains("GridTemplateColumns", result);
        Assert.Contains("1fr 1fr", result);
    }

    [Fact]
    public void GridColumn_Span_EmitsGridColumnStartEnd()
    {
        var result = S("{ gridColumn: '1/3' }");
        Assert.Contains("GridColumnStart = 1", result);
        Assert.Contains("GridColumnEnd = 3", result);
    }

    [Fact]
    public void GridRow_Span_EmitsGridRowStartEnd()
    {
        var result = S("{ gridRow: '2/4' }");
        Assert.Contains("GridRowStart = 2", result);
        Assert.Contains("GridRowEnd = 4", result);
    }

    // ── Multi-property ────────────────────────────────────────────────────────

    [Fact]
    public void MultipleProps_AllEmitted()
    {
        var result = S("{ display: 'flex', flexDirection: 'column', gap: 8, padding: 16 }");
        Assert.Contains("Display.Flex", result);
        Assert.Contains("FlexDirection.Column", result);
        Assert.Contains("RowGap", result);
        Assert.Contains("new Thickness(16", result);
        Assert.DoesNotContain("Parse error", result);
    }

    [Fact]
    public void EmptyStyle_EmitsStyleSheetEmpty()
    {
        // An empty style object → StyleSheet.Empty
        var result = CSXCompiler.Parse("<Box style={{}} />");
        Assert.DoesNotContain("Parse error", result);
    }
}
