using Paper.Core.Reconciler;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;
using Paper.Layout;
using Xunit;
using R = Paper.Core.Reconciler.Reconciler;

namespace Paper.Core.Tests;

/// <summary>
/// Tests for LayoutEngine, BoxModel, FlexLayout, and GridLayout.
/// Uses the reconciler to build a committed fiber tree, then runs LayoutEngine over it.
/// </summary>
[Collection("Sequential")]
public sealed class LayoutTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (Fiber root, LayoutEngine engine) Mount(UINode node, float w = 800f, float h = 600f)
    {
        var rec = new R();
        rec.Mount(node);
        ApplyComputedStyle(rec.Root!);
        var engine = new LayoutEngine();
        engine.Layout(rec.Root!, w, h);
        return (rec.Root!, engine);
    }

    /// <summary>
    /// Propagate inline styles to ComputedStyle so the layout engine can read them.
    /// In production this is done by StyleResolver; in tests we use Props.Style directly.
    /// </summary>
    private static void ApplyComputedStyle(Fiber fiber)
    {
        fiber.ComputedStyle = fiber.Props.Style ?? Paper.Core.Styles.StyleSheet.Empty;
        var child = fiber.Child;
        while (child != null) { ApplyComputedStyle(child); child = child.Sibling; }
    }

    private static float Approx(float v) => MathF.Round(v, 2);

    // ── BoxModel ──────────────────────────────────────────────────────────────

    [Fact]
    public void BoxModel_ExplicitSize_ResolvedCorrectly()
    {
        var style = new StyleSheet { Width = Length.Px(200), Height = Length.Px(100) };
        var (w, h) = BoxModel.OuterSize(style, 800f, 600f);
        Assert.Equal(200f, w);
        Assert.Equal(100f, h);
    }

    [Fact]
    public void BoxModel_PercentSize_ResolvesAgainstContainer()
    {
        var style = new StyleSheet { Width = Length.Percent(50), Height = Length.Percent(25) };
        var (w, h) = BoxModel.OuterSize(style, 800f, 600f);
        Assert.Equal(400f, w);
        Assert.Equal(150f, h);
    }

    [Fact]
    public void BoxModel_MinWidth_Clamps()
    {
        var style = new StyleSheet { Width = Length.Px(50), MinWidth = Length.Px(100) };
        var (w, _) = BoxModel.OuterSize(style, 800f, 600f);
        Assert.Equal(100f, w);
    }

    [Fact]
    public void BoxModel_MaxWidth_Clamps()
    {
        var style = new StyleSheet { Width = Length.Px(500), MaxWidth = Length.Px(200) };
        var (w, _) = BoxModel.OuterSize(style, 800f, 600f);
        Assert.Equal(200f, w);
    }

    [Fact]
    public void BoxModel_BorderBoxPadding_SubtractedFromContentSize()
    {
        // 200px wide, 20px padding each side → content = 160px
        var style = new StyleSheet
        {
            Width   = Length.Px(200),
            Height  = Length.Px(100),
            Padding = new Thickness(Length.Px(10), Length.Px(20)),
            BoxSizing = BoxSizing.BorderBox,
        };
        var (cw, ch) = BoxModel.ContentSize(200f, 100f, style, 800f, 600f);
        Assert.Equal(160f, cw); // 200 - 20L - 20R
        Assert.Equal(80f, ch);  // 100 - 10T - 10B
    }

    [Fact]
    public void BoxModel_IndividualPaddingSides_Override()
    {
        var style = new StyleSheet
        {
            Padding    = new Thickness(Length.Px(10)),
            PaddingTop = Length.Px(30),
        };
        var (pt, pr, pb, pl) = BoxModel.PaddingPixels(style, 100f, 100f);
        Assert.Equal(30f, pt);
        Assert.Equal(10f, pr);
        Assert.Equal(10f, pb);
        Assert.Equal(10f, pl);
    }

    // ── Block layout ──────────────────────────────────────────────────────────

    [Fact]
    public void Block_Root_FillsSurface()
    {
        var (root, _) = Mount(UI.Box());
        Assert.Equal(0f, root.Layout.X);
        Assert.Equal(0f, root.Layout.Y);
        Assert.Equal(800f, root.Layout.Width);
        Assert.Equal(600f, root.Layout.Height);
    }

    [Fact]
    public void Block_ExplicitSize_UsesSpecifiedDimensions()
    {
        var style = new StyleSheet { Width = Length.Px(300), Height = Length.Px(150) };
        var (root, _) = Mount(UI.Box(style));
        Assert.Equal(300f, root.Layout.Width);
        Assert.Equal(150f, root.Layout.Height);
    }

    [Fact]
    public void Block_Children_StackVertically()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet { Height = Length.Px(400) },
            UI.Box(new StyleSheet { Height = Length.Px(100) }),
            UI.Box(new StyleSheet { Height = Length.Px(120) })
        ));

        var first  = root.Child!;
        var second = root.Child!.Sibling!;

        Assert.Equal(0f, first.Layout.Y);
        Assert.Equal(100f, second.Layout.Y);
    }

    [Fact]
    public void Block_Child_InheritsFullWidth()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet { Width = Length.Px(400), Height = Length.Px(200) },
            UI.Box(new StyleSheet { Height = Length.Px(50) })
        ));

        var child = root.Child!;
        Assert.Equal(400f, child.Layout.Width);
    }

    [Fact]
    public void Block_Padding_ShrinksChildContentArea()
    {
        // Parent: 400px wide, 20px padding each side → child gets 360px
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Width   = Length.Px(400),
                Height  = Length.Px(200),
                Padding = new Thickness(Length.Px(0), Length.Px(20)),
            },
            UI.Box(new StyleSheet { Height = Length.Px(50) })
        ));

        var child = root.Child!;
        Assert.Equal(360f, child.Layout.Width);
    }

    [Fact]
    public void Block_Margin_OffsetAndGapsChild()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet { Width = Length.Px(400), Height = Length.Px(300) },
            UI.Box(new StyleSheet
            {
                Height = Length.Px(50),
                Margin = new Thickness(Length.Px(10), Length.Px(20)),
            })
        ));

        var child = root.Child!;
        Assert.Equal(20f, child.Layout.X); // left margin
        Assert.Equal(10f, child.Layout.Y); // top margin
        Assert.Equal(360f, child.Layout.Width); // 400 - 20L - 20R
    }

    // ── Absolute positioning ──────────────────────────────────────────────────

    [Fact]
    public void Absolute_TopLeft_PositionedCorrectly()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet { Width = Length.Px(400), Height = Length.Px(300) },
            UI.Box(new StyleSheet
            {
                Position = Position.Absolute,
                Top      = Length.Px(20),
                Left     = Length.Px(30),
                Width    = Length.Px(100),
                Height   = Length.Px(50),
            })
        ));

        var child = root.Child!;
        Assert.Equal(30f, child.Layout.X);
        Assert.Equal(20f, child.Layout.Y);
    }

    [Fact]
    public void Absolute_BottomRight_PositionedCorrectly()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet { Width = Length.Px(400), Height = Length.Px(300) },
            UI.Box(new StyleSheet
            {
                Position = Position.Absolute,
                Bottom   = Length.Px(10),
                Right    = Length.Px(20),
                Width    = Length.Px(100),
                Height   = Length.Px(50),
            })
        ));

        var child = root.Child!;
        Assert.Equal(280f, child.Layout.X); // 400 - 100 - 20
        Assert.Equal(240f, child.Layout.Y); // 300 - 50 - 10
    }

    [Fact]
    public void Absolute_NotInFlow_SiblingsUnaffected()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet { Width = Length.Px(400), Height = Length.Px(300) },
            UI.Box(new StyleSheet
            {
                Position = Position.Absolute,
                Top = Length.Px(0), Left = Length.Px(0),
                Width = Length.Px(100), Height = Length.Px(50),
            }),
            UI.Box(new StyleSheet { Height = Length.Px(80) })
        ));

        var inFlow = root.Child!.Sibling!;
        Assert.Equal(0f, inFlow.Layout.Y); // Not pushed down by absolute sibling
    }

    // ── Flex layout ───────────────────────────────────────────────────────────

    [Fact]
    public void Flex_Row_ChildrenArrangedHorizontally()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.Row,
                Width         = Length.Px(400),
                Height        = Length.Px(100),
            },
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(50) }),
            UI.Box(new StyleSheet { Width = Length.Px(150), Height = Length.Px(50) })
        ));

        var first  = root.Child!;
        var second = first.Sibling!;

        Assert.Equal(0f, first.Layout.X);
        Assert.Equal(100f, second.Layout.X);
        // Both start at same Y
        Assert.Equal(first.Layout.Y, second.Layout.Y);
    }

    [Fact]
    public void Flex_Column_ChildrenArrangedVertically()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.Column,
                Width         = Length.Px(200),
                Height        = Length.Px(400),
            },
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(80) }),
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(60) })
        ));

        var first  = root.Child!;
        var second = first.Sibling!;

        Assert.Equal(0f, first.Layout.Y);
        Assert.Equal(80f, second.Layout.Y);
    }

    [Fact]
    public void Flex_JustifyContentCenter_CentersItems()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display        = Display.Flex,
                FlexDirection  = FlexDirection.Row,
                JustifyContent = JustifyContent.Center,
                Width          = Length.Px(400),
                Height         = Length.Px(100),
            },
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(50) })
        ));

        var child = root.Child!;
        // 400 - 100 = 300 free space; centered → offset 150
        Assert.Equal(150f, Approx(child.Layout.X));
    }

    [Fact]
    public void Flex_JustifyContentSpaceBetween_SpacesEvenly()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display        = Display.Flex,
                FlexDirection  = FlexDirection.Row,
                JustifyContent = JustifyContent.SpaceBetween,
                Width          = Length.Px(400),
                Height         = Length.Px(100),
            },
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(50) }),
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(50) })
        ));

        var first  = root.Child!;
        var second = first.Sibling!;

        Assert.Equal(0f, first.Layout.X);
        Assert.Equal(300f, second.Layout.X); // right edge: 400 - 100
    }

    [Fact]
    public void Flex_AlignItemsCenter_CrossAxisCentered()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display      = Display.Flex,
                FlexDirection = FlexDirection.Row,
                AlignItems   = AlignItems.Center,
                Width        = Length.Px(400),
                Height       = Length.Px(100),
            },
            UI.Box(new StyleSheet { Width = Length.Px(80), Height = Length.Px(40) })
        ));

        var child = root.Child!;
        // 100 height, 40 item height → centered at Y=30
        Assert.Equal(30f, Approx(child.Layout.Y));
    }

    [Fact]
    public void Flex_Gap_AddsSpaceBetweenItems()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.Row,
                Gap           = Length.Px(16),
                Width         = Length.Px(400),
                Height        = Length.Px(100),
            },
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(50) }),
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(50) })
        ));

        var second = root.Child!.Sibling!;
        Assert.Equal(116f, second.Layout.X); // 100 + 16 gap
    }

    [Fact]
    public void Flex_FlexGrow_DistributesRemainingSpace()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.Row,
                Width         = Length.Px(400),
                Height        = Length.Px(100),
            },
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(50), FlexGrow = 0f }),
            UI.Box(new StyleSheet { Height = Length.Px(50), FlexGrow = 1f })
        ));

        // Second child should get the remaining 300px
        var second = root.Child!.Sibling!;
        Assert.Equal(300f, Approx(second.Layout.Width));
    }

    [Fact]
    public void Flex_RowReverse_ChildrenRightToLeft()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.RowReverse,
                Width         = Length.Px(400),
                Height        = Length.Px(100),
            },
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(50) }),
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(50) })
        ));

        var first  = root.Child!;
        var second = first.Sibling!;

        // In row-reverse, first child ends up at the right, second at 200
        Assert.True(first.Layout.X > second.Layout.X,
            $"Expected first.X ({first.Layout.X}) > second.X ({second.Layout.X}) in row-reverse");
    }

    // ── Display:None ──────────────────────────────────────────────────────────

    [Fact]
    public void DisplayNone_ElementHasZeroSize()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet { Width = Length.Px(400), Height = Length.Px(300) },
            UI.Box(new StyleSheet { Display = Display.None, Width = Length.Px(100), Height = Length.Px(50) })
        ));

        var child = root.Child!;
        Assert.Equal(0f, child.Layout.Width);
        Assert.Equal(0f, child.Layout.Height);
    }

    // ── Absolute positions (surface-space propagation) ─────────────────────

    [Fact]
    public void AbsolutePosition_Propagates_FromRoot()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet { Width = Length.Px(800), Height = Length.Px(600) },
            UI.Box(
                new StyleSheet { Width = Length.Px(400), Height = Length.Px(300) },
                UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(50) })
            )
        ));

        var outer = root.Child!;
        var inner = outer.Child!;

        // Root has no padding/margin, so outer is at 0,0 and inner is at 0,0 too
        Assert.Equal(0f, inner.Layout.AbsoluteX);
        Assert.Equal(0f, inner.Layout.AbsoluteY);
    }

    [Fact]
    public void AbsolutePosition_NestedPadded_CorrectSurfaceCoords()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Width   = Length.Px(800),
                Height  = Length.Px(600),
                Padding = new Thickness(Length.Px(50)),
            },
            UI.Box(new StyleSheet { Width = Length.Px(100), Height = Length.Px(50) })
        ));

        var child = root.Child!;
        // Child is inside 50px padding
        Assert.Equal(50f, child.Layout.AbsoluteX);
        Assert.Equal(50f, child.Layout.AbsoluteY);
    }

    // ── Flex wrap ─────────────────────────────────────────────────────────────

    [Fact]
    public void Flex_Wrap_OverflowingItemsMovedToNextLine()
    {
        // 3 × 150px items in 400px container → first two fit on line 1, third wraps.
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.Row,
                FlexWrap      = FlexWrap.Wrap,
                Width         = Length.Px(400),
                Height        = Length.Px(300),
            },
            UI.Box(new StyleSheet { Width = Length.Px(150), Height = Length.Px(60) }),
            UI.Box(new StyleSheet { Width = Length.Px(150), Height = Length.Px(60) }),
            UI.Box(new StyleSheet { Width = Length.Px(150), Height = Length.Px(60) })
        ));

        var first  = root.Child!;
        var second = first.Sibling!;
        var third  = second.Sibling!;

        // First two share the same row (Y=0), third wraps.
        Assert.Equal(0f, Approx(first.Layout.Y));
        Assert.Equal(0f, Approx(second.Layout.Y));
        Assert.True(third.Layout.Y > 0f, $"third.Y={third.Layout.Y} should be > 0 (wrapped to next line)");
    }

    [Fact]
    public void Flex_Wrap_WrappedItemStartsAtLeftEdge()
    {
        // Wrapped item should start at x=0, not continue from where the previous item ended.
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.Row,
                FlexWrap      = FlexWrap.Wrap,
                Width         = Length.Px(300),
                Height        = Length.Px(300),
            },
            UI.Box(new StyleSheet { Width = Length.Px(200), Height = Length.Px(60) }),
            UI.Box(new StyleSheet { Width = Length.Px(200), Height = Length.Px(60) }) // wraps
        ));

        var second = root.Child!.Sibling!;
        Assert.Equal(0f, Approx(second.Layout.X));
    }

    [Fact]
    public void Flex_NoWrap_ItemsOverflowContainer()
    {
        // With no wrap, items overflow horizontally without moving to next line.
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.Row,
                FlexWrap      = FlexWrap.NoWrap,
                Width         = Length.Px(200),
                Height        = Length.Px(100),
            },
            UI.Box(new StyleSheet { Width = Length.Px(150), Height = Length.Px(50) }),
            UI.Box(new StyleSheet { Width = Length.Px(150), Height = Length.Px(50) }) // would overflow
        ));

        var first  = root.Child!;
        var second = first.Sibling!;

        // Both items must be on the same row (Y same), second starts after first.
        Assert.Equal(0f, Approx(first.Layout.Y));
        Assert.Equal(0f, Approx(second.Layout.Y));
        Assert.True(second.Layout.X > 0f);
    }

    // ── Flex min/max clamping ─────────────────────────────────────────────────

    [Fact]
    public void Flex_MinWidth_ClampsBelowGrowTarget()
    {
        // A flex-shrink scenario: container 200px, two 200px items → shrunk to 100px each.
        // If one has min-width 150px, it should not shrink below that.
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.Row,
                FlexWrap      = FlexWrap.NoWrap,
                Width         = Length.Px(200),
                Height        = Length.Px(100),
            },
            UI.Box(new StyleSheet { Width = Length.Px(200), Height = Length.Px(50), FlexShrink = 1f, MinWidth = Length.Px(150) }),
            UI.Box(new StyleSheet { Width = Length.Px(200), Height = Length.Px(50), FlexShrink = 1f })
        ));

        var first = root.Child!;
        Assert.True(first.Layout.Width >= 150f, $"Expected >= 150px due to MinWidth, got {first.Layout.Width}");
    }

    [Fact]
    public void Flex_MaxWidth_ClampsAboveGrowTarget()
    {
        // flex-grow distributes space, but max-width caps one item.
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display       = Display.Flex,
                FlexDirection = FlexDirection.Row,
                Width         = Length.Px(400),
                Height        = Length.Px(100),
            },
            UI.Box(new StyleSheet { Height = Length.Px(50), FlexGrow = 1f, MaxWidth = Length.Px(100) }),
            UI.Box(new StyleSheet { Height = Length.Px(50), FlexGrow = 1f })
        ));

        var first = root.Child!;
        Assert.True(first.Layout.Width <= 100f, $"Expected <= 100px due to MaxWidth, got {first.Layout.Width}");
    }

    // ── Grid layout ───────────────────────────────────────────────────────────

    [Fact]
    public void Grid_TwoEqualColumns_SplitsWidthEvenly()
    {
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display             = Display.Grid,
                GridTemplateColumns = "1fr 1fr",
                Width               = Length.Px(400),
                Height              = Length.Px(200),
            },
            UI.Box(new StyleSheet { Height = Length.Px(50) }),
            UI.Box(new StyleSheet { Height = Length.Px(50) })
        ));

        var first  = root.Child!;
        var second = first.Sibling!;

        Assert.Equal(0f,    Approx(first.Layout.X));
        Assert.Equal(200f,  Approx(second.Layout.X));
        Assert.Equal(200f,  Approx(first.Layout.Width));
        Assert.Equal(200f,  Approx(second.Layout.Width));
    }

    [Fact]
    public void Grid_PxAndFrColumns_CorrectSizes()
    {
        // 400px wide, columns: 100px + 1fr → 100 + 300 = 400
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display             = Display.Grid,
                GridTemplateColumns = "100px 1fr",
                Width               = Length.Px(400),
                Height              = Length.Px(200),
            },
            UI.Box(new StyleSheet { Height = Length.Px(50) }),
            UI.Box(new StyleSheet { Height = Length.Px(50) })
        ));

        var first  = root.Child!;
        var second = first.Sibling!;

        Assert.Equal(100f, Approx(first.Layout.Width));
        Assert.Equal(100f, Approx(second.Layout.X));
        Assert.Equal(300f, Approx(second.Layout.Width));
    }

    [Fact]
    public void Grid_ThreeColumns_SecondRowStartsBelow()
    {
        // 3-column grid with 4 items → 2 rows
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display             = Display.Grid,
                GridTemplateColumns = "1fr 1fr 1fr",
                Width               = Length.Px(300),
                Height              = Length.Px(200),
            },
            UI.Box(new StyleSheet { Height = Length.Px(60) }),
            UI.Box(new StyleSheet { Height = Length.Px(60) }),
            UI.Box(new StyleSheet { Height = Length.Px(60) }),
            UI.Box(new StyleSheet { Height = Length.Px(60) })
        ));

        var item1 = root.Child!;
        var item4 = root.Child!.Sibling!.Sibling!.Sibling!;

        // First item at row 0, fourth at row 1
        Assert.Equal(0f, Approx(item1.Layout.Y));
        Assert.True(item4.Layout.Y > 0f, $"item4.Y={item4.Layout.Y} should be > 0");
    }

    [Fact]
    public void Grid_RepeatTemplate_ExpandsColumns()
    {
        // repeat(3, 1fr) → same as "1fr 1fr 1fr"
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display             = Display.Grid,
                GridTemplateColumns = "repeat(3, 1fr)",
                Width               = Length.Px(300),
                Height              = Length.Px(200),
            },
            UI.Box(new StyleSheet { Height = Length.Px(50) }),
            UI.Box(new StyleSheet { Height = Length.Px(50) }),
            UI.Box(new StyleSheet { Height = Length.Px(50) })
        ));

        var first  = root.Child!;
        var second = first.Sibling!;
        var third  = second.Sibling!;

        Assert.Equal(0f,    Approx(first.Layout.X));
        Assert.Equal(100f,  Approx(second.Layout.X));
        Assert.Equal(200f,  Approx(third.Layout.X));
        Assert.Equal(100f,  Approx(first.Layout.Width));
    }

    [Fact]
    public void Grid_ExplicitColumnSpan_OccupiesMultipleCells()
    {
        // 3-column grid; second item spans columns 2-3.
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display             = Display.Grid,
                GridTemplateColumns = "1fr 1fr 1fr",
                Width               = Length.Px(300),
                Height              = Length.Px(200),
            },
            UI.Box(new StyleSheet { Height = Length.Px(50) }),
            UI.Box(new StyleSheet
            {
                Height            = Length.Px(50),
                GridColumnStart   = 2,
                GridColumnEnd     = 4, // exclusive → spans cols 2..3
            })
        ));

        var second = root.Child!.Sibling!;
        // Spans 2 of 3 columns = 200px wide
        Assert.True(second.Layout.Width > 100f, $"Expected > 100px for 2-col span, got {second.Layout.Width}");
    }

    [Fact]
    public void Grid_Gap_AppliedBetweenCells()
    {
        // 2 columns, 16px gap: col0 = 0..192, gap = 16, col1 = 208..400
        var (root, _) = Mount(UI.Box(
            new StyleSheet
            {
                Display             = Display.Grid,
                GridTemplateColumns = "1fr 1fr",
                Gap                 = Length.Px(16),
                Width               = Length.Px(400),
                Height              = Length.Px(200),
            },
            UI.Box(new StyleSheet { Height = Length.Px(50) }),
            UI.Box(new StyleSheet { Height = Length.Px(50) })
        ));

        var first  = root.Child!;
        var second = first.Sibling!;

        Assert.Equal(0f,   Approx(first.Layout.X));
        Assert.Equal(208f, Approx(second.Layout.X)); // (400 - 16) / 2 = 192; start = 192 + 16 = 208
        Assert.Equal(192f, Approx(first.Layout.Width));
        Assert.Equal(192f, Approx(second.Layout.Width));
    }
}
