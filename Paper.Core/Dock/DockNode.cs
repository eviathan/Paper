using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paper.Core.Dock
{
    // ── DockNode discriminated union ──────────────────────────────────────────
    //
    //   SplitNode    — two children arranged horizontally or vertically
    //   TabGroupNode — N panels sharing the same space with a tab bar
    //   PanelNode    — a single leaf: title, content id, minimized/maximized
    //   FloatNode    — a panel torn off into a floating overlay
    //
    // The tree is immutable; all mutations produce a new tree via DockReducer.
    // ─────────────────────────────────────────────────────────────────────────

    public enum DockDirection { Horizontal, Vertical }

    public enum DropZone { Left, Top, Right, Bottom, Center, None }

    /// <summary>Which screen edge an auto-hidden panel is pinned to.</summary>
    public enum AutoHideEdge { Left, Right, Top, Bottom }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(SplitNode),    "split")]
    [JsonDerivedType(typeof(TabGroupNode), "tabs")]
    [JsonDerivedType(typeof(PanelNode),    "panel")]
    [JsonDerivedType(typeof(FloatNode),    "float")]
    public abstract class DockNode
    {
        /// <summary>Unique id used as the React-style key so reconciler reuses fibers on mutation.</summary>
        public string NodeId { get; init; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>Serialize this layout tree to JSON (indented for readability).</summary>
        public string Serialize() =>
            JsonSerializer.Serialize(this, DockNodeJsonContext.Default.DockNode);

        /// <summary>Deserialize a layout tree from JSON.</summary>
        public static DockNode Deserialize(string json) =>
            JsonSerializer.Deserialize(json, DockNodeJsonContext.Default.DockNode)
                ?? throw new InvalidOperationException("Failed to deserialize dock layout.");
    }

    /// <summary>Two children split along <see cref="Direction"/>.</summary>
    public sealed class SplitNode : DockNode
    {
        /// <summary>Horizontal = children sit side-by-side; Vertical = stacked.</summary>
        public DockDirection Direction { get; init; } = DockDirection.Horizontal;

        /// <summary>Fraction [0.05 .. 0.95] allocated to <see cref="First"/>.</summary>
        public float Ratio { get; set; } = 0.5f;

        public DockNode First  { get; set; } = new PanelNode { PanelId = "empty" };
        public DockNode Second { get; set; } = new PanelNode { PanelId = "empty" };
    }

    /// <summary>N panels sharing the same space, selected by tab.</summary>
    public sealed class TabGroupNode : DockNode
    {
        public List<PanelNode> Tabs        { get; set; } = new();
        public int             ActiveIndex { get; set; } = 0;

        public PanelNode? ActiveTab =>
            Tabs.Count > 0 ? Tabs[Math.Clamp(ActiveIndex, 0, Tabs.Count - 1)] : null;
    }

    /// <summary>A single leaf panel.</summary>
    public sealed class PanelNode : DockNode
    {
        /// <summary>Key into the panel registry (maps to content factory).</summary>
        public string PanelId   { get; init; } = "";
        public string Title     { get; set; }  = "";
        public bool   Minimized { get; set; }  = false;
        public bool   Maximized { get; set; }  = false;
    }

    /// <summary>A panel torn off into a floating portal overlay.</summary>
    public sealed class FloatNode : DockNode
    {
        public PanelNode Panel { get; set; } = new();
        /// <summary>Position and size in logical pixels from the top-left of the surface.</summary>
        public float X      { get; set; } = 100;
        public float Y      { get; set; } = 100;
        public float Width  { get; set; } = 400;
        public float Height { get; set; } = 300;
    }

    /// <summary>A panel collapsed to an edge strip (auto-hide state).</summary>
    public sealed class AutoHideEntry
    {
        public string       NodeId { get; init; } = Guid.NewGuid().ToString("N")[..8];
        public PanelNode    Panel  { get; init; } = new();
        public AutoHideEdge Edge   { get; init; }
    }

    // ── JSON serialization context ────────────────────────────────────────────

    [JsonSerializable(typeof(DockNode))]
    [JsonSerializable(typeof(SplitNode))]
    [JsonSerializable(typeof(TabGroupNode))]
    [JsonSerializable(typeof(PanelNode))]
    [JsonSerializable(typeof(FloatNode))]
    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    internal partial class DockNodeJsonContext : JsonSerializerContext { }

    // ── Layout preset helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Named layout preset — title + serialized node tree.
    /// Save/load via <see cref="Serialize"/>/<see cref="Deserialize"/>.
    /// </summary>
    public sealed class DockLayoutPreset
    {
        public string Name   { get; init; } = "";
        public string Layout { get; init; } = "";   // DockNode.Serialize() output
        /// <summary>Floating panels are separate from the root tree.</summary>
        public List<string> Floats { get; init; } = new();

        public string Serialize() =>
            JsonSerializer.Serialize(this, DockPresetJsonContext.Default.DockLayoutPreset);

        public static DockLayoutPreset Deserialize(string json) =>
            JsonSerializer.Deserialize(json, DockPresetJsonContext.Default.DockLayoutPreset)
                ?? throw new InvalidOperationException("Failed to deserialize preset.");
    }

    [JsonSerializable(typeof(DockLayoutPreset))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal partial class DockPresetJsonContext : JsonSerializerContext { }
}
