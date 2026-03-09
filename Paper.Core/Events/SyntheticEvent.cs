namespace Paper.Core.Events
{
    public abstract class SyntheticEvent
    {
        public bool PropagationStopped { get; private set; }
        public bool DefaultPrevented   { get; private set; }

        public EventPhase Phase { get; set; } = EventPhase.AtTarget;

        public void StopPropagation() => PropagationStopped = true;
        public void PreventDefault()  => DefaultPrevented   = true;
    }
}

