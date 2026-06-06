using System.Text.Json.Serialization;

namespace Paper.Icons;

/// <summary>
/// Raw icon data extracted from react-icons: viewBox dimensions + SVG path strings.
/// Loaded lazily from compressed embedded resources, one set per file.
/// </summary>
public sealed class IconData
{
    /// <summary>ViewBox width (e.g. 576 for a 576x512 icon).</summary>
    [JsonPropertyName("w")] public float W { get; set; }

    /// <summary>ViewBox height (e.g. 512 for a 576x512 icon).</summary>
    [JsonPropertyName("h")] public float H { get; set; }

    /// <summary>SVG path data strings (one per shape in the icon).</summary>
    [JsonPropertyName("p")] public string[] P { get; set; } = [];

    /// <summary>
    /// Per-path fill colors (CSS color strings). Only present for colored icons (e.g. Fc set).
    /// Null entries inherit the current color. When this property is null, all paths are tintable.
    /// </summary>
    [JsonPropertyName("f")] public string?[]? F { get; set; }

    /// <summary>True if this icon has fixed fill colors per path (not tintable).</summary>
    [JsonIgnore]
    public bool IsColored => F != null && F.Any(f => f != null);
}
