using System.Text;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Map;

/// <summary>Detects and removes duplicate map path segments. Port of <c>MegaCrit.Sts2.Core.Map.MapPathPruning</c>.</summary>
public static class MapPathPruning
{
    public static void PruneAndRepair(
        MapPoint?[,] grid,
        HashSet<MapPoint> startMapPoints,
        ActMap map,
        MapPointTypeCounts pointTypeCounts,
        Rng rng,
        Func<MapPointType, MapPoint, bool> isValidPointType)
    {
        for (int i = 0; i < 3; i++)
        {
            PruneDuplicateSegments(grid, startMapPoints, map.StartingMapPoint, rng);
            if (!RepairPrunedPointTypes(map, pointTypeCounts, rng, isValidPointType))
            {
                break;
            }
        }
    }

    public static bool RepairPrunedPointTypes(
        ActMap map,
        MapPointTypeCounts pointTypeCounts,
        Rng rng,
        Func<MapPointType, MapPoint, bool> isValidPointType)
    {
        bool anyRepaired = false;
        anyRepaired |= RepairPointType(map, MapPointType.Shop, pointTypeCounts.NumOfShops, rng, isValidPointType);
        anyRepaired |= RepairPointType(map, MapPointType.Elite, pointTypeCounts.NumOfElites, rng, isValidPointType);
        anyRepaired |= RepairPointType(map, MapPointType.RestSite, pointTypeCounts.NumOfRests, rng, isValidPointType);
        anyRepaired |= RepairPointType(map, MapPointType.Unknown, pointTypeCounts.NumOfUnknowns, rng, isValidPointType);
        return anyRepaired;
    }

    private static bool RepairPointType(
        ActMap map,
        MapPointType type,
        int targetCount,
        Rng rng,
        Func<MapPointType, MapPoint, bool> isValidPointType)
    {
        // v0.113.1: only modifiable points count toward the existing total (#941).
        int current = map.GetAllMapPoints().Count(p => p.PointType == type && p.CanBeModified);
        int deficit = targetCount - current;
        if (deficit <= 0)
        {
            return false;
        }

        bool repaired = false;
        List<MapPoint> candidates = map.GetAllMapPoints()
            .Where(p => p.PointType == MapPointType.Monster && p.CanBeModified)
            .ToList();
        candidates.StableShuffle(rng);
        if (type != MapPointType.Unknown)
        {
            // v0.113.1: Unknown rooms are a second, separately shuffled candidate pool,
            // consumed after the Monster pool on the same rng (#941).
            List<MapPoint> unknownCandidates = map.GetAllMapPoints()
                .Where(p => p.PointType == MapPointType.Unknown && p.CanBeModified)
                .ToList();
            unknownCandidates.StableShuffle(rng);
            candidates.AddRange(unknownCandidates);
        }

        foreach (MapPoint candidate in candidates)
        {
            if (deficit == 0)
            {
                break;
            }

            if (isValidPointType(type, candidate))
            {
                candidate.PointType = type;
                deficit--;
                repaired = true;
            }
        }

        return repaired;
    }

    public static void PruneDuplicateSegments(
        MapPoint?[,] grid,
        HashSet<MapPoint> startMapPoints,
        MapPoint startingMapPoint,
        Rng rng)
    {
        int iterations = 0;
        List<List<MapPoint[]>> matchingSegments = FindMatchingSegments(startingMapPoint);
        while (PrunePaths(grid, startMapPoints, matchingSegments, rng))
        {
            iterations++;
            if (iterations > 50)
            {
                throw new InvalidOperationException($"Unable to prune matching segments in {iterations} iterations");
            }

            matchingSegments = FindMatchingSegments(startingMapPoint);
        }
    }

    public static List<List<MapPoint[]>> FindMatchingSegments(MapPoint startingMapPoint)
    {
        var fastGroups = new Dictionary<CompactSegmentSig, SegmentGroup>();
        Dictionary<ulong, List<SegmentGroup>>? fallbackBuckets = null;
        NumberText? fallbackNumbers = null;
        var path = new List<MapPoint>(24);

        // Preserve FindAllPaths' child order and stop at the first Boss. Only the
        // shared DFS stack is retained; completed paths need no separate lists.
        void Visit(MapPoint point)
        {
            path.Add(point);
            if (point.PointType == MapPointType.Boss)
            {
                AddSegments(path, fastGroups, ref fallbackBuckets, ref fallbackNumbers);
            }
            else
            {
                foreach (MapPoint child in point.Children)
                {
                    Visit(child);
                }
            }
            path.RemoveAt(path.Count - 1);
        }

        Visit(startingMapPoint);
        var duplicates = new List<SegmentGroup>();
        foreach (SegmentGroup group in fastGroups.Values)
        {
            if (group.Segments.Count > 1)
            {
                group.EnsureKey();
                duplicates.Add(group);
            }
        }
        if (fallbackBuckets is not null)
        {
            foreach (List<SegmentGroup> bucket in fallbackBuckets.Values)
            {
                foreach (SegmentGroup group in bucket)
                {
                    if (group.Segments.Count > 1)
                    {
                        group.EnsureKey();
                        duplicates.Add(group);
                    }
                }
            }
        }
        if (duplicates.Count == 0)
        {
            return new List<List<MapPoint[]>>();
        }
        // Numeric coordinate order differs from the original ordinal key order.
        duplicates.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
        var result = new List<List<MapPoint[]>>(duplicates.Count);
        for (int i = 0; i < duplicates.Count; i++)
        {
            result.Add(duplicates[i].Segments);
        }
        return result;
    }

    public static List<List<MapPoint>> FindAllPaths(MapPoint current)
    {
        var result = new List<List<MapPoint>>();
        if (current.PointType == MapPointType.Boss)
        {
            result.Add(new List<MapPoint> { current });
            return result;
        }

        foreach (MapPoint child in current.Children)
        {
            foreach (List<MapPoint> childPath in FindAllPaths(child))
            {
                var path = new List<MapPoint> { current };
                path.AddRange(childPath);
                result.Add(path);
            }
        }

        return result;
    }

    private readonly record struct CompactSegmentSig(ulong Header, ulong TypesLo, ulong TypesHi);

    private static void AddSegments(
        List<MapPoint> path,
        Dictionary<CompactSegmentSig, SegmentGroup> fastGroups,
        ref Dictionary<ulong, List<SegmentGroup>>? fallbackBuckets,
        ref NumberText? fallbackNumbers)
    {
        int pathCount = path.Count;
        for (int i = 0; i < pathCount - 1; i++)
        {
            MapPoint startPoint = path[i];
            if (!IsValidSegmentStartMapPoint(startPoint))
            {
                continue;
            }

            MapCoord firstCoord = startPoint.coord;
            int t0 = (int)startPoint.PointType;
            int t1 = (int)path[i + 1].PointType;
            bool canPack = firstCoord.col is >= 0 and < 256 && firstCoord.row is >= 0 and < 256
                && (uint)t0 < 16 && (uint)t1 < 16;
            ulong typesLo = canPack ? (((ulong)(uint)t0) | ((ulong)(uint)t1 << 4)) : 0;
            ulong typesHi = 0;

            for (int j = 2; j < pathCount - i; j++)
            {
                MapPoint end = path[i + j];
                int length = j + 1;
                int tj = (int)end.PointType;
                if (canPack && length <= 32 && (uint)tj < 16)
                {
                    int bitPos = j * 4;
                    if (bitPos < 64) typesLo |= (ulong)(uint)tj << bitPos;
                    else typesHi |= (ulong)(uint)tj << (bitPos - 64);
                }
                else
                {
                    canPack = false;
                }

                if (!IsValidSegmentEndMapPoint(end))
                {
                    continue;
                }

                MapCoord lastCoord = end.coord;
                if (canPack && lastCoord.col is >= 0 and < 256 && lastCoord.row is >= 0 and < 256)
                {
                    ulong header = (firstCoord.row == 0 ? (1UL << 40) : ((ulong)(uint)firstCoord.col << 24))
                        | ((ulong)(uint)firstCoord.row << 16)
                        | ((ulong)(uint)lastCoord.col << 8)
                        | (uint)lastCoord.row
                        | ((ulong)(uint)length << 32);
                    var sig = new CompactSegmentSig(header, typesLo, typesHi);
                    if (fastGroups.TryGetValue(sig, out SegmentGroup? existingGroup))
                    {
                        if (HasOverlap(existingGroup.Segments, path, i, length))
                        {
                            continue;
                        }
                        var extraSeg = new MapPoint[length];
                        path.CopyTo(i, extraSeg, 0, length);
                        existingGroup.Segments.Add(extraSeg);
                        continue;
                    }

                    var firstSeg = new MapPoint[length];
                    path.CopyTo(i, firstSeg, 0, length);
                    var created = new SegmentGroup(null);
                    created.Segments.Add(firstSeg);
                    fastGroups.Add(sig, created);
                    continue;
                }

                fallbackBuckets ??= new Dictionary<ulong, List<SegmentGroup>>();
                fallbackNumbers ??= new NumberText();
                var key = new SegmentKeyChars(path, i, length, fallbackNumbers);
                ulong hash = HashKey(key);
                if (!fallbackBuckets.TryGetValue(hash, out List<SegmentGroup>? bucket))
                {
                    bucket = new List<SegmentGroup>();
                    fallbackBuckets.Add(hash, bucket);
                }
                SegmentGroup? group = null;
                foreach (SegmentGroup candidate in bucket)
                {
                    candidate.EnsureKey();
                    if (KeyEquals(key, candidate.Key!))
                    {
                        group = candidate;
                        break;
                    }
                }
                if (group is not null && HasOverlap(group.Segments, path, i, length))
                {
                    continue;
                }
                var segment = new MapPoint[length];
                path.CopyTo(i, segment, 0, length);
                if (group is null)
                {
                    group = new SegmentGroup(GenerateSegmentKey(segment));
                    bucket.Add(group);
                }
                group.Segments.Add(segment);
            }
        }
    }

    private static bool IsValidSegmentStartMapPoint(MapPoint start) =>
        start.Children.Count > 1 || start.coord.row == 0;

    private static bool IsValidSegmentEndMapPoint(MapPoint end) => end.parents.Count >= 2;

    private static string GenerateSegmentKey(IReadOnlyList<MapPoint> segment)
    {
        var sb = new StringBuilder();
        MapPoint first = segment[0];
        MapPoint last = segment[^1];
        sb.Append(first.coord.row == 0
            ? $"{first.coord.row}-{last.coord.col},{last.coord.row}-"
            : $"{first.coord.col},{first.coord.row}-{last.coord.col},{last.coord.row}-");
        sb.Append(string.Join(",", segment.Select(p => (int)p.PointType)));
        return sb.ToString();
    }

    private static bool HasOverlap(List<MapPoint[]> existingSegments, List<MapPoint> path, int offset, int length)
    {
        foreach (MapPoint[] existing in existingSegments)
        {
            for (int i = 1; i <= existing.Length - 2; i++)
            {
                // Same-position reference equality, never set intersection.
                // The complete type key normally fixes length. Preserve the
                // IReadOnlyList indexing failure for exotic ambiguous number text.
                if (i >= length)
                {
                    throw new ArgumentOutOfRangeException("index");
                }
                if (ReferenceEquals(existing[i], path[offset + i]))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static ulong HashKey(SegmentKeyChars key)
    {
        ulong hash = 14695981039346656037UL;
        while (key.TryNext(out char value))
        {
            hash = unchecked((hash ^ value) * 1099511628211UL);
        }
        return hash;
    }

    private static bool KeyEquals(SegmentKeyChars key, string text)
    {
        int index = 0;
        while (key.TryNext(out char value))
        {
            if (index >= text.Length || text[index++] != value)
            {
                return false;
            }
        }
        return index == text.Length;
    }

    private sealed class SegmentGroup(string? key)
    {
        public string? Key { get; private set; } = key;
        public List<MapPoint[]> Segments { get; } = new(2);
        public void EnsureKey() => Key ??= GenerateSegmentKey(Segments[0]);
    }

    private sealed class NumberText
    {
        private readonly Dictionary<int, string> _values = new();
        public string Get(int value)
        {
            if (!_values.TryGetValue(value, out string? text))
            {
                text = value.ToString();
                _values.Add(value, text);
            }
            return text;
        }
    }

    // A non-allocating view of GenerateSegmentKey's character stream. Only an
    // accepted group's first key is materialized; number text is cached per call.
    private struct SegmentKeyChars
    {
        private readonly List<MapPoint> _path;
        private readonly int _offset;
        private readonly NumberText _numbers;
        private readonly MapCoord _first;
        private readonly MapCoord _last;
        private readonly int _headerTokens;
        private readonly int _tokenCount;
        private int _tokenIndex;
        private int _character;
        private string _text;

        public SegmentKeyChars(List<MapPoint> path, int offset, int length, NumberText numbers)
        {
            _path = path;
            _offset = offset;
            _numbers = numbers;
            _first = path[offset].coord;
            _last = path[offset + length - 1].coord;
            _headerTokens = _first.row == 0 ? 6 : 8;
            _tokenCount = _headerTokens + length * 2 - 1;
            _tokenIndex = -1;
            _character = 0;
            _text = string.Empty;
        }

        public bool TryNext(out char value)
        {
            while (_character >= _text.Length)
            {
                if (++_tokenIndex >= _tokenCount)
                {
                    value = default;
                    return false;
                }
                _text = Token(_tokenIndex);
                _character = 0;
            }
            value = _text[_character++];
            return true;
        }

        private string Token(int index)
        {
            if (index >= _headerTokens)
            {
                int typeIndex = index - _headerTokens;
                return typeIndex % 2 == 1 ? "," : _numbers.Get((int)_path[_offset + typeIndex / 2].PointType);
            }
            if (_first.row == 0)
            {
                return index switch
                {
                    0 => _numbers.Get(_first.row), 1 => "-", 2 => _numbers.Get(_last.col),
                    3 => ",", 4 => _numbers.Get(_last.row), _ => "-",
                };
            }
            return index switch
            {
                0 => _numbers.Get(_first.col), 1 => ",", 2 => _numbers.Get(_first.row), 3 => "-",
                4 => _numbers.Get(_last.col), 5 => ",", 6 => _numbers.Get(_last.row), _ => "-",
            };
        }
    }

    private static bool PrunePaths(
        MapPoint?[,] grid,
        HashSet<MapPoint> startMapPoints,
        IEnumerable<List<MapPoint[]>> matchingSegments,
        Rng rng)
    {
        foreach (List<MapPoint[]> matching in matchingSegments)
        {
            matching.UnstableShuffle(rng);
            if (PruneAllButLast(grid, startMapPoints, matching) != 0)
            {
                return true;
            }

            if (BreakAParentChildRelationshipInAnySegment(matching))
            {
                return true;
            }
        }

        return false;
    }

    private static int PruneAllButLast(MapPoint?[,] grid, HashSet<MapPoint> startMapPoints, IReadOnlyList<MapPoint[]> matches)
    {
        int pruned = 0;
        for (int i = 0; i < matches.Count; i++)
        {
            if (pruned == matches.Count - 1)
            {
                return pruned;
            }

            if (PruneSegment(grid, startMapPoints, matches[i]))
            {
                pruned++;
            }
        }

        return pruned;
    }

    private static bool PruneSegment(MapPoint?[,] grid, HashSet<MapPoint> startMapPoints, MapPoint[] segment)
    {
        bool result = false;
        for (int i = 0; i < segment.Length - 1; i++)
        {
            MapPoint point = segment[i];
            if (!IsInMap(grid, point))
            {
                return true;
            }

            if (point.Children.Count > 1 || point.parents.Count > 1
                || point.parents.Any(n => n.Children.Count == 1 && !IsRemoved(grid, n)))
            {
                continue;
            }

            bool tailHasBranchingSingleParent = false;
            for (int k = i; k < segment.Length; k++)
            {
                MapPoint n = segment[k];
                if (n.Children.Count > 1 && n.parents.Count == 1)
                {
                    tailHasBranchingSingleParent = true;
                    break;
                }
            }
            if (tailHasBranchingSingleParent)
            {
                continue;
            }

            if (segment[^1].parents.Count == 1)
            {
                return false;
            }

            bool hasStrandedOutsideChild = false;
            foreach (MapPoint c in point.Children)
            {
                if (c.parents.Count == 1 && Array.IndexOf(segment, c) < 0)
                {
                    hasStrandedOutsideChild = true;
                    break;
                }
            }
            if (!hasStrandedOutsideChild)
            {
                RemovePoint(grid, startMapPoints, point);
                result = true;
            }
        }

        return result;
    }

    private static void RemovePoint(MapPoint?[,] grid, HashSet<MapPoint> startMapPoints, MapPoint point)
    {
        grid[point.coord.col, point.coord.row] = null;
        startMapPoints.Remove(point);
        foreach (MapPoint child in point.Children.ToList())
        {
            point.RemoveChildPoint(child);
        }

        foreach (MapPoint parent in point.parents.ToList())
        {
            parent.RemoveChildPoint(point);
        }
    }

    private static bool IsInMap(MapPoint?[,] grid, MapPoint point)
    {
        if (grid[point.coord.col, point.coord.row] == null && point.PointType != MapPointType.Ancient)
        {
            return point.PointType == MapPointType.Boss;
        }

        return true;
    }

    private static bool IsRemoved(MapPoint?[,] grid, MapPoint point) => grid[point.coord.col, point.coord.row] == null;

    private static bool BreakAParentChildRelationshipInAnySegment(List<MapPoint[]> matches) =>
        matches.Any(BreakAParentChildRelationshipInSegment);

    private static bool BreakAParentChildRelationshipInSegment(MapPoint[] segment)
    {
        bool result = false;
        for (int i = 0; i < segment.Length - 1; i++)
        {
            MapPoint point = segment[i];
            if (point.Children.Count < 2)
            {
                continue;
            }

            MapPoint nextInSegment = segment[i + 1];
            if (nextInSegment.parents.Count != 1)
            {
                point.RemoveChildPoint(nextInSegment);
                result = true;
            }
        }

        return result;
    }
}
