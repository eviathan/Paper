using Paper.Core.Reconciler;

namespace Paper.Rendering.Silk.NET.Models
{
    public class InputState
    {
        public const int CaretIdleMs = 500;
        public const int CaretBlinkPeriodMs = 1000;
        public const int CaretBlinkOnMs = 500;

        public Fiber? Focused;
        public string? FocusedPath;
        public int InputCaret;
        public int InputSelStart;
        public int InputSelEnd;
        public int InputSelAnchor;
        public bool InputSelecting;
        public float InputScrollX;
        public string? InputText;
        public long LastInputActivityTicks;
        public Timer? CaretBlinkTimer;
    }
}
