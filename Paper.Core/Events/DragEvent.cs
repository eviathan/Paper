namespace Paper.Core.Events
{
    public enum DragEventType
    {
        DragStart,
        Drag,
        DragEnd,
        DragEnter,
        DragOver,
        DragLeave,
        Drop,
    }

    /// <summary>
    /// Event fired during drag-and-drop interactions.
    /// <see cref="X"/> / <see cref="Y"/> are window-relative pointer coordinates.
    /// <see cref="Data"/> carries an arbitrary payload set during DragStart.
    /// </summary>
    public sealed class DragEvent : SyntheticEvent
    {
        public DragEventType Type { get; init; }

        public float X { get; init; }
        public float Y { get; init; }

        /// <summary>Pointer position relative to the event target's top-left corner.</summary>
        public float LocalX { get; set; }
        public float LocalY { get; set; }

        /// <summary>Layout dimensions of the event target.</summary>
        public float TargetWidth  { get; set; }
        public float TargetHeight { get; set; }

        /// <summary>Arbitrary payload attached by the drag source in OnDragStart.</summary>
        public object? Data { get; set; }

        /// <summary>
        /// True when a DragEnd fires because the mouse was released outside the source window's bounds.
        /// Used by the dock system to eject panels to a new window.
        /// </summary>
        public bool OutsideSourceWindow { get; set; } = false;

        /// <summary>Screen-absolute pointer position at DragEnd, used for cross-window drop detection.</summary>
        public int ScreenX { get; set; }
        public int ScreenY { get; set; }
    }
}
