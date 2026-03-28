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

        /// <summary>X coordinate relative to the hit-test target element's AbsoluteX.</summary>
        public float LocalX { get; set; }
        /// <summary>Y coordinate relative to the hit-test target element's AbsoluteY.</summary>
        public float LocalY { get; set; }

        /// <summary>Pixel width of the hit-test target element (from its layout box).</summary>
        public float TargetWidth { get; set; }
        /// <summary>Pixel height of the hit-test target element (from its layout box).</summary>
        public float TargetHeight { get; set; }

        public int Button { get; init; } = -1; // 0=left,1=right,2=middle

        public float WheelDeltaX { get; init; }
        public float WheelDeltaY { get; init; }

        public bool Shift { get; init; }
        public bool Ctrl  { get; init; }
        public bool Alt   { get; init; }
    }
}

