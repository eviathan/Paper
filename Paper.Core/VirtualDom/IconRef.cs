namespace Paper.Core.VirtualDom;

/// <summary>
/// A reference to a react-icons icon, carrying the set name (e.g. "fa") and icon name (e.g. "FaHome").
/// Obtain instances from the generated <c>Icons.*</c> static classes in <c>Paper.Icons</c>.
/// </summary>
public readonly record struct IconRef(string Set, string Name);
