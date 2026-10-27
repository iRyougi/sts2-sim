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
        int current = map.GetAllMapPoints().Count(p => p.PointType == type);
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
        var buckets = new Dictionary<ulong, List<SegmentGroup>>();
        var numbers = new NumberText();
        var path = new List<MapPoint>();

        // Preserve FindAllPaths' child order and stop at the first Boss. Only the
        // shared DFS stack is retained; completed paths need no separate lists.
        void Visit(MapPoint point)
        {
            path.Add(point);
            if (point.PointType == MapPointType.Boss)
            {
                AddSegments(path, buckets, numbers);
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
        foreach (List<SegmentGroup> bucket in buckets.Values)
        {
            foreach (SegmentGroup group in bucket)
            {
                if (group.Segments.Count > 1)
                {
                    duplicates.Add(group);
                }
            }
        }
        // Numeric coordinate order differs from the original ordinal key order.
        duplicates.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
        return duplicates.Select(group => group.Segments).ToList();
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

    private static void AddSegments(
        List<MapPoint> path,
        Dictionary<ulong, List<SegmentGroup>> buckets,
        NumberText numbers)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            if (!IsValidSegmentStartMapPoint(path[i]))
            {
                continue;
            }

            for (int j = 2; j < path.Count - i; j++)
            {
                MapPoint end = path[i + j];
                if (!IsValidSegmentEndMapPoint(end))
                {
                    continue;
                }

                int length = j + 1;
                var key = new SegmentKeyChars(path, i, length, numbers);
                ulong hash = HashKey(key);
                if (!buckets.TryGetValue(hash, out List<SegmentGroup>? bucket))
                {
                    bucket = new List<SegmentGroup>();
                    buckets.Add(hash, bucket);
                }
                SegmentGroup? group = null;
                foreach (SegmentGroup candidate in bucket)
                {
                    // Hashes never establish equality: compare every original
                    // key character, including culture-specific integer text.
                    if (KeyEquals(key, candidate.Key))
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

    private sealed class SegmentGroup(string key)
    {
        public string Key { get; } = key;
        public List<MapPoint[]> Segments { get; } = new();
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

            MapPoint[] tail = segment.Skip(i).ToArray();
            if (tail.Any(n => n.Children.Count > 1 && n.parents.Count == 1))
            {
                continue;
            }

            if (segment[^1].parents.Count == 1)
            {
                return false;
            }

            if (!point.Children.Where(c => !segment.Contains(c)).Any(c => c.parents.Count == 1))
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
