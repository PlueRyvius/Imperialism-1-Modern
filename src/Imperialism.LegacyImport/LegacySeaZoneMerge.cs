namespace Imperialism.LegacyImport;

/// <summary>What one step of the original's sea-zone merge did.</summary>
/// <param name="Moved">
/// False: zone <paramref name="Zone"/>, of <paramref name="Cells"/> cells, was merged
/// into <paramref name="Target"/> with <paramref name="Score"/> (null when it went by
/// the small-zone fallback rather than by a segment). True: the last zone,
/// <paramref name="Zone"/>, took the emptied id <paramref name="Target"/>.
/// </param>
internal readonly record struct LegacySeaZoneMergeEvent(
    bool Moved,
    int Zone,
    int Target,
    int Cells,
    long? Score);

internal sealed record LegacySeaZoneMergeResult(
    int[] Zones,
    int ZoneCount,
    IReadOnlyList<LegacySeaZoneMergeEvent> Events);

/// <summary>
/// The second pass the original runs over its sea zones on load, after the dense
/// first-appearance scan at <c>0x00529F60</c>: every zone under 32 cells is merged into
/// a neighbour and the ids are compacted again. <b>The rule is the original's, read from
/// the disassembly</b> — <c>0x0052C1A0</c> builds corner records, <c>0x0052CAE0</c>
/// pairs them into boundary segments, <c>0x0052D750</c> merges — and is followed
/// literally, quirks included. It belongs to the 1997 grid and lives here, in the
/// importer: the half-column units are <c>2 × width</c> of the legacy map, never a
/// runtime constant.
/// </summary>
/// <remarks>
/// <para>Three points the trace leaves open are <b>chosen</b>:</para>
/// <list type="bullet">
/// <item><b>U1</b>: neighbours wrap east-west. Where the executable sets its wrap flag
/// (<c>[0x006A43D4]+20</c>) is untraced; with wrap off the record and segment counts
/// change but no event or score on any of the ten shipped maps.</item>
/// <item><b>U4</b>: relabelling can leave a segment naming the same zone at both ends.
/// Read literally, a later small zone would then "merge into itself": its count drops to
/// zero while its cells keep its id, and the last zone is moved on top of them — or, when
/// it is the last id, the id is dropped with cells still naming it. <b>Here a segment whose
/// far end is the zone itself is skipped</b>, like one whose far end is gone. No shipped
/// map reaches the case, so no oracle event changes.</item>
/// <item><b>U5</b>: the compaction check runs for every id, so a zone already empty at
/// the start would be compacted. Scan ids are dense, so that cannot arise from a map.</item>
/// </list>
/// </remarks>
internal static class LegacySeaZoneMerge
{
    /// <summary>A zone with fewer cells than this is merged (<c>0x0052D849</c>).</summary>
    internal const int SmallZoneCells = 32;

    /// <summary>A zone no segment touches is merged by the fallback only at or under this.</summary>
    internal const int FallbackZoneCells = 6;

    private const long FillsScore = 10_000;
    private const long UnabsorbedScore = 5_000;

    private readonly record struct CornerRecord(int Point, int ZoneA, int ZoneB, int Direction);

    private sealed class Segment
    {
        public int X1;
        public int Y1;
        public int X2;
        public int Y2;
        public int ZoneA;
        public int ZoneB;
    }

    /// <param name="width">Map width in cells.</param>
    /// <param name="height">Map height in rows.</param>
    /// <param name="cellZones">The dense scan's zone per cell, −1 for land.</param>
    /// <param name="zoneCount">The number of dense ids the scan gave.</param>
    internal static LegacySeaZoneMergeResult Merge(int width, int height, IReadOnlyList<int> cellZones, int zoneCount)
    {
        var zones = cellZones.ToArray();
        var segments = BuildSegments(width, height, zones);
        var events = new List<LegacySeaZoneMergeEvent>();

        // 0x0052D75D-D7FB: counts are taken once and never recounted from the map.
        var count = new int[zoneCount];
        var absorbed = new bool[zoneCount];
        foreach (var zone in zones)
        {
            if (zone >= 0)
            {
                count[zone]++;
            }
        }

        var remaining = zoneCount;
        for (var z = zoneCount - 1; z >= 0; z--)
        {
            if (count[z] > 0 && count[z] < SmallZoneCells)
            {
                // 0x0052D876-D99E: strict >, so the first segment with the best score wins.
                // The box is the raw one: the segment's wrap byte is not applied.
                var target = -1;
                var best = -1L;
                Segment? chosen = null;
                foreach (var segment in segments)
                {
                    int other;
                    if (segment.ZoneA == z)
                    {
                        other = segment.ZoneB;
                    }
                    else if (segment.ZoneB == z)
                    {
                        other = segment.ZoneA;
                    }
                    else
                    {
                        continue;
                    }

                    // U4, chosen: a segment left with z at both ends is not a neighbour.
                    if (other < 0 || other == z)
                    {
                        continue;
                    }

                    var score =
                        (count[other] + count[z] >= SmallZoneCells ? FillsScore : 0) +
                        (absorbed[other] ? 0 : UnabsorbedScore) +
                        (Math.Abs((long)(segment.X2 - segment.X1) * (segment.Y2 - segment.Y1)) * count[other]);
                    if (score > best)
                    {
                        best = score;
                        target = other;
                        chosen = segment;
                    }
                }

                // 0x0052D9C3-DB23: with no segment, a zone of six cells or fewer takes the
                // first ocean neighbour in another zone, its cells in index order, NE clockwise.
                if (target < 0 && count[z] <= FallbackZoneCells)
                {
                    target = FirstOtherZoneNeighbour(width, height, zones, z);
                }

                if (target >= 0)
                {
                    // 0x0052DB32-DDB6.
                    events.Add(new LegacySeaZoneMergeEvent(false, z, target, count[z], chosen is null ? null : best));
                    count[target] += count[z];
                    count[z] = 0;
                    absorbed[target] = true;
                    Relabel(zones, segments, z, target);
                    if (chosen is not null)
                    {
                        chosen.X1 = chosen.Y1 = chosen.X2 = chosen.Y2 = 0;
                        chosen.ZoneA = chosen.ZoneB = -1;
                    }
                }
            }

            // 0x0052DDFA-E03B: the last zone takes the emptied id. It came from an id
            // already visited, so the loop simply carries on downward.
            if (count[z] == 0)
            {
                remaining--;
                count[z] = count[remaining];
                absorbed[z] = absorbed[remaining];
                if (remaining != z)
                {
                    events.Add(new LegacySeaZoneMergeEvent(true, remaining, z, count[z], null));
                    Relabel(zones, segments, remaining, z);
                }
            }
        }

        return new LegacySeaZoneMergeResult(zones, remaining, events);
    }

    private static void Relabel(int[] zones, List<Segment> segments, int from, int to)
    {
        for (var index = 0; index < zones.Length; index++)
        {
            if (zones[index] == from)
            {
                zones[index] = to;
            }
        }

        foreach (var segment in segments)
        {
            if (segment.ZoneA == from)
            {
                segment.ZoneA = to;
            }

            if (segment.ZoneB == from)
            {
                segment.ZoneB = to;
            }
        }
    }

    private static int FirstOtherZoneNeighbour(int width, int height, int[] zones, int zone)
    {
        for (var index = 0; index < zones.Length; index++)
        {
            if (zones[index] != zone)
            {
                continue;
            }

            for (var direction = 0; direction < 6; direction++)
            {
                var neighbour = Neighbour(width, height, zones, index, direction);
                if (neighbour >= 0 && neighbour != zone)
                {
                    return neighbour;
                }
            }
        }

        return -1;
    }

    /// <summary>The zone of a neighbour, −1 for land or off the top or bottom (0 = NE, clockwise).</summary>
    private static int Neighbour(int width, int height, int[] zones, int index, int direction) =>
        LegacyWorldConverter.GetNeighbour(index, width, height, (byte)(1 << direction)) is { } cell ? zones[cell] : -1;

    private static List<Segment> BuildSegments(int width, int height, int[] zones)
    {
        var halfColumns = checked(2 * width);
        var cells = zones.Length;
        var records = new List<CornerRecord>();

        int NorthWest(int index)
        {
            var (column, row) = (index % width, index / width);
            return (2 * column) + (row & 1) + (halfColumns * row);
        }

        int SouthEast(int index)
        {
            var (column, row) = (index % width, index / width);
            return (((2 * column) + (row & 1) + 2) % halfColumns) + (halfColumns * (row + 1));
        }

        void Add(int point, int a, int b, int direction) =>
            records.Add(new CornerRecord(point, Math.Min(a, b), Math.Max(a, b), direction));

        int Zone(int index) => zones[index];

        // 1. Corner records, 0x0052C1A0. Row 0 (C1C9-C2C4): its west edges.
        for (var index = 0; index < Math.Min(width, cells); index++)
        {
            var (a, b) = (Zone(index), Neighbour(width, height, zones, index, 4));
            if (a >= 0 && b >= 0 && a != b)
            {
                Add(NorthWest(index), a, b, 2);
            }
        }

        // Rows 1 onward (C2CA-C5C9): each cell's north-west corner, against W and NW.
        for (var index = width; index < cells; index++)
        {
            var (zc, ed, es) = (Zone(index), Neighbour(width, height, zones, index, 4), Neighbour(width, height, zones, index, 5));
            var (bx, ax, bp) = (4, 2, 0);
            if (zc < 0)
            {
                (zc, es, bx, ax) = (es, -1, 2, 4);
            }

            if (ed < 0)
            {
                (ed, es, bp, ax) = (es, -1, ax, 0);
            }

            if (zc == ed || zc == es || ed == es)
            {
                continue;
            }

            Add(NorthWest(index), zc, ed, ax);
            if (es >= 0)
            {
                Add(NorthWest(index), zc, es, bp);
                Add(NorthWest(index), ed, es, bx);
            }
        }

        // All rows but the last (C5CF-C755): each cell's south-east corner, against E and SE.
        var lastRow = width * (height - 1);
        for (var index = 0; index < lastRow; index++)
        {
            var (zc, ed, es) = (Zone(index), Neighbour(width, height, zones, index, 1), Neighbour(width, height, zones, index, 2));
            var (v10, v14, ax) = (1, 3, 5);
            if (zc < 0)
            {
                (zc, es, v10, ax) = (es, -1, 5, 1);
            }

            if (ed < 0)
            {
                (ed, es, v14, ax) = (es, -1, ax, 3);
            }

            if (zc == ed || zc == es || ed == es)
            {
                continue;
            }

            Add(SouthEast(index), zc, ed, ax);
            if (es >= 0)
            {
                Add(SouthEast(index), zc, es, v14);
                Add(SouthEast(index), ed, es, v10);
            }
        }

        // The last row (C75B-C7A8): its east edges.
        for (var index = Math.Max(lastRow, 0); index < cells; index++)
        {
            var (a, b) = (Zone(index), Neighbour(width, height, zones, index, 1));
            if (a >= 0 && b >= 0 && a != b)
            {
                Add(SouthEast(index), a, b, 5);
            }
        }

        // 2. Segments, 0x0052CAE0: each record pairs with its NEAREST partner of the same
        // zone pair, preferring one whose direction differs by 2-4 (strict <, first wins).
        long Area(int a, int b)
        {
            long dy = Math.Abs((a / halfColumns) - (b / halfColumns));
            long dx = (((a % halfColumns) - (b % halfColumns)) + halfColumns) % halfColumns;
            if (dx > width)
            {
                // 0x0052CD3D-CDA0: 215, not 216, on the original's grid. A quirk, kept.
                dx = halfColumns - 1 - dx;
            }

            return dx * dy;
        }

        var used = new bool[records.Count];
        var segments = new List<Segment>();
        for (var i = 0; i < records.Count; i++)
        {
            if (used[i])
            {
                continue;
            }

            var (best, alternative) = (-1, -1);
            for (var j = i + 1; j < records.Count; j++)
            {
                if (used[j] || records[j].ZoneA != records[i].ZoneA || records[j].ZoneB != records[i].ZoneB)
                {
                    continue;
                }

                var turn = (records[j].Direction - records[i].Direction + 6) % 6;
                if (turn is >= 2 and <= 4)
                {
                    if (best < 0 || Area(records[i].Point, records[j].Point) < Area(records[i].Point, records[best].Point))
                    {
                        best = j;
                    }
                }
                else if (best < 0 &&
                         (alternative < 0 || Area(records[i].Point, records[j].Point) < Area(records[i].Point, records[alternative].Point)))
                {
                    alternative = j;
                }
            }

            if (best < 0)
            {
                best = alternative;
            }

            if (best >= 0)
            {
                // 0x0052B220. Which end is stored first does not change |dx·dy|.
                segments.Add(new Segment
                {
                    X1 = records[i].Point % halfColumns,
                    Y1 = records[i].Point / halfColumns,
                    X2 = records[best].Point % halfColumns,
                    Y2 = records[best].Point / halfColumns,
                    ZoneA = records[i].ZoneA,
                    ZoneB = records[i].ZoneB,
                });
                used[best] = true;
            }

            used[i] = true;
        }

        return segments;
    }
}
