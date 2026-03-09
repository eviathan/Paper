namespace Paper.Core.Styles
{
    // ── Display ──────────────────────────────────────────────────────────────

    public enum Display
    {
        Block,
        Flex,
        Grid,
        None,
        Inline,
        InlineFlex,
    }

    // ── Positioning ───────────────────────────────────────────────────────────

    public enum Position
    {
        Static,
        Relative,
        Absolute,
        Fixed,
        Sticky,
    }

    // ── Flexbox ───────────────────────────────────────────────────────────────

    public enum FlexDirection
    {
        Row,
        RowReverse,
        Column,
        ColumnReverse,
    }

    public enum FlexWrap
    {
        NoWrap,
        Wrap,
        WrapReverse,
    }

    public enum JustifyContent
    {
        FlexStart,
        FlexEnd,
        Center,
        SpaceBetween,
        SpaceAround,
        SpaceEvenly,
    }

    public enum AlignItems
    {
        FlexStart,
        FlexEnd,
        Center,
        Stretch,
        Baseline,
    }

    public enum AlignSelf
    {
        Auto,
        FlexStart,
        FlexEnd,
        Center,
        Stretch,
        Baseline,
    }

    public enum AlignContent
    {
        FlexStart,
        FlexEnd,
        Center,
        SpaceBetween,
        SpaceAround,
        Stretch,
    }

    // ── Grid ─────────────────────────────────────────────────────────────────

    public enum JustifyItems
    {
        Start,
        End,
        Center,
        Stretch,
    }

    // ── Overflow ──────────────────────────────────────────────────────────────

    public enum Overflow
    {
        Visible,
        Hidden,
        Scroll,
        Auto,
    }

    /// <summary>How an image or background-image fits inside its box (CSS object-fit / background-size).</summary>
    public enum ObjectFit
    {
        /// <summary>Stretch to fill the box; may distort.</summary>
        Fill,
        /// <summary>Scale to fit inside the box; aspect ratio preserved; may letterbox.</summary>
        Contain,
        /// <summary>Scale to cover the box; aspect ratio preserved; may crop.</summary>
        Cover,
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    public enum TextAlign
    {
        Left,
        Right,
        Center,
        Justify,
    }

    public enum FontWeight
    {
        Thin       = 100,
        ExtraLight = 200,
        Light      = 300,
        Normal     = 400,
        Medium     = 500,
        SemiBold   = 600,
        Bold       = 700,
        ExtraBold  = 800,
        Black      = 900,
    }

    public enum TextOverflow
    {
        Clip,
        Ellipsis,
    }

    // ── Border ────────────────────────────────────────────────────────────────

    public enum BorderStyle
    {
        None,
        Solid,
        Dashed,
        Dotted,
    }

    // ── Cursor ────────────────────────────────────────────────────────────────

    public enum Cursor
    {
        Default,
        Pointer,
        Text,
        Move,
        NotAllowed,
        Crosshair,
        Grab,
        Grabbing,
        Wait,
        Help,
        None,
    }

    // ── Visibility / Pointer Events ───────────────────────────────────────────

    public enum Visibility
    {
        Visible,
        Hidden,
    }

    public enum PointerEvents
    {
        Auto,
        None,
    }

    // ── Box Sizing ────────────────────────────────────────────────────────────

    public enum BoxSizing
    {
        /// <summary>Width/Height exclude padding and border (default CSS).</summary>
        ContentBox,
        /// <summary>Width/Height include padding and border (IE / border-box model).</summary>
        BorderBox,
    }

    // ── White Space ───────────────────────────────────────────────────────────

    public enum WhiteSpace
    {
        Normal,
        NoWrap,
        Pre,
        PreWrap,
    }
}
