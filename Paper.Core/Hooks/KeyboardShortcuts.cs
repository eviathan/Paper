namespace Paper.Core.Hooks
{
    /// <summary>
    /// Specifies modifier keys for keyboard shortcuts.
    /// </summary>
    [Flags]
    public enum ShortcutModifiers
    {
        None = 0,
        Ctrl = 1,
        Alt = 2,
        Shift = 4,
        Meta = 8,
    }

    /// <summary>
    /// Represents a keyboard shortcut with key and optional modifiers.
    /// </summary>
    public readonly struct KeyboardShortcut : IEquatable<KeyboardShortcut>
    {
        public required string Key { get; init; }
        public ShortcutModifiers Modifiers { get; init; }

        public KeyboardShortcut(string key, ShortcutModifiers modifiers = ShortcutModifiers.None)
        {
            Key = key;
            Modifiers = modifiers;
        }

        public static KeyboardShortcut Ctrl(string key) => new() { Key = key, Modifiers = ShortcutModifiers.Ctrl };
        public static KeyboardShortcut Alt(string key) => new() { Key = key, Modifiers = ShortcutModifiers.Alt };
        public static KeyboardShortcut Shift(string key) => new() { Key = key, Modifiers = ShortcutModifiers.Shift };
        public static KeyboardShortcut Meta(string key) => new() { Key = key, Modifiers = ShortcutModifiers.Meta };
        public static KeyboardShortcut CtrlShift(string key) => new() { Key = key, Modifiers = ShortcutModifiers.Ctrl | ShortcutModifiers.Shift };
        public static KeyboardShortcut CtrlAlt(string key) => new() { Key = key, Modifiers = ShortcutModifiers.Ctrl | ShortcutModifiers.Alt };

        public bool Matches(string key, bool ctrl, bool alt, bool shift, bool meta)
        {
            if (!Key.Equals(key, StringComparison.OrdinalIgnoreCase)) return false;

            bool hasCtrl = (Modifiers & ShortcutModifiers.Ctrl) != 0;
            bool hasAlt = (Modifiers & ShortcutModifiers.Alt) != 0;
            bool hasShift = (Modifiers & ShortcutModifiers.Shift) != 0;
            bool hasMeta = (Modifiers & ShortcutModifiers.Meta) != 0;

            return ctrl == hasCtrl && alt == hasAlt && shift == hasShift && meta == hasMeta;
        }

        public bool Equals(KeyboardShortcut other) => Key == other.Key && Modifiers == other.Modifiers;
        public override bool Equals(object? obj) => obj is KeyboardShortcut other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Key, (int)Modifiers);
        public override string ToString() => $"{Modifiers}+{Key}";
    }

    /// <summary>
    /// Registry for global keyboard shortcuts. Components can register shortcuts that will
    /// be triggered regardless of which element is focused.
    /// </summary>
    public static class KeyboardShortcutRegistry
    {
        private static readonly Dictionary<int, List<(KeyboardShortcut shortcut, Action handler)>> _shortcuts = new();
        private static int _nextId = 1;

        public static int Register(KeyboardShortcut shortcut, Action handler)
        {
            int id = _nextId++;
            if (!_shortcuts.ContainsKey(id))
                _shortcuts[id] = new List<(KeyboardShortcut, Action)>();
            _shortcuts[id].Add((shortcut, handler));
            return id;
        }

        public static void Unregister(int id)
        {
            _shortcuts.Remove(id);
        }

        public static bool TryDispatch(string key, bool ctrl, bool alt, bool shift, bool meta, out bool handled)
        {
            handled = false;
            foreach (var list in _shortcuts.Values)
            {
                foreach (var (shortcut, handler) in list)
                {
                    if (shortcut.Matches(key, ctrl, alt, shift, meta))
                    {
                        handler();
                        handled = true;
                        return true;
                    }
                }
            }
            return false;
        }

        public static void Clear()
        {
            _shortcuts.Clear();
        }
    }
}
