namespace Paper.Rendering.Silk.NET.Models
{
    public class ClickState
    {
        public DateTime LastClickAtUtc = DateTime.MinValue;
        public bool LastClickWasDoubleOnInput;
        public string? LastDoubleClickInputPath;
        public DateTime LastDoubleClickAtUtc = DateTime.MinValue;
    }
}
