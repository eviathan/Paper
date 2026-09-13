using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using Paper.Core.VirtualDom;

namespace Paper.Icons;

/// <summary>
/// Loads and caches icon SVG data from compressed embedded resources.
/// Each icon set (fa, md, bs, etc.) is loaded lazily on first access.
/// </summary>
public static class IconRegistry
{
    private static readonly ConcurrentDictionary<string, Dictionary<string, IconData>?> _sets = new();

    /// <summary>
    /// Returns the <see cref="IconData"/> for the given <paramref name="iconRef"/>, or null if not found.
    /// The icon set is loaded from embedded resources on first access.
    /// </summary>
    public static IconData? Get(IconRef iconRef)
    {
        if (string.IsNullOrEmpty(iconRef.Set) || string.IsNullOrEmpty(iconRef.Name))
            return null;

        var set = GetSet(iconRef.Set);
        if (set == null) return null;
        set.TryGetValue(iconRef.Name, out var data);
        return data;
    }

    /// <summary>Returns all icon names in the given set, or an empty enumerable if the set is unknown.</summary>
    public static IEnumerable<string> GetNames(string setName)
    {
        var set = GetSet(setName);
        return set?.Keys ?? (IEnumerable<string>)[];
    }

    private static Dictionary<string, IconData>? GetSet(string setName)
    {
        if (_sets.TryGetValue(setName, out var cached)) return cached;

        var resourceName = $"Paper.Icons.Resources.icons.{setName}.json.gz";
        using var stream = typeof(IconRegistry).Assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _sets[setName] = null;
            return null;
        }

        try
        {
            using var gz   = new GZipStream(stream, CompressionMode.Decompress);
            var data = JsonSerializer.Deserialize<Dictionary<string, IconData>>(gz);
            _sets[setName] = data;
            return data;
        }
        catch
        {
            _sets[setName] = null;
            return null;
        }
    }
}
