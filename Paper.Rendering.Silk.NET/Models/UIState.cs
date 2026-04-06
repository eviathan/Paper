using Paper.Core.Reconciler;

namespace Paper.Rendering.Silk.NET.Models
{
    public class UIState
    {
        public Fiber? Hovered;
        public string? HoveredPath;
        public Fiber? Pressed;
        public string? PressedPath;
        public Fiber? Focused;
        public string? FocusedPath;
        public Fiber? PointerDownFiber;
        public Fiber? DragSource;
        public string? DragSourcePath;
        public object? DragData;
        public bool DragActive;
        public float DragStartX;
        public float DragStartY;
        public float DragCursorX;
        public float DragCursorY;
        public Fiber? DragOver;
        public string? DragOverPath;
    }
}
