using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Xunit;

namespace Paper.Core.Tests;

/// <summary>
/// Tests for the style system: StyleSheet.Merge cascade, StyleRegistry, StyleResolver.
/// </summary>
[Collection("Sequential")]
public sealed class StyleSystemTests
{
    // ── StyleSheet.Merge ──────────────────────────────────────────────────────

    [Fact]
    public void Merge_NonNullInOther_OverridesBase()
    {
        var base_ = new StyleSheet { Width = Length.Px(100) };
        var other = new StyleSheet { Width = Length.Px(200) };

        var result = base_.Merge(other);
        Assert.Equal(Length.Px(200), result.Width);
    }

    [Fact]
    public void Merge_NullInOther_FallsBackToBase()
    {
        var base_ = new StyleSheet { Width = Length.Px(100) };
        var other = new StyleSheet { Height = Length.Px(50) }; // Width is null

        var result = base_.Merge(other);
        Assert.Equal(Length.Px(100), result.Width);
        Assert.Equal(Length.Px(50), result.Height);
    }

    [Fact]
    public void Merge_EmptyOther_ReturnsCopyOfBase()
    {
        var base_ = new StyleSheet
        {
            Width    = Length.Px(100),
            Height   = Length.Px(200),
            FlexGrow = 1f,
        };

        var result = base_.Merge(StyleSheet.Empty);
        Assert.Equal(Length.Px(100), result.Width);
        Assert.Equal(Length.Px(200), result.Height);
        Assert.Equal(1f, result.FlexGrow);
    }

    [Fact]
    public void Merge_IsNotMutating_ReturnsNewRecord()
    {
        var base_ = new StyleSheet { Width = Length.Px(100) };
        var other = new StyleSheet { Width = Length.Px(200) };

        var result = base_.Merge(other);

        Assert.NotSame(base_, result);
        Assert.Equal(Length.Px(100), base_.Width); // unchanged
    }

    [Fact]
    public void Merge_MultipleLayersStack_LastWins()
    {
        var a = new StyleSheet { Width = Length.Px(10), Height = Length.Px(10) };
        var b = new StyleSheet { Width = Length.Px(20) };
        var c = new StyleSheet { Height = Length.Px(30) };

        var result = a.Merge(b).Merge(c);
        Assert.Equal(Length.Px(20), result.Width);  // from b
        Assert.Equal(Length.Px(30), result.Height); // from c
    }

    // ── StyleRegistry ─────────────────────────────────────────────────────────

    [Fact]
    public void StyleRegistry_SetClass_CanBeRetrieved()
    {
        var reg = new StyleRegistry();
        var sheet = new StyleSheet { FlexGrow = 1f };

        reg.SetClass("flex", sheet);

        Assert.True(reg.TryGetClass("flex", out var retrieved));
        Assert.Equal(1f, retrieved.FlexGrow);
    }

    [Fact]
    public void StyleRegistry_TryGetClass_ReturnsFalse_ForMissingClass()
    {
        var reg = new StyleRegistry();
        Assert.False(reg.TryGetClass("missing", out _));
    }

    [Fact]
    public void StyleRegistry_SetClass_IncrementsVersion()
    {
        var reg = new StyleRegistry();
        var v0 = reg.Version;

        reg.SetClass("btn", new StyleSheet { Width = Length.Px(80) });

        Assert.Equal(v0 + 1, reg.Version);
    }

    [Fact]
    public void StyleRegistry_MultipleSetClass_IncrementVersionEachTime()
    {
        var reg = new StyleRegistry();
        var v0 = reg.Version;

        reg.SetClass("a", StyleSheet.Empty);
        reg.SetClass("b", StyleSheet.Empty);
        reg.SetClass("c", StyleSheet.Empty);

        Assert.Equal(v0 + 3, reg.Version);
    }

    [Fact]
    public void StyleRegistry_SetClass_OverwritesExisting()
    {
        var reg = new StyleRegistry();
        reg.SetClass("x", new StyleSheet { Width = Length.Px(10) });
        reg.SetClass("x", new StyleSheet { Width = Length.Px(20) });

        Assert.True(reg.TryGetClass("x", out var s));
        Assert.Equal(Length.Px(20), s.Width);
    }

    // ── StyleResolver ─────────────────────────────────────────────────────────

    [Fact]
    public void StyleResolver_Box_HasDefaultDisplayBlock()
    {
        var style = StyleResolver.Resolve(ElementTypes.Box, Props.Empty, null, default);
        Assert.Equal(Display.Block, style.Display);
    }

    [Fact]
    public void StyleResolver_Text_HasDefaultDisplayInline()
    {
        var style = StyleResolver.Resolve(ElementTypes.Text, Props.Empty, null, default);
        Assert.Equal(Display.Inline, style.Display);
    }

    [Fact]
    public void StyleResolver_InlineStyle_OverridesDefault()
    {
        var inline = new StyleSheet { Display = Display.Flex };
        var props  = new PropsBuilder().Style(inline).Build();

        var style = StyleResolver.Resolve(ElementTypes.Box, props, null, default);
        Assert.Equal(Display.Flex, style.Display);
    }

    [Fact]
    public void StyleResolver_ClassStyle_OverridesDefault()
    {
        var reg = new StyleRegistry();
        reg.SetClass("custom", new StyleSheet { FlexGrow = 2f });

        var props = new PropsBuilder().Set("className", "custom").Build();
        var style = StyleResolver.Resolve(ElementTypes.Box, props, reg, default);

        Assert.Equal(2f, style.FlexGrow);
    }

    [Fact]
    public void StyleResolver_InlineStyle_OverridesClassStyle()
    {
        var reg = new StyleRegistry();
        reg.SetClass("btn", new StyleSheet { Width = Length.Px(80) });

        var inline = new StyleSheet { Width = Length.Px(120) };
        var props  = new PropsBuilder().Set("className", "btn").Style(inline).Build();

        var style = StyleResolver.Resolve(ElementTypes.Box, props, reg, default);
        Assert.Equal(Length.Px(120), style.Width);
    }

    [Fact]
    public void StyleResolver_HoverStyle_AppliedWhenHovered()
    {
        var hover = new StyleSheet { Background = new PaperColour(1f, 0f, 0f, 1f) };
        var props = new PropsBuilder().Set("hoverStyle", hover).Build();

        var normalStyle = StyleResolver.Resolve(ElementTypes.Box, props, null, new InteractionState(false, false, false));
        Assert.Null(normalStyle.Background);

        var hoveredStyle = StyleResolver.Resolve(ElementTypes.Box, props, null, new InteractionState(true, false, false));
        Assert.Equal(new PaperColour(1f, 0f, 0f, 1f), hoveredStyle.Background);
    }

    [Fact]
    public void StyleResolver_ActiveStyle_AppliedWhenActive()
    {
        var active = new StyleSheet { Opacity = 0.5f };
        var props  = new PropsBuilder().Set("activeStyle", active).Build();

        var normalStyle = StyleResolver.Resolve(ElementTypes.Box, props, null, new InteractionState(false, false, false));
        Assert.Null(normalStyle.Opacity);

        var activeStyle = StyleResolver.Resolve(ElementTypes.Box, props, null, new InteractionState(false, true, false));
        Assert.Equal(0.5f, activeStyle.Opacity);
    }

    [Fact]
    public void StyleResolver_FocusStyle_AppliedWhenFocused()
    {
        var focus = new StyleSheet { BorderRadius = 8f };
        var props = new PropsBuilder().Set("focusStyle", focus).Build();

        var normalStyle = StyleResolver.Resolve(ElementTypes.Box, props, null, new InteractionState(false, false, false));
        Assert.Null(normalStyle.BorderRadius);

        var focusedStyle = StyleResolver.Resolve(ElementTypes.Box, props, null, new InteractionState(false, false, true));
        Assert.Equal(8f, focusedStyle.BorderRadius);
    }

    [Fact]
    public void StyleResolver_ClassHoverVariant_AppliedWhenHovered()
    {
        var reg = new StyleRegistry();
        reg.SetClass("btn",        new StyleSheet { Background = new PaperColour(0.2f, 0.2f, 0.2f, 1f) });
        reg.SetClass("btn:hover",  new StyleSheet { Background = new PaperColour(0.4f, 0.4f, 0.4f, 1f) });

        var props = new PropsBuilder().Set("className", "btn").Build();

        var normal  = StyleResolver.Resolve(ElementTypes.Box, props, reg, new InteractionState(false, false, false));
        var hovered = StyleResolver.Resolve(ElementTypes.Box, props, reg, new InteractionState(true, false, false));

        Assert.Equal(new PaperColour(0.2f, 0.2f, 0.2f, 1f), normal.Background);
        Assert.Equal(new PaperColour(0.4f, 0.4f, 0.4f, 1f), hovered.Background);
    }

    [Fact]
    public void StyleResolver_CascadeOrder_DefaultThenClassThenInline()
    {
        // Box default: Display.Block
        // Class: FlexGrow = 1
        // Inline: FlexGrow = 2 (overrides class)
        var reg = new StyleRegistry();
        reg.SetClass("grow", new StyleSheet { FlexGrow = 1f });

        var inline = new StyleSheet { FlexGrow = 2f };
        var props  = new PropsBuilder().Set("className", "grow").Style(inline).Build();

        var style = StyleResolver.Resolve(ElementTypes.Box, props, reg, default);

        Assert.Equal(Display.Block, style.Display); // from default
        Assert.Equal(2f, style.FlexGrow);           // inline wins over class
    }
}
