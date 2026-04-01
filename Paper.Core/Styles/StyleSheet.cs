namespace Paper.Core.Styles
{
    /// <summary>
    /// CSS-like style properties for a Paper UI node.
    /// All properties are nullable — null means "not explicitly set" / "use default".
    /// Use <see cref="Merge"/> to layer stylesheets (other overrides this, null = inherit).
    /// </summary>
    public sealed record StyleSheet
    {
        // ── Box model ─────────────────────────────────────────────────────────

        public BoxSizing? BoxSizing { get; init; }

        // ── Dimensions ───────────────────────────────────────────────────────

        public Length? Width { get; init; }
        public Length? Height { get; init; }
        public Length? MinWidth { get; init; }
        public Length? MinHeight { get; init; }
        public Length? MaxWidth { get; init; }
        public Length? MaxHeight { get; init; }

        // ── Spacing ───────────────────────────────────────────────────────────

        public Thickness? Padding { get; init; }
        /// <summary>Individual padding sides — override the corresponding side from <see cref="Padding"/> when set.</summary>
        public Length? PaddingTop { get; init; }
        public Length? PaddingRight { get; init; }
        public Length? PaddingBottom { get; init; }
        public Length? PaddingLeft { get; init; }

        public Thickness? Margin { get; init; }
        /// <summary>Individual margin sides — override the corresponding side from <see cref="Margin"/> when set.</summary>
        public Length? MarginTop { get; init; }
        public Length? MarginRight { get; init; }
        public Length? MarginBottom { get; init; }
        public Length? MarginLeft { get; init; }

        // ── Display & Position ────────────────────────────────────────────────

        public Display? Display { get; init; }
        public Position? Position { get; init; }

        public Length? Top { get; init; }
        public Length? Right { get; init; }
        public Length? Bottom { get; init; }
        public Length? Left { get; init; }

        public int? ZIndex { get; init; }

        // ── Flexbox ───────────────────────────────────────────────────────────

        public FlexDirection? FlexDirection { get; init; }
        public FlexWrap? FlexWrap { get; init; }
        public JustifyContent? JustifyContent { get; init; }
        public AlignItems? AlignItems { get; init; }
        public AlignContent? AlignContent { get; init; }
        public AlignSelf? AlignSelf { get; init; }

        public float? FlexGrow { get; init; }
        public float? FlexShrink { get; init; }
        public Length? FlexBasis { get; init; }

        // ── CSS Grid ──────────────────────────────────────────────────────────

        public string? GridTemplateColumns { get; init; }
        public string? GridTemplateRows { get; init; }

        /// <summary>1-based column start line (0 = auto).</summary>
        public int GridColumnStart { get; init; } = 0;
        /// <summary>1-based column end line (0 = auto).</summary>
        public int GridColumnEnd { get; init; } = 0;
        /// <summary>1-based row start line (0 = auto).</summary>
        public int GridRowStart { get; init; } = 0;
        /// <summary>1-based row end line (0 = auto).</summary>
        public int GridRowEnd { get; init; } = 0;

        public Length? ColumnGap { get; init; }
        public Length? RowGap { get; init; }
        /// <summary>Shorthand gap applied to both row and column when set.</summary>
        public Length? Gap
        {
            get => RowGap ?? ColumnGap;
            init { ColumnGap = value; RowGap = value; }
        }

        public JustifyItems? JustifyItems { get; init; }

        // ── Visual ────────────────────────────────────────────────────────────

        public PaperColour? Background { get; init; }
        public string? BackgroundImage { get; init; }
        public ObjectFit? BackgroundSize { get; init; }
        public ObjectFit? ObjectFit { get; init; }
        public PaperColour? Color { get; init; }
        public float? Opacity { get; init; }

        public BorderEdges? Border { get; init; }
        /// <summary>Individual border sides — override the corresponding side from <see cref="Border"/> when set.</summary>
        public Border? BorderTop { get; init; }
        public Border? BorderRight { get; init; }
        public Border? BorderBottom { get; init; }
        public Border? BorderLeft { get; init; }
        public float? BorderRadius { get; init; }
        public float? BorderTopLeftRadius { get; init; }
        public float? BorderTopRightRadius { get; init; }
        public float? BorderBottomRightRadius { get; init; }
        public float? BorderBottomLeftRadius { get; init; }

        public BoxShadow[]? BoxShadow { get; init; }

        public Overflow? OverflowX { get; init; }
        public Overflow? OverflowY { get; init; }
        public Overflow Overflow
        {
            init { OverflowX = value; OverflowY = value; }
        }

        // ── Text / Font ───────────────────────────────────────────────────────

        public string? FontFamily { get; init; }
        public Length? FontSize { get; init; }
        public FontWeight? FontWeight { get; init; }
        public FontStyle? FontStyle { get; init; }
        public float? LineHeight { get; init; }
        public float? LetterSpacing { get; init; }
        public TextAlign? TextAlign { get; init; }
        public TextOverflow? TextOverflow { get; init; }
        public TextTransform? TextTransform { get; init; }
        public WhiteSpace? WhiteSpace { get; init; }
        public Direction? Direction { get; init; }

        // ── Dimensions (aspect ratio) ─────────────────────────────────────────

        /// <summary>
        /// Locks the width:height ratio. When only width is set, height = width / AspectRatio.
        /// When only height is set, width = height * AspectRatio.
        /// </summary>
        public float? AspectRatio { get; init; }

        // ── Interaction ───────────────────────────────────────────────────────

        public Cursor? Cursor { get; init; }
        public UserSelect? UserSelect { get; init; }
        public Visibility? Visibility { get; init; }
        public PointerEvents? PointerEvents { get; init; }

        // ── Transform ────────────────────────────────────────────────────────

        public float? TranslateX { get; init; }
        public float? TranslateY { get; init; }
        public float? Rotate { get; init; }
        public float? ScaleX { get; init; }
        public float? ScaleY { get; init; }

        // ── Transition ────────────────────────────────────────────────────────

        /// <summary>
        /// CSS transition spec, e.g. "background 0.2s" or "all 0.15s" or "background 0.2s, opacity 0.3s".
        /// Parsed at render time by FiberRenderer to smoothly interpolate between style states.
        /// </summary>
        public string? Transition { get; init; }

        // ── Convenience factory ───────────────────────────────────────────────

        public static readonly StyleSheet Empty = new();

        /// <summary>Create a flex-column container with optional gap.</summary>
        public static StyleSheet Column(Length? gap = null) => new()
        {
            Display = Styles.Display.Flex,
            FlexDirection = Styles.FlexDirection.Column,
            RowGap = gap,
        };

        /// <summary>Create a flex-row container with optional gap.</summary>
        public static StyleSheet Row(Length? gap = null) => new()
        {
            Display = Styles.Display.Flex,
            FlexDirection = Styles.FlexDirection.Row,
            ColumnGap = gap,
        };

        /// <summary>
        /// Merge <paramref name="other"/> on top of this stylesheet.
        /// <paramref name="other"/> wins for every property it has explicitly set (non-null).
        /// Null properties in <paramref name="other"/> fall back to this stylesheet's values.
        /// </summary>
        public StyleSheet Merge(StyleSheet other) => new StyleSheet
        {
            BoxSizing     = other.BoxSizing     ?? BoxSizing,
            Width         = other.Width         ?? Width,
            Height        = other.Height        ?? Height,
            MinWidth      = other.MinWidth      ?? MinWidth,
            MinHeight     = other.MinHeight     ?? MinHeight,
            MaxWidth      = other.MaxWidth      ?? MaxWidth,
            MaxHeight     = other.MaxHeight     ?? MaxHeight,
            Padding       = other.Padding       ?? Padding,
            PaddingTop    = other.PaddingTop    ?? PaddingTop,
            PaddingRight  = other.PaddingRight  ?? PaddingRight,
            PaddingBottom = other.PaddingBottom ?? PaddingBottom,
            PaddingLeft   = other.PaddingLeft   ?? PaddingLeft,
            Margin        = other.Margin        ?? Margin,
            MarginTop     = other.MarginTop     ?? MarginTop,
            MarginRight   = other.MarginRight   ?? MarginRight,
            MarginBottom  = other.MarginBottom  ?? MarginBottom,
            MarginLeft    = other.MarginLeft    ?? MarginLeft,
            Display       = other.Display       ?? Display,
            Position      = other.Position      ?? Position,
            Top           = other.Top           ?? Top,
            Right         = other.Right         ?? Right,
            Bottom        = other.Bottom        ?? Bottom,
            Left          = other.Left          ?? Left,
            ZIndex        = other.ZIndex        ?? ZIndex,
            FlexDirection = other.FlexDirection ?? FlexDirection,
            FlexWrap      = other.FlexWrap      ?? FlexWrap,
            JustifyContent= other.JustifyContent?? JustifyContent,
            AlignItems    = other.AlignItems    ?? AlignItems,
            AlignContent  = other.AlignContent  ?? AlignContent,
            AlignSelf     = other.AlignSelf     ?? AlignSelf,
            FlexGrow      = other.FlexGrow      ?? FlexGrow,
            FlexShrink    = other.FlexShrink    ?? FlexShrink,
            FlexBasis     = other.FlexBasis     ?? FlexBasis,
            GridTemplateColumns = other.GridTemplateColumns ?? GridTemplateColumns,
            GridTemplateRows    = other.GridTemplateRows    ?? GridTemplateRows,
            GridColumnStart     = other.GridColumnStart != 0 ? other.GridColumnStart : GridColumnStart,
            GridColumnEnd       = other.GridColumnEnd   != 0 ? other.GridColumnEnd   : GridColumnEnd,
            GridRowStart        = other.GridRowStart    != 0 ? other.GridRowStart    : GridRowStart,
            GridRowEnd          = other.GridRowEnd      != 0 ? other.GridRowEnd      : GridRowEnd,
            ColumnGap     = other.ColumnGap     ?? ColumnGap,
            RowGap        = other.RowGap        ?? RowGap,
            JustifyItems  = other.JustifyItems  ?? JustifyItems,
            Background    = other.Background    ?? Background,
            BackgroundImage = other.BackgroundImage ?? BackgroundImage,
            BackgroundSize  = other.BackgroundSize  ?? BackgroundSize,
            ObjectFit     = other.ObjectFit     ?? ObjectFit,
            Color         = other.Color         ?? Color,
            Opacity       = other.Opacity       ?? Opacity,
            Border        = other.Border        ?? Border,
            BorderTop     = other.BorderTop     ?? BorderTop,
            BorderRight   = other.BorderRight   ?? BorderRight,
            BorderBottom  = other.BorderBottom  ?? BorderBottom,
            BorderLeft    = other.BorderLeft    ?? BorderLeft,
            BorderRadius  = other.BorderRadius  ?? BorderRadius,
            BorderTopLeftRadius     = other.BorderTopLeftRadius     ?? BorderTopLeftRadius,
            BorderTopRightRadius    = other.BorderTopRightRadius    ?? BorderTopRightRadius,
            BorderBottomRightRadius = other.BorderBottomRightRadius ?? BorderBottomRightRadius,
            BorderBottomLeftRadius  = other.BorderBottomLeftRadius  ?? BorderBottomLeftRadius,
            BoxShadow     = other.BoxShadow     ?? BoxShadow,
            OverflowX     = other.OverflowX     ?? OverflowX,
            OverflowY     = other.OverflowY     ?? OverflowY,
            FontFamily    = other.FontFamily    ?? FontFamily,
            FontSize      = other.FontSize      ?? FontSize,
            FontWeight    = other.FontWeight    ?? FontWeight,
            FontStyle     = other.FontStyle     ?? FontStyle,
            LineHeight    = other.LineHeight    ?? LineHeight,
            LetterSpacing = other.LetterSpacing ?? LetterSpacing,
            TextAlign     = other.TextAlign     ?? TextAlign,
            TextOverflow  = other.TextOverflow  ?? TextOverflow,
            TextTransform = other.TextTransform ?? TextTransform,
            WhiteSpace    = other.WhiteSpace    ?? WhiteSpace,
            Direction     = other.Direction     ?? Direction,
            AspectRatio   = other.AspectRatio   ?? AspectRatio,
            Cursor        = other.Cursor        ?? Cursor,
            UserSelect   = other.UserSelect     ?? UserSelect,
            Visibility   = other.Visibility     ?? Visibility,
            PointerEvents = other.PointerEvents ?? PointerEvents,
            TranslateX    = other.TranslateX    ?? TranslateX,
            TranslateY    = other.TranslateY    ?? TranslateY,
            Rotate        = other.Rotate        ?? Rotate,
            ScaleX        = other.ScaleX        ?? ScaleX,
            ScaleY        = other.ScaleY        ?? ScaleY,
            Transition    = other.Transition    ?? Transition,
        };
    }
}
