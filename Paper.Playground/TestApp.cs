using Paper.Core.Hooks;
using Paper.Core.Styles;
using Paper.Core.VirtualDom;

/// <summary>
/// Test application demonstrating a simple counter with Paper UI
/// </summary>
public static class TestApp
{
    /// <summary>
    /// Simple counter component with increment, decrement, and reset functionality
    /// </summary>
    public static UINode SimpleCounter(Props props)
    {
        var (count, setCount, updateCount) = Hooks.UseState(0);

        var containerStyle = Hooks.UseMemo(() => new StyleSheet
        {
            Display = Display.Flex,
            FlexDirection = FlexDirection.Column,
            AlignItems = AlignItems.Center,
            JustifyContent = JustifyContent.Center,
            Height = Length.Percent(100),
            Background = new PaperColour(0.1f, 0.1f, 0.15f, 1f)
        }, Array.Empty<object>());
        
        var cardStyle = Hooks.UseMemo(() => new StyleSheet
        {
            Display = Display.Flex,
            FlexDirection = FlexDirection.Column,
            AlignItems = AlignItems.Center,
            Background = new PaperColour(0.2f, 0.2f, 0.25f, 1f),
            Padding = Thickness.All(32),
            BorderRadius = 8,
            BoxShadow = new[] { new BoxShadow(0, 4, 12, new PaperColour(0, 0, 0, 0.3f)) }
        }, Array.Empty<object>());
        
        var buttonStyle = Hooks.UseMemo(() => new StyleSheet
        {
            Background = new PaperColour(0.3f, 0.6f, 1f, 1f),
            Color = new PaperColour(1f, 1f, 1f, 1f),
            Padding = Thickness.All(12),
            BorderRadius = 6,
            Margin = Thickness.Horizontal(8)
        }, Array.Empty<object>());
        
        var countDisplayStyle = Hooks.UseMemo(() => new StyleSheet
        {
            Width = Length.Px(200),
            Height = Length.Px(100),
            Background = new PaperColour(0.3f, 0.3f, 0.4f, 1f),
            BorderRadius = 8,
            Margin = new Thickness(0, 0, 24, 0),
            Display = Display.Flex,
            AlignItems = AlignItems.Center,
            JustifyContent = JustifyContent.Center,
            FontSize = Length.Px(32),
            Color = new PaperColour(1f, 0.6f, 0.2f, 1f)
        }, Array.Empty<object>());
        
        var increment = Hooks.UseCallback(() => updateCount(c => c + 1), Array.Empty<object>());
        var decrement = Hooks.UseCallback(() => updateCount(c => c - 1), Array.Empty<object>());
        var reset = Hooks.UseCallback(() => setCount(0), Array.Empty<object>());
        
        return UI.Box(containerStyle,
            UI.Box(cardStyle,
                // Simple count display: flex row of orange squares
                UI.Box(countDisplayStyle,
                    UI.Box(new StyleSheet
                    {
                        Display = Display.Flex,
                        FlexDirection = FlexDirection.Row,
                        Gap = Length.Px(4),
                        AlignItems = AlignItems.Center,
                        JustifyContent = JustifyContent.Center
                    }, Enumerable.Range(0, count).Select(_ => UI.Box(new StyleSheet
                    {
                        Width = Length.Px(20),
                        Height = Length.Px(20),
                        Background = new PaperColour(1f, 0.6f, 0.2f, 1f),
                        BorderRadius = 4
                    })).ToArray())
                ),
                UI.Box(new StyleSheet
                {
                    Display = Display.Flex,
                    FlexDirection = FlexDirection.Row,
                    Gap = Length.Px(8)
                },
                    UI.Button("Increment", increment, buttonStyle),
                    UI.Button("Decrement", decrement, buttonStyle),
                    UI.Button("Reset", reset, buttonStyle)
                )
            )
        );
    }
}
