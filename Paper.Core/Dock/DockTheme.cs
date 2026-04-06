using Paper.Core.Styles;

namespace Paper.Core.Dock
{
    // ── DockTheme ─────────────────────────────────────────────────────────────
    //
    // Colour + size tokens for the dock system.
    // Pass a DockTheme to DockContext.Root() to customise the look.
    // Two built-in presets: DockTheme.Dark (default) and DockTheme.Light.
    // ─────────────────────────────────────────────────────────────────────────

    public sealed record DockTheme
    {
        // ── Colours ───────────────────────────────────────────────────────────
        public PaperColour Bg           { get; init; } = new("#1a1a2e");
        public PaperColour Header       { get; init; } = new("#252545");
        public PaperColour HeaderHover  { get; init; } = new("#2d2d58");
        public PaperColour Border       { get; init; } = new("#3a3a5a");
        public PaperColour Text         { get; init; } = new("#c8c8e0");
        public PaperColour TextDim      { get; init; } = new("#7878a0");
        public PaperColour Handle       { get; init; } = new("#303050");
        public PaperColour HandleHover  { get; init; } = new("#5060b0");
        public PaperColour TabActive    { get; init; } = new("#3a3a70");
        public PaperColour TabHover     { get; init; } = new("#2a2a55");
        public PaperColour DropZone     { get; init; } = new(0.3f, 0.5f, 1f, 0.22f);
        public PaperColour DropCenter   { get; init; } = new(0.3f, 0.8f, 0.5f, 0.22f);
        public PaperColour DropBorder   { get; init; } = new(0.4f, 0.6f, 1f, 0.7f);
        public PaperColour Float        { get; init; } = new("#1e1e38");
        public PaperColour FloatBorder  { get; init; } = new("#4a4a80");
        public PaperColour MinStrip     { get; init; } = new("#141428");
        public PaperColour ButtonHover  { get; init; } = new("#404070");
        public PaperColour CloseHover   { get; init; } = new(0.8f, 0.2f, 0.2f, 1f);

        // ── Sizes (px) ────────────────────────────────────────────────────────
        public float HandlePx   { get; init; } = 5f;
        public float HeaderPx   { get; init; } = 30f;
        public float TabBarPx   { get; init; } = 28f;
        public float MinStripPx { get; init; } = 24f;

        // ── Built-in presets ──────────────────────────────────────────────────

        public static readonly DockTheme Dark = new();

        public static readonly DockTheme Light = new()
        {
            Bg          = new("#f0f0f5"),
            Header      = new("#dcdcec"),
            HeaderHover = new("#c8c8e0"),
            Border      = new("#b0b0c8"),
            Text        = new("#1a1a2e"),
            TextDim     = new("#606080"),
            Handle      = new("#c0c0d8"),
            HandleHover = new("#8080c0"),
            TabActive   = new("#c0c0e8"),
            TabHover    = new("#d0d0e8"),
            DropZone    = new(0.2f, 0.4f, 0.9f, 0.18f),
            DropCenter  = new(0.2f, 0.7f, 0.4f, 0.18f),
            DropBorder  = new(0.3f, 0.5f, 0.9f, 0.7f),
            Float       = new("#e8e8f4"),
            FloatBorder = new("#9090c0"),
            MinStrip    = new("#d8d8ec"),
            ButtonHover = new("#c0c0e0"),
            CloseHover  = new(0.9f, 0.3f, 0.3f, 1f),
        };
    }
}
