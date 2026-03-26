using System.Collections.Generic;
using System.Linq;
using Paper.Core.Components;
using Paper.Core.VirtualDom;
using Paper.Core.Styles;
using Paper.Core.Hooks;
using Paper.Core.Context;

namespace Paper.Generated
{

    public static partial class BasicAppComponent
    {
        public static UINode BasicApp(Props props)
        {
            return UI.Box(
new PropsBuilder()
.Style(new StyleSheet { Display = Display.Flex, FlexDirection = FlexDirection.Column, AlignItems = AlignItems.Center, JustifyContent = JustifyContent.Center, Height = Length.Percent(100), Background = new PaperColour(0.101960786f, 0.101960786f, 0.18039216f, 1f), Padding = new Thickness(16f), BorderRadius = 8f })
.Children(new UINode("text",
new PropsBuilder()
.Style(new StyleSheet { FontSize = Length.Px(48), Color = new PaperColour(1f, 0.41960785f, 0.20784314f, 1f), PaddingBottom = Length.Px(24) })
.Text("Hello World!")
.Build()), UI.Button("Click Me", () => { System.Console.WriteLine("Button clicked!"); }, new StyleSheet { Background = new PaperColour(0.05882353f, 0.20392157f, 0.3764706f, 1f), Color = new PaperColour(1f, 1f, 1f, 1f), Padding = new Thickness(Length.Px(12), Length.Px(24)), BorderRadius = 6f, FontSize = Length.Px(16) }))
.Build());
        }
    }
}
