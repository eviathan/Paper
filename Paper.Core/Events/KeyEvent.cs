namespace Paper.Core.Events
{
    public enum KeyEventType
    {
        Down,
        Up,
        Char,
    }

    public sealed class KeyEvent : SyntheticEvent
    {
        public KeyEventType Type { get; init; }
        public string Key { get; init; } = "";
        public char? Char { get; init; }

        public bool Shift { get; init; }
        public bool Ctrl  { get; init; }
        public bool Alt   { get; init; }
    }
}

