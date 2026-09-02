namespace Paper.Core.Hooks;

/// <summary>
/// Global pre-filter for raw key events, evaluated before the shortcut registry.
/// Set CapsLockActive to true (toggled automatically on Key.CapsLock press) and wire
/// KeyDown / KeyUp to intercept keys in musical-keyboard mode or similar.
/// Return true from either handler to consume the event and suppress shortcut dispatch.
/// </summary>
public static class GlobalKeyFilter
{
    public static bool CapsLockActive { get; set; }
    public static Func<string, bool>? KeyDown { get; set; }
    public static Func<string, bool>? KeyUp   { get; set; }
}
