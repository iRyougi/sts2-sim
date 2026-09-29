using Sts2Sim.Core.Map;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Tests.Map;

public class MapPathPruningTests
{
    [Fact]
    public void ArchivedMVBCYMS8BUST_FirstActHasTheRecordedCompletePath()
    {
        // Exact map_point_type sequence from the archive's first act; no generated
        // coordinates or alternative no-pruning map supplies the expected path.
        MapPointType[] archivedPath =
        [
            MapPointType.Ancient, MapPointType.Monster, MapPointType.Monster,
            MapPointType.Shop, MapPointType.Monster, MapPointType.Monster,
            MapPointType.Unknown, MapPointType.Monster, MapPointType.RestSite,
            MapPointType.Treasure, MapPointType.Shop, MapPointType.Unknown,
            MapPointType.RestSite, MapPointType.Monster, MapPointType.Unknown,
            MapPointType.RestSite, MapPointType.Boss,
        ];
        var run = new Sts2Sim.Core.Runs.RunState("MVBCYMS8BUST", ascensionLevel: 10);
        var frontier = new HashSet<MapPoint> { run.Map.StartingMapPoint };
        for (int index = 0; index < archivedPath.Length; index++)
        {
            MapPoint[] matching = frontier.Where(point => point.PointType == archivedPath[index]).ToArray();
            Assert.True(matching.Length > 0,
                $"Archived floor {index + 1} requires {archivedPath[index]}, but reachable types are {string.Join(", ", frontier.Select(point => point.PointType))}.");
            if (index == archivedPath.Length - 1)
                Assert.Contains(matching, point => point.Children.Count == 0);
            else
                frontier = matching.SelectMany(point => point.Children).ToHashSet();
        }
    }
    [Fact]
    public void PruneAndRepair_KeepsBossAndAncientReachable()
    {
        var map = new StandardActMap(new Rng(11UL), numberOfRooms: 15, new MapPointTypeCounts(12, 7));

        // enablePruning defaults to true; constructor has already pruned. Boss remains reachable from Ancient.
        var reachable = new HashSet<MapPoint>();
        var queue = new Queue<MapPoint>();
        queue.Enqueue(map.StartingMapPoint);
        while (queue.Count > 0)
        {
            MapPoint current = queue.Dequeue();
            if (!reachable.Add(current))
            {
                continue;
            }

            foreach (MapPoint child in current.Children)
            {
                queue.Enqueue(child);
            }
        }

        Assert.Contains(map.BossMapPoint, reachable);
    }

    [Fact]
    public void PruneAndRepair_NoDuplicateSegmentsRemainAfterPruning()
    {
        var map = new StandardActMap(new Rng(12UL), numberOfRooms: 15, new MapPointTypeCounts(12, 7));

        var remaining = MapPathPruning.FindMatchingSegments(map.StartingMapPoint);

        Assert.Empty(remaining);
    }

    [Fact]
    public void PruneAndRepair_IsDeterministicForSameSeed()
    {
        var mapA = new StandardActMap(new Rng(13UL), numberOfRooms: 15, new MapPointTypeCounts(12, 7));
        var mapB = new StandardActMap(new Rng(13UL), numberOfRooms: 15, new MapPointTypeCounts(12, 7));

        int countA = mapA.GetAllMapPoints().Count();
        int countB = mapB.GetAllMapPoints().Count();

        Assert.Equal(countA, countB);
    }
}
