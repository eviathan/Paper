using Paper.Core.Styles;
using Paper.CSSS.Preprocessor;
using System.Globalization;

namespace Paper.CSSS
{
    /// <summary>
    /// Maps flat <see cref="Preprocessor.CSSSRule"/>s to <see cref="StyleSheet"/> instances by translating
    /// CSS property names and value strings into typed StyleSheet properties.
    /// </summary>
    internal static class StyleSheetMapper
    {
        public static Dictionary<string, StyleSheet> MapRules(List<Preprocessor.CSSSRule> rules)
        {
            var result = new Dictionary<string, StyleSheet>(StringComparer.Ordinal);

            foreach (var rule in rules)
            {
                foreach (var selector in rule.Selectors)
                {
                    if (!result.TryGetValue(selector, out var existing))
                        existing = StyleSheet.Empty;

                    foreach (var (prop, val) in rule.Declarations)
                        existing = ApplyDeclaration(existing, prop, val);

                    result[selector] = existing;
                }
            }

            return result;
        }

        public static StyleSheet ApplyDeclaration(StyleSheet style, string property, string value)
        {
            string p = property.Trim().ToLowerInvariant();
            string v = value.Trim();

            return p switch
            {
                // ── Display & position ─────────────────────────────────────────
                "display"   => style with { Display  = ParseDisplay(v) },
                "position"  => style with { Position = ParsePosition(v) },
                "z-index"   => style with { ZIndex   = ParseInt(v, 0) },
                "visibility"=> style with { Visibility = v == "hidden" ? Visibility.Hidden : Visibility.Visible },

                // ── Box model ─────────────────────────────────────────────────
                "box-sizing" => style with { BoxSizing = v == "content-box" ? BoxSizing.ContentBox : BoxSizing.BorderBox },

                // ── Width / Height ────────────────────────────────────────────
                "width"      => style with { Width     = ParseLength(v) },
                "height"     => style with { Height    = ParseLength(v) },
                "min-width"  => style with { MinWidth  = ParseLength(v) },
                "min-height" => style with { MinHeight = ParseLength(v) },
                "max-width"  => style with { MaxWidth  = ParseLength(v) },
                "max-height" => style with { MaxHeight = ParseLength(v) },

                // ── Padding ───────────────────────────────────────────────────
                "padding"        => style with { Padding = ParseThickness(v) },
                "padding-top"    => style with { PaddingTop    = ParseLength(v) },
                "padding-right"  => style with { PaddingRight  = ParseLength(v) },
                "padding-bottom" => style with { PaddingBottom = ParseLength(v) },
                "padding-left"   => style with { PaddingLeft   = ParseLength(v) },

                // ── Margin ────────────────────────────────────────────────────
                "margin"        => style with { Margin = ParseThickness(v) },
                "margin-top"    => style with { MarginTop    = ParseLength(v) },
                "margin-right"  => style with { MarginRight  = ParseLength(v) },
                "margin-bottom" => style with { MarginBottom = ParseLength(v) },
                "margin-left"   => style with { MarginLeft   = ParseLength(v) },

                // ── Position offsets ──────────────────────────────────────────
                "top"    => style with { Top    = ParseLength(v) },
                "right"  => style with { Right  = ParseLength(v) },
                "bottom" => style with { Bottom = ParseLength(v) },
                "left"   => style with { Left   = ParseLength(v) },

                // ── Flexbox ───────────────────────────────────────────────────
                "flex-direction"  => style with { FlexDirection  = ParseFlexDirection(v) },
                "flex-wrap"       => style with { FlexWrap       = ParseFlexWrap(v) },
                "justify-content" => style with { JustifyContent = ParseJustifyContent(v) },
                "align-items"     => style with { AlignItems     = ParseAlignItems(v) },
                "align-content"   => style with { AlignContent   = ParseAlignContent(v) },
                "align-self"      => style with { AlignSelf      = ParseAlignSelf(v) },
                "flex-grow"       => style with { FlexGrow       = ParseFloat(v, 0f) },
                "flex-shrink"     => style with { FlexShrink     = ParseFloat(v, 1f) },
                "flex-basis"      => style with { FlexBasis      = ParseLength(v) },
                "flex"            => ApplyFlexShorthand(style, v),

                // ── Grid ──────────────────────────────────────────────────────
                "grid-template-columns"  => style with { GridTemplateColumns = v },
                "grid-template-rows"     => style with { GridTemplateRows    = v },
                "grid-column"            => ApplyGridColumn(style, v),
                "grid-row"               => ApplyGridRow(style, v),
                "grid-column-start"      => style with { GridColumnStart = ParseInt(v, 0) },
                "grid-column-end"        => style with { GridColumnEnd   = ParseInt(v, 0) },
                "grid-row-start"         => style with { GridRowStart    = ParseInt(v, 0) },
                "grid-row-end"           => style with { GridRowEnd      = ParseInt(v, 0) },
                "column-gap"             => style with { ColumnGap = ParseLength(v) },
                "row-gap"                => style with { RowGap    = ParseLength(v) },
                "gap"                    => ApplyGap(style, v),
                "justify-items"          => style with { JustifyItems = ParseJustifyItems(v) },

                // ── Visual ────────────────────────────────────────────────────
                "background"        => style with { Background = ParseColour(v) },
                "background-color"  => style with { Background = ParseColour(v) },
                "background-image"  => style with { BackgroundImage = v.Trim('"', '\'') },
                "background-size"   => style with { BackgroundSize  = ParseObjectFit(v) },
                "object-fit"        => style with { ObjectFit       = ParseObjectFit(v) },
                "color"             => style with { Color      = ParseColour(v) },
                "opacity"           => style with { Opacity    = ParseFloat(v, 1f) },
                "border-radius"     => style with { BorderRadius = ParseFloat(v.Replace("px","").Trim(), 0f) },
                "overflow"          => style with { Overflow   = ParseOverflow(v) },
                "overflow-x"        => style with { OverflowX  = ParseOverflow(v) },
                "overflow-y"        => style with { OverflowY  = ParseOverflow(v) },

                // ── Border ────────────────────────────────────────────────────
                "border"        => style with { Border = ParseBorderEdges(v) },
                "border-top"    => ApplyBorderSide(style, v, Side.Top),
                "border-right"  => ApplyBorderSide(style, v, Side.Right),
                "border-bottom" => ApplyBorderSide(style, v, Side.Bottom),
                "border-left"   => ApplyBorderSide(style, v, Side.Left),

                // ── Text ──────────────────────────────────────────────────────
                "font-family"    => style with { FontFamily    = v.Trim('\'', '"') },
                "font-size"      => style with { FontSize      = ParseLength(v) },
                "font-weight"    => style with { FontWeight    = ParseFontWeight(v) },
                "line-height"    => style with { LineHeight    = ParseFloat(v, 1.4f) },
                "letter-spacing" => style with { LetterSpacing = ParseFloat(v.Replace("px","").Trim(), 0f) },
                "text-align"     => style with { TextAlign     = ParseTextAlign(v) },
                "text-overflow"  => style with { TextOverflow  = v == "ellipsis" ? TextOverflow.Ellipsis : TextOverflow.Clip },
                "white-space"    => style with { WhiteSpace    = ParseWhiteSpace(v) },

                // ── Transform ─────────────────────────────────────────────────
                "transform" => ApplyTransform(style, v),
                "rotate"    => ApplyRotate(style, v),

                // ── Transition ────────────────────────────────────────────────
                "transition" => style with { Transition = v },

                // ── Cursor ────────────────────────────────────────────────────
                "cursor" => style with { Cursor = ParseCursor(v) },

                // ── Interaction states (pointer-events, visibility) ────────────
                "pointer-events" => style with { PointerEvents = v == "none" ? PointerEvents.None : PointerEvents.Auto },

                // Unknown — ignore
                _ => style,
            };
        }

        // ── Shorthands ────────────────────────────────────────────────────────

        private static StyleSheet ApplyFlexShorthand(StyleSheet style, string v)
        {
            // flex: grow shrink basis  |  flex: 1  |  flex: auto
            if (v == "auto")  return style with { FlexGrow = 1, FlexShrink = 1, FlexBasis = Length.Auto };
            if (v == "none")  return style with { FlexGrow = 0, FlexShrink = 0, FlexBasis = Length.Auto };
            var parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                1 => style with { FlexGrow = ParseFloat(parts[0], 0) },
                2 => style with { FlexGrow = ParseFloat(parts[0], 0), FlexShrink = ParseFloat(parts[1], 1) },
                _ => style with {
                    FlexGrow   = ParseFloat(parts[0], 0),
                    FlexShrink = ParseFloat(parts[1], 1),
                    FlexBasis  = ParseLength(parts[2]),
                },
            };
        }

        private static StyleSheet ApplyGridColumn(StyleSheet style, string v)
        {
            var parts = v.Split('/');
            int start = ParseInt(parts[0].Trim(), 0);
            int end   = parts.Length > 1 ? ParseInt(parts[1].Trim(), 0) : 0;
            return style with { GridColumnStart = start, GridColumnEnd = end };
        }

        private static StyleSheet ApplyGridRow(StyleSheet style, string v)
        {
            var parts = v.Split('/');
            int start = ParseInt(parts[0].Trim(), 0);
            int end   = parts.Length > 1 ? ParseInt(parts[1].Trim(), 0) : 0;
            return style with { GridRowStart = start, GridRowEnd = end };
        }

        private static StyleSheet ApplyGap(StyleSheet style, string v)
        {
            var parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var rowGap = ParseLength(parts[0]);
            var colGap = parts.Length > 1 ? ParseLength(parts[1]) : rowGap;
            return style with { RowGap = rowGap, ColumnGap = colGap };
        }

        private static StyleSheet ApplyBorderSide(StyleSheet style, string v, Side side)
        {
            var border = ParseBorder(v);
            var edges  = style.Border ?? new BorderEdges();
            return style with
            {
                Border = side switch
                {
                    Side.Top    => new BorderEdges { Top    = border, Right = edges.Right, Bottom = edges.Bottom, Left = edges.Left },
                    Side.Right  => new BorderEdges { Top = edges.Top, Right  = border, Bottom = edges.Bottom, Left = edges.Left },
                    Side.Bottom => new BorderEdges { Top = edges.Top, Right = edges.Right, Bottom  = border, Left = edges.Left },
                    Side.Left   => new BorderEdges { Top = edges.Top, Right = edges.Right, Bottom = edges.Bottom, Left   = border },
                    _           => edges,
                }
            };
        }

        private static StyleSheet ApplyTransform(StyleSheet style, string v)
        {
            // Only handle translate(x, y) and rotate(deg) for now
            if (v.StartsWith("translate("))
            {
                var inner = v[10..^1];
                var parts = inner.Split(',');
                float tx = ParseFloat(parts[0].Trim().Replace("px",""), 0);
                float ty = parts.Length > 1 ? ParseFloat(parts[1].Trim().Replace("px",""), 0) : 0;
                return style with { TranslateX = tx, TranslateY = ty };
            }
            if (v.StartsWith("rotate("))
            {
                float deg = ParseFloat(v[7..].TrimEnd(')').Replace("deg","").Trim(), 0);
                return style with { Rotate = deg };
            }
            return style;
        }

        private static StyleSheet ApplyRotate(StyleSheet style, string v)
        {
            string t = v.Trim().ToLowerInvariant();
            if (t.EndsWith("deg")  && float.TryParse(t[..^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float deg))
                return style with { Rotate = deg * MathF.PI / 180f };
            if (t.EndsWith("turn") && float.TryParse(t[..^4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float turn))
                return style with { Rotate = turn * 2f * MathF.PI };
            if (t.EndsWith("rad")  && float.TryParse(t[..^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float rad))
                return style with { Rotate = rad };
            return style;
        }

        // ── Parsers ───────────────────────────────────────────────────────────

        private static Length? ParseLength(string v)
        {
            if (string.IsNullOrWhiteSpace(v) || v == "auto") return Length.Auto;
            if (v == "none") return Length.None;
            if (v.EndsWith("px") && float.TryParse(v[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float px))
                return Length.Px(px);
            if (v.EndsWith('%') && float.TryParse(v[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                return Length.Percent(pct);
            if (v.EndsWith("em") && float.TryParse(v[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float em))
                return Length.Em(em);
            if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float bare))
                return Length.Px(bare);
            return null;
        }

        private static Thickness ParseThickness(string v)
        {
            var parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Length L(int i) => ParseLength(parts.Length > i ? parts[i] : "0") ?? Length.Zero;
            return parts.Length switch
            {
                1 => new Thickness(L(0)),
                2 => new Thickness(L(0), L(1)),
                3 => new Thickness(L(0), L(1), L(2), L(1)),
                _ => new Thickness(L(0), L(1), L(2), L(3)),
            };
        }

        private static PaperColour? ParseColour(string v)
        {
            if (string.IsNullOrWhiteSpace(v) || v == "transparent") return PaperColour.Transparent;
            if (v.StartsWith('#')) return new PaperColour(v);
            return v switch
            {
                "black"   => PaperColour.Black,
                "white"   => PaperColour.White,
                "red"     => PaperColour.Red,
                "green"   => PaperColour.Green,
                "blue"    => PaperColour.Blue,
                _         => null,
            };
        }

        private static Border ParseBorder(string v)
        {
            // "1px solid #ff0000"
            var parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            float width = 0;
            var style   = BorderStyle.Solid;
            PaperColour colour = PaperColour.Black;

            foreach (var p in parts)
            {
                if (p.EndsWith("px") && float.TryParse(p[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float bw))
                    width = bw;
                else if (p == "solid")  style = BorderStyle.Solid;
                else if (p == "dashed") style = BorderStyle.Dashed;
                else if (p == "dotted") style = BorderStyle.Dotted;
                else if (p == "none")   style = BorderStyle.None;
                else if (p.StartsWith('#'))
                    colour = new PaperColour(p);
            }
            return new Border(width, colour, style);
        }

        private static BorderEdges ParseBorderEdges(string v) => new BorderEdges(ParseBorder(v));

        private static Display ParseDisplay(string v) => v switch
        {
            "flex"        => Display.Flex,
            "grid"        => Display.Grid,
            "none"        => Display.None,
            "inline"      => Display.Inline,
            "inline-flex" => Display.InlineFlex,
            _             => Display.Block,
        };

        private static Position ParsePosition(string v) => v switch
        {
            "relative" => Position.Relative,
            "absolute" => Position.Absolute,
            "fixed"    => Position.Fixed,
            "sticky"   => Position.Sticky,
            _          => Position.Static,
        };

        private static FlexDirection ParseFlexDirection(string v) => v switch
        {
            "row-reverse"    => FlexDirection.RowReverse,
            "column"         => FlexDirection.Column,
            "column-reverse" => FlexDirection.ColumnReverse,
            _                => FlexDirection.Row,
        };

        private static FlexWrap ParseFlexWrap(string v) => v switch
        {
            "wrap"         => FlexWrap.Wrap,
            "wrap-reverse" => FlexWrap.WrapReverse,
            _              => FlexWrap.NoWrap,
        };

        private static JustifyContent ParseJustifyContent(string v) => v switch
        {
            "flex-end"     => JustifyContent.FlexEnd,
            "center"       => JustifyContent.Center,
            "space-between"=> JustifyContent.SpaceBetween,
            "space-around" => JustifyContent.SpaceAround,
            "space-evenly" => JustifyContent.SpaceEvenly,
            _              => JustifyContent.FlexStart,
        };

        private static AlignItems ParseAlignItems(string v) => v switch
        {
            "flex-end"  => AlignItems.FlexEnd,
            "center"    => AlignItems.Center,
            "baseline"  => AlignItems.Baseline,
            "flex-start"=> AlignItems.FlexStart,
            _           => AlignItems.Stretch,
        };

        private static AlignContent ParseAlignContent(string v) => v switch
        {
            "flex-end"      => AlignContent.FlexEnd,
            "center"        => AlignContent.Center,
            "space-between" => AlignContent.SpaceBetween,
            "space-around"  => AlignContent.SpaceAround,
            "flex-start"    => AlignContent.FlexStart,
            _               => AlignContent.Stretch,
        };

        private static AlignSelf ParseAlignSelf(string v) => v switch
        {
            "flex-start" => AlignSelf.FlexStart,
            "flex-end"   => AlignSelf.FlexEnd,
            "center"     => AlignSelf.Center,
            "stretch"    => AlignSelf.Stretch,
            "baseline"   => AlignSelf.Baseline,
            _            => AlignSelf.Auto,
        };

        private static Overflow ParseOverflow(string v) => v switch
        {
            "hidden" => Overflow.Hidden,
            "scroll" => Overflow.Scroll,
            "auto"   => Overflow.Auto,
            _        => Overflow.Visible,
        };

        private static FontWeight ParseFontWeight(string v) => v switch
        {
            "thin"        => FontWeight.Thin,
            "light"       => FontWeight.Light,
            "normal"      => FontWeight.Normal,
            "medium"      => FontWeight.Medium,
            "semibold"    => FontWeight.SemiBold,
            "bold"        => FontWeight.Bold,
            "extrabold"   => FontWeight.ExtraBold,
            "black"       => FontWeight.Black,
            _             => int.TryParse(v, out int n) ? (FontWeight)n : FontWeight.Normal,
        };

        private static TextAlign ParseTextAlign(string v) => v switch
        {
            "right"   => TextAlign.Right,
            "center"  => TextAlign.Center,
            "justify" => TextAlign.Justify,
            _         => TextAlign.Left,
        };

        private static WhiteSpace ParseWhiteSpace(string v) => v switch
        {
            "nowrap"   => WhiteSpace.NoWrap,
            "pre"      => WhiteSpace.Pre,
            "pre-wrap" => WhiteSpace.PreWrap,
            _          => WhiteSpace.Normal,
        };

        private static Cursor ParseCursor(string v) => v switch
        {
            "pointer"     => Cursor.Pointer,
            "text"        => Cursor.Text,
            "move"        => Cursor.Move,
            "not-allowed" => Cursor.NotAllowed,
            "crosshair"   => Cursor.Crosshair,
            "grab"        => Cursor.Grab,
            "grabbing"    => Cursor.Grabbing,
            "wait"        => Cursor.Wait,
            "help"        => Cursor.Help,
            "none"        => Cursor.None,
            _             => Cursor.Default,
        };

        private static ObjectFit ParseObjectFit(string v) => v.Trim().ToLowerInvariant() switch
        {
            "contain" => ObjectFit.Contain,
            "cover"   => ObjectFit.Cover,
            _         => ObjectFit.Fill,
        };

        private static JustifyItems ParseJustifyItems(string v) => v.Trim().ToLowerInvariant() switch
        {
            "start"   => JustifyItems.Start,
            "end"     => JustifyItems.End,
            "center"  => JustifyItems.Center,
            "stretch" => JustifyItems.Stretch,
            _         => JustifyItems.Stretch,
        };

        private static float ParseFloat(string v, float fallback) =>
            float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;

        private static int ParseInt(string v, int fallback) =>
            int.TryParse(v, out int i) ? i : fallback;

        // Thickness side replacement helpers (struct — no `with` expression available)
        private static Thickness WithTop(Thickness t, Length v)    => new(v, t.Right, t.Bottom, t.Left);
        private static Thickness WithRight(Thickness t, Length v)  => new(t.Top, v, t.Bottom, t.Left);
        private static Thickness WithBottom(Thickness t, Length v) => new(t.Top, t.Right, v, t.Left);
        private static Thickness WithLeft(Thickness t, Length v)   => new(t.Top, t.Right, t.Bottom, v);

        private enum Side { Top, Right, Bottom, Left }
    }
}
