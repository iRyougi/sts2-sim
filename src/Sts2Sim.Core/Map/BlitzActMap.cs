using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Map;

public sealed class BlitzActMap : ActMap
{
    public static readonly MapPointType[] pointTypes =
    [
        MapPointType.Monster,
        MapPointType.Monster,
        MapPointType.Shop,
        MapPointType.Elite,
        MapPointType.Treasure,
        MapPointType.RestSite,
    ];

    private const int _width = 7;
    private const int _middle = 3;

    public override MapPoint BossMapPoint { get; }

    public override MapPoint StartingMapPoint { get; }

    public override MapPoint? SecondBossMapPoint { get; }

    protected override MapPoint?[,] Grid { get; }

    public BlitzActMap(bool hasSecondBoss = false)
    {
        int rowCount = pointTypes.Length;
        Grid = new MapPoint?[_width, rowCount + 1];
        BossMapPoint = new MapPoint(_middle, rowCount + 1)
        {
            PointType = MapPointType.Boss,
        };
        StartingMapPoint = new MapPoint(_middle, 0)
        {
            PointType = MapPointType.Ancient,
        };
        if (hasSecondBoss)
        {
            SecondBossMapPoint = new MapPoint(_middle, rowCount + 2)
            {
                PointType = MapPointType.Boss,
            };
        }

        ConnectChildren();
    }

    private BlitzActMap(
        BlitzActMap source,
        IReadOnlyDictionary<Models.AbstractModel, Models.AbstractModel> modelMap)
    {
        (MapPoint?[,] grid, Dictionary<MapPoint, MapPoint> points) = source.ClonePointGraph(modelMap);
        Grid = grid;
        StartingMapPoint = points[source.StartingMapPoint];
        BossMapPoint = points[source.BossMapPoint];
        SecondBossMapPoint = source.SecondBossMapPoint is { } secondBoss ? points[secondBoss] : null;
        foreach (MapPoint point in source.startMapPoints)
        {
            startMapPoints.Add(points[point]);
        }
    }

    internal override ActMap CloneForRun(
        IReadOnlyDictionary<Models.AbstractModel, Models.AbstractModel> modelMap,
        Random.Rng? replacementRng = null) => new BlitzActMap(this, modelMap);

    /// <summary>按起点子节点链顺序返回极速单线地图节点序列。</summary>
    public List<MapPoint> GetMapPoints()
    {
        var points = new List<MapPoint>();
        var visited = new HashSet<MapPoint>(ReferenceEqualityComparer.Instance);
        for (MapPoint? point = StartingMapPoint; point is not null; point = point.Children.FirstOrDefault())
        {
            if (!visited.Add(point))
            {
                throw new InvalidOperationException("BlitzActMap contains a cyclic path.");
            }

            points.Add(point);
        }

        return points;
    }

    internal static void ValidateStartToBossChain(
        MapPoint startingMapPoint,
        MapPoint bossMapPoint,
        MapPoint? secondBossMapPoint,
        int totalPointCount,
        Func<string, Exception> createError)
    {
        if (ReferenceEquals(startingMapPoint, bossMapPoint))
        {
            throw createError("BlitzActMap start and boss must be distinct.");
        }

        if (secondBossMapPoint is not null &&
            (ReferenceEquals(secondBossMapPoint, startingMapPoint) ||
             ReferenceEquals(secondBossMapPoint, bossMapPoint)))
        {
            throw createError("BlitzActMap second boss must be distinct.");
        }

        var visited = new HashSet<MapPoint>(ReferenceEqualityComparer.Instance);
        MapPoint terminal = secondBossMapPoint ?? bossMapPoint;
        MapPoint current = startingMapPoint;
        MapPoint? previous = null;
        bool sawBoss = false;

        while (true)
        {
            if (!visited.Add(current))
            {
                throw createError("BlitzActMap contains a cyclic path.");
            }

            if (previous is null)
            {
                if (current.parents.Count != 0)
                {
                    throw createError("BlitzActMap starting node cannot have parents.");
                }
            }
            else if (current.parents.Count != 1 || !current.parents.Contains(previous))
            {
                throw createError("BlitzActMap path contains invalid parent linkage.");
            }

            if (ReferenceEquals(current, bossMapPoint))
            {
                sawBoss = true;
                if (secondBossMapPoint is not null)
                {
                    if (current.Children.Count != 1 || !current.Children.Contains(secondBossMapPoint))
                    {
                        throw createError("BlitzActMap boss must link solely to second boss.");
                    }
                }
                else if (current.Children.Count != 0)
                {
                    throw createError("BlitzActMap boss cannot have outgoing children.");
                }
            }

            if (ReferenceEquals(current, terminal))
            {
                if (current.Children.Count != 0)
                {
                    throw createError("BlitzActMap terminal node cannot have outgoing children.");
                }

                break;
            }

            if (current.Children.Count != 1)
            {
                throw createError("BlitzActMap path must form a single unbranched chain to boss.");
            }

            previous = current;
            current = current.Children.First();
        }

        if (!sawBoss || !ReferenceEquals(current, terminal) || visited.Count != totalPointCount)
        {
            throw createError("BlitzActMap graph must be a single connected chain from start to boss.");
        }
    }

    private void ConnectChildren()
    {
        for (int i = 0; i < pointTypes.Length; i++)
        {
            var point = new MapPoint(_middle, i + 1)
            {
                PointType = pointTypes[i],
            };
            Grid[_middle, i + 1] = point;
            if (i > 0)
            {
                Grid[_middle, i]?.AddChildPoint(point);
            }
        }

        Grid[_middle, pointTypes.Length]?.AddChildPoint(BossMapPoint);
        StartingMapPoint.AddChildPoint(Grid[_middle, 1]!);
        if (SecondBossMapPoint is not null)
        {
            BossMapPoint.AddChildPoint(SecondBossMapPoint);
        }
    }

    internal BlitzActMap(
        MapPoint?[,] grid,
        MapPoint startingMapPoint,
        MapPoint bossMapPoint,
        MapPoint? secondBossMapPoint,
        IEnumerable<MapPoint> starts)
    {
        Grid = grid;
        StartingMapPoint = startingMapPoint;
        BossMapPoint = bossMapPoint;
        SecondBossMapPoint = secondBossMapPoint;
        foreach (MapPoint point in starts)
        {
            startMapPoints.Add(point);
        }

        var allPoints = new HashSet<MapPoint>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<MapPoint>();
        void Track(MapPoint? candidate)
        {
            if (candidate is not null && allPoints.Add(candidate))
            {
                pending.Enqueue(candidate);
            }
        }

        for (int col = 0; col < grid.GetLength(0); col++)
        {
            for (int row = 0; row < grid.GetLength(1); row++)
            {
                Track(grid[col, row]);
            }
        }

        Track(startingMapPoint);
        Track(bossMapPoint);
        Track(secondBossMapPoint);
        foreach (MapPoint start in startMapPoints)
        {
            Track(start);
        }

        while (pending.TryDequeue(out MapPoint? node))
        {
            foreach (MapPoint child in node.Children)
            {
                Track(child);
            }

            foreach (MapPoint parent in node.parents)
            {
                Track(parent);
            }
        }

        ValidateStartToBossChain(
            startingMapPoint,
            bossMapPoint,
            secondBossMapPoint,
            allPoints.Count,
            message => new InvalidOperationException(message));
    }
}
