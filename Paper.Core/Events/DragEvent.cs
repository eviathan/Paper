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

        /// <summary>Arbitrary payload attached by the drag source in OnDragStart.</summary>
        public object? Data { get; init; }
    }
}
