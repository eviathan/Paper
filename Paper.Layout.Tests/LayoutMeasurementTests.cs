using Paper.Core.Reconciler;
using Paper.Core.VirtualDom;
using Paper.Layout;
using Xunit;

namespace Paper.Layout.Tests;

public sealed class LayoutMeasurementTests
{
    private sealed class DummyMeasurer : ILayoutMeasurer
    {
        public (float width, float height) MeasureText(string text, Paper.Core.Styles.StyleSheet style, float? maxWidth = null)
            => (text.Length * 5f, 12f);
    }

    [Fact]
    public void TextNode_WithAutoHeight_GetsMeasuredHeight()
    {
        var rootNode = UI.Box(
            UI.Text("Hello", style: Paper.Core.Styles.StyleSheet.Empty),
            UI.Text("World", style: Paper.Core.Styles.StyleSheet.Empty)
        );

        var reconciler = new Reconciler();
        reconciler.Mount(rootNode);

        var root = reconciler.Root!;

        // Compute styles for the test (inline only)
        ApplyInlineComputedStyle(root);

        var engine = new LayoutEngine();
        engine.Layout(root, 800, 600, new DummyMeasurer());

        var firstText = root.Child!;
        Assert.True(firstText.Layout.Height > 0);
    }

    private static void ApplyInlineComputedStyle(Fiber fiber)
    {
        fiber.ComputedStyle = fiber.Props.Style ?? Paper.Core.Styles.StyleSheet.Empty;
        var child = fiber.Child;
        while (child != null)
        {
            ApplyInlineComputedStyle(child);
            child = child.Sibling;
        }
    }
}

