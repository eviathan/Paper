namespace Paper.Core.Events
{
    public enum PointerEventType
    {
        Move,
        Down,
        Up,
        Click,
        DoubleClick,
        Enter,
        Leave,
        Wheel,
    }

    public sealed class PointerEvent : SyntheticEvent
    {
        public PointerEventType Type { get; init; }

        public float X { get; init; }
        public float Y { get; init; }

        public int Button { get; init; } = -1; // 0=left,1=right,2=middle

        public float WheelDeltaX { get; init; }
        public float WheelDeltaY { get; init; }

        public bool Shift { get; init; }
        public bool Ctrl  { get; init; }
        public bool Alt   { get; init; }
    }
}

