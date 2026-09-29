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
        List<List<MapPoint>> allPaths = FindAllPaths(startingMapPoint);
        var segments = new SortedDictionary<string, List<MapPoint[]>>(StringComparer.Ordinal);
        foreach (List<MapPoint> path in allPaths)
        {
            AddSegmentsToDictionary(path, segments);
        }

        return GetDuplicateSegments(segments);
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

    private static void AddSegmentsToDictionary(IReadOnlyList<MapPoint> path, IDictionary<string, List<MapPoint[]>> segments)
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

                MapPoint[] segment = path.Skip(i).Take(j + 1).ToArray();
                string key = GenerateSegmentKey(segment);
                if (!segments.ContainsKey(key))
                {
                    segments[key] = new List<MapPoint[]> { segment };
                }
                else if (!AnyOverlappingSegments(segments[key], segment))
                {
                    segments[key].Add(segment);
                }
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

    private static bool AnyOverlappingSegments(IEnumerable<MapPoint[]> existingSegments, IReadOnlyList<MapPoint> segment) =>
        existingSegments.Any(existing => OverlappingSegment(existing, segment));

    private static bool OverlappingSegment(IReadOnlyList<MapPoint> a, IReadOnlyList<MapPoint> b)
    {
        if (a.Count < 3 || b.Count < 3)
        {
            return false;
        }

        for (int i = 1; i <= a.Count - 2; i++)
        {
            if (ReferenceEquals(a[i], b[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static List<List<MapPoint[]>> GetDuplicateSegments(IDictionary<string, List<MapPoint[]>> segments) =>
        segments.Values.Where(list => list.Count > 1).ToList();

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
