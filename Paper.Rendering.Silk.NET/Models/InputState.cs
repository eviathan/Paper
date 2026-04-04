using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paper.Core.Reconciler;

namespace Paper.Rendering.Silk.NET.Models
{
    public class InputState
    {
        public const int CARET_IDLE_MS = 500;
        public const int CARET_BLINK_PERIOD_MS = 1000;
        public const int CARET_BLINK_ON_MS = 500;
        
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