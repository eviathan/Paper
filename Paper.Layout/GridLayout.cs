using Paper.Core.Reconciler;
using Paper.Core.Styles;

namespace Paper.Layout
{
    /// <summary>
    /// Implements CSS Grid layout for a container fiber.
    /// Supports: fr, px, %, auto, repeat(N, x), explicit item placement
    /// (grid-column-start/end, grid-row-start/end), and auto-placement.
    /// </summary>
    internal static class GridLayout
    {
        public static void Layout(
            Fiber container,
            StyleSheet style,
            float contentX,
            float contentY,
            float contentWidth,
            float contentHeight,
            Func<string?, (float w, float h)?>? getImageSize = null)
        {
            // Parse column and row templates
            var colTracks = GridTemplateParser.Parse(style.GridTemplateColumns);
            var rowTracks = GridTemplateParser.Parse(style.GridTemplateRows);

            var colGapLength = style.ColumnGap ?? style.Gap;
            var rowGapLength = style.RowGap ?? style.Gap;
            float colGap = BoxModel.ResolveLength(colGapLength, contentWidth, 0f);
            float rowGap = BoxModel.ResolveLength(rowGapLength, contentHeight, 0f);

            // Collect grid items (non-absolute children)
            var items = CollectGridItems(container);
            if (items.Count == 0) return;

            // Pre-placement pass so auto tracks can be sized from content
            int autoRowCount = ComputeImplicitRowCount(items, colTracks.Count == 0 ? 1 : colTracks.Count);
            var allRowTracks = EnsureRowTracks(rowTracks, autoRowCount);
            var placed = PlaceItems(items, colTracks.Count == 0 ? 1 : colTracks.Count, allRowTracks.Count);

            // Resolve column and row track sizes (auto tracks sized from placed items' preferred sizes)
            // Pass container dimensions so % widths on auto-tracked items can be approximated against the container.
            var resolvedCols = ResolveTracks(colTracks, contentWidth, colGap, colTracks.Count, placed, isColumn: true, containerRef: contentWidth);
            var resolvedRows = ResolveTracks(allRowTracks, contentHeight, rowGap, allRowTracks.Count, placed, isColumn: false, containerRef: contentHeight);

            // Set layout for each item
            foreach (var (item, col, row, colSpan, rowSpan) in placed)
            {
                if (col >= resolvedCols.Count || row >= resolvedRows.Count) continue;

                float x = contentX + resolvedCols[col].Start;
                float y = contentY + resolvedRows[row].Start;

                // Sum widths for spanning items
                float w = 0;
                for (int c = col; c < Math.Min(col + colSpan, resolvedCols.Count); c++)
                    w += resolvedCols[c].Size + (c < col + colSpan - 1 ? colGap : 0);

                float h = 0;
                for (int r = row; r < Math.Min(row + rowSpan, resolvedRows.Count); r++)
                    h += resolvedRows[r].Size + (r < row + rowSpan - 1 ? rowGap : 0);

                var itemStyle = item.ComputedStyle;
                var (mt, mr, mb, ml) = BoxModel.MarginPx(itemStyle, w, h);
                x += ml; y += mt; w -= ml + mr; h -= mt + mb;
                w = Math.Max(0, w); h = Math.Max(0, h);

                item.Layout = new LayoutBox { X = x, Y = y, Width = w, Height = h };

                // Layout item's children
                var (cx, cy) = BoxModel.ContentOrigin(itemStyle, w, h);
                var (cw, ch) = BoxModel.ContentSize(w, h, itemStyle, w, h);
                LayoutEngine.LayoutChildren(item, itemStyle, x + cx, y + cy, cw, ch, null, getImageSize);
            }
        }

        // ── Track resolution ──────────────────────────────────────────────────

        private static List<ResolvedTrack> ResolveTracks(
            List<GridTrack> tracks, float containerSize, float gap, int count,
            List<(Fiber item, int col, int row, int colSpan, int rowSpan)> placed,
            bool isColumn, float containerRef = 0f)
        {
            float totalGap = Math.Max(0, count - 1) * gap;
            float available = containerSize - totalGap;

            var sizes = new float[count];
            float usedFixed = 0;
            float totalFr = 0;

            for (int i = 0; i < count && i < tracks.Count; i++)
            {
                float size = tracks[i].Resolve(available);
                if (!float.IsNaN(size))
                {
                    sizes[i] = size;
                    usedFixed += size;
                }
                else if (tracks[i].IsFractional)
                {
                    totalFr += tracks[i].Value;
                }
                else if (tracks[i].IsAuto)
                {
                    // Size auto tracks to the max preferred size of items placed in them.
                    // % widths are resolved against containerRef (the full container axis size)
                    // as an approximation — avoids circular dependency with the track size.
                    float autoSize = MeasureAutoTrack(i, isColumn, placed, containerRef > 0f ? containerRef : available);
                    sizes[i] = autoSize;
                    usedFixed += autoSize;
                }
            }

            // Distribute remaining space to fr tracks
            float remaining = Math.Max(0, available - usedFixed);
            float frUnit = totalFr > 0 ? remaining / totalFr : 0;

            for (int i = 0; i < count && i < tracks.Count; i++)
            {
                if (tracks[i].IsFractional)
                    sizes[i] = tracks[i].Value * frUnit;
            }

            // Build ResolvedTrack list
            var result = new List<ResolvedTrack>(count);
            float cursor = 0;
            for (int i = 0; i < count; i++)
            {
                float sz = i < sizes.Length ? sizes[i] : (totalFr > 0 ? 0 : available / count);
                result.Add(new ResolvedTrack { Start = cursor, Size = sz });
                cursor += sz + gap;
            }

            return result;
        }

        private static float MeasureAutoTrack(
            int trackIdx, bool isColumn,
            List<(Fiber item, int col, int row, int colSpan, int rowSpan)> placed,
            float containerRef = 0f)
        {
            float max = 0;
            foreach (var (item, col, row, colSpan, rowSpan) in placed)
            {
                int idx  = isColumn ? col     : row;
                int span = isColumn ? colSpan : rowSpan;
                if (idx != trackIdx || span != 1) continue; // skip spanning items
                var s = item.ComputedStyle;
                float preferred = isColumn
                    ? GetPreferredSize(s.Width, s.MinWidth, containerRef)
                    : GetPreferredSize(s.Height, s.MinHeight, containerRef);
                max = Math.Max(max, preferred);
            }
            return max;
        }

        private static float GetPreferredSize(Length? size, Length? minSize, float containerRef = 0f)
        {
            if (size != null && !size.Value.IsAuto)
            {
                // Resolve % against containerRef; for fixed units resolve directly
                float sz = size.Value.Kind == Length.Unit.Percent
                    ? (containerRef > 0f ? size.Value.Resolve(containerRef) : 0f)
                    : size.Value.Resolve();
                if (sz > 0f) return sz;
            }
            if (minSize != null && !minSize.Value.IsAuto)
            {
                float msz = minSize.Value.Kind == Length.Unit.Percent
                    ? (containerRef > 0f ? minSize.Value.Resolve(containerRef) : 0f)
                    : minSize.Value.Resolve();
                if (msz > 0f) return msz;
            }
            return 0;
        }

        // ── Item placement ────────────────────────────────────────────────────

        private static List<(Fiber item, int col, int row, int colSpan, int rowSpan)> PlaceItems(
            List<Fiber> items, int colCount, int rowCount)
        {
            var result = new List<(Fiber, int, int, int, int)>();
            // Grid occupancy: true = occupied
            var occupied = new bool[rowCount, colCount];

            int autoCol = 0;
            int autoRow = 0;

            foreach (var item in items)
            {
                var s = item.ComputedStyle;
                int colStart = s.GridColumnStart - 1; // CSS is 1-based, -1 = auto
                int colEnd = s.GridColumnEnd - 1;
                int rowStart = s.GridRowStart - 1;
                int rowEnd = s.GridRowEnd - 1;

                int colSpan = colEnd > colStart ? colEnd - colStart : 1;
                int rowSpan = rowEnd > rowStart ? rowEnd - rowStart : 1;

                bool hasExplicitCol = s.GridColumnStart > 0;
                bool hasExplicitRow = s.GridRowStart > 0;

                int placedCol, placedRow;

                if (hasExplicitCol && hasExplicitRow)
                {
                    placedCol = Math.Max(0, colStart);
                    placedRow = Math.Max(0, rowStart);
                }
                else
                {
                    // Auto placement — find next available cell
                    (placedCol, placedRow) = FindNextCell(occupied, rowCount, colCount,
                        autoCol, autoRow, colSpan, rowSpan);
                    autoCol = placedCol + colSpan;
                    autoRow = placedRow;
                    if (autoCol >= colCount)
                    {
                        autoCol = 0;
                        autoRow = placedRow + 1;
                    }
                }

                // Mark occupied
                for (int r = placedRow; r < Math.Min(placedRow + rowSpan, rowCount); r++)
                    for (int c = placedCol; c < Math.Min(placedCol + colSpan, colCount); c++)
                        if (r < occupied.GetLength(0) && c < occupied.GetLength(1))
                            occupied[r, c] = true;

                result.Add((item, placedCol, placedRow, colSpan, rowSpan));
            }

            return result;
        }

        private static (int col, int row) FindNextCell(
            bool[,] occupied, int rowCount, int colCount,
            int startCol, int startRow, int colSpan, int rowSpan)
        {
            for (int r = startRow; r < rowCount; r++)
            {
                for (int c = (r == startRow ? startCol : 0); c <= colCount - colSpan; c++)
                {
                    if (CellsFree(occupied, c, r, colSpan, rowSpan, rowCount, colCount))
                        return (c, r);
                }
            }
            // Overflow — place beyond defined grid
            return (0, rowCount);
        }

        private static bool CellsFree(bool[,] occupied, int col, int row,
            int colSpan, int rowSpan, int rowCount, int colCount)
        {
            for (int r = row; r < row + rowSpan; r++)
                for (int c = col; c < col + colSpan; c++)
                    if (r >= rowCount || c >= colCount || occupied[r, c]) return false;
            return true;
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private static List<Fiber> CollectGridItems(Fiber container)
        {
            var items = new List<Fiber>();
            var child = container.Child;
            while (child != null)
            {
                var s = child.ComputedStyle;
                var display = s.Display ?? Display.Block;
                var position = s.Position ?? Position.Static;
                if (display != Display.None && position != Position.Absolute && position != Position.Fixed)
                    items.Add(child);
                child = child.Sibling;
            }
            return items;
        }

        private static int ComputeImplicitRowCount(List<Fiber> items, int colCount)
        {
            int maxRow = 0;
            int autoCol = 0, autoRow = 0;
            foreach (var item in items)
            {
                var s = item.ComputedStyle;
                int r = s.GridRowStart > 0 ? s.GridRowStart - 1 : autoRow;
                maxRow = Math.Max(maxRow, r + 1);
                if (s.GridColumnStart <= 0)
                {
                    autoCol++;
                    if (autoCol >= colCount) { autoCol = 0; autoRow++; }
                }
            }
            return Math.Max(1, maxRow);
        }

        private static List<GridTrack> EnsureRowTracks(List<GridTrack> defined, int needed)
        {
            if (defined.Count >= needed) return defined;
            var result = new List<GridTrack>(defined);
            while (result.Count < needed)
                result.Add(new GridTrack(GridTrackKind.Auto, 0));
            return result;
        }
    }
}
