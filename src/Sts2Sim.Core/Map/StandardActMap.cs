using System.Linq;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Map;

/// <summary>
/// 标准 act 地图生成算法。逐字移植（<c>MegaCrit.Sts2.Core.Map.StandardActMap</c>）——这是本计划移植保真度
/// 要求最高的类，路径生成/点类型分配/合法性规则五条全部逐字对照，不做任何算法层面简化。
/// 偏离 #46：构造函数直接接收 <c>numberOfRooms</c>/<c>MapPointTypeCounts</c>，取代游戏的
/// <c>ActModel actModel</c> 参数。偏离 #51：无 <c>isMultiplayer</c>/
/// <c>shouldReplaceTreasureWithElites</c>（固定单人、宝箱不换精英）。
/// </summary>
public sealed class StandardActMap : ActMap
{
    private const int Iterations = 7;
    private const int MapWidth = 7;

    private readonly MapPointTypeCounts _pointTypeCounts;
    private readonly int _mapLength;
    private readonly Rng _rng;

    private static readonly HashSet<MapPointType> LowerMapPointRestrictions = new()
    {
        MapPointType.RestSite,
        MapPointType.Elite,
    };

    private static readonly HashSet<MapPointType> UpperMapPointRestrictions = new()
    {
        MapPointType.RestSite,
    };

    private static readonly HashSet<MapPointType> ParentMapPointRestrictions = new()
    {
        MapPointType.Elite,
        MapPointType.RestSite,
        MapPointType.Treasure,
        MapPointType.Shop,
    };

    private static readonly HashSet<MapPointType> ChildMapPointRestrictions = new()
    {
        MapPointType.Elite,
        MapPointType.RestSite,
        MapPointType.Treasure,
        MapPointType.Shop,
    };

    private static readonly HashSet<MapPointType> SiblingPointTypeRestrictions = new()
    {
        MapPointType.RestSite,
        MapPointType.Monster,
        MapPointType.Unknown,
        MapPointType.Elite,
        MapPointType.Shop,
    };

    public override MapPoint BossMapPoint { get; }

    public override MapPoint StartingMapPoint { get; }

    public override MapPoint? SecondBossMapPoint { get; }

    protected override MapPoint?[,] Grid { get; }

    /// <param name="mapRng">地图专用 rng（由调用方从 run 的 rng 派生，见 Task 6 <c>StandardActMap.CreateFor</c>）。</param>
    /// <param name="numberOfRooms">不含 Boss 行的房间数（真实 Overgrowth 是 15，见 Task 10）。</param>
    /// <param name="pointTypeCounts">各点类型目标数量。</param>
    /// <param name="hasSecondBoss">A10 DoubleBoss：在 Boss 之后再挂一个 Boss 节点。</param>
    /// <param name="enablePruning">测试用：关闭剪枝以便断言剪枝前的原始拓扑。生产路径必须为 true。</param>
    public StandardActMap(
        Rng mapRng,
        int numberOfRooms,
        MapPointTypeCounts pointTypeCounts,
        bool hasSecondBoss = false,
        bool enablePruning = true)
    {
        _mapLength = numberOfRooms + 1;
        Grid = new MapPoint[7, _mapLength];
        _rng = mapRng;
        _pointTypeCounts = pointTypeCounts;
        BossMapPoint = new MapPoint(GetColumnCount() / 2, GetRowCount());
        StartingMapPoint = new MapPoint(GetColumnCount() / 2, 0);
        if (hasSecondBoss)
        {
            SecondBossMapPoint = new MapPoint(GetColumnCount() / 2, GetRowCount() + 1);
        }

        GenerateMap();
        AssignPointTypes();
        if (enablePruning)
        {
            MapPathPruning.PruneAndRepair(Grid, startMapPoints, this, _pointTypeCounts, _rng, IsValidPointType);
        }

        // 偏离 #52（已解决）：这三步会改写 coord.col，见 MapPostProcessing 类注释。
        Grid = MapPostProcessing.CenterGrid(Grid);
        Grid = MapPostProcessing.SpreadAdjacentMapPoints(Grid);
        Grid = MapPostProcessing.StraightenPaths(Grid);
    }

    private MapPoint GetOrCreatePoint(int col, int row)
    {
        MapPoint? point = GetPoint(col, row);
        if (point != null)
        {
            return point;
        }

        point = new MapPoint(col, row);
        Grid[col, row] = point;
        return point;
    }

    private void PathGenerate(MapPoint startingPoint)
    {
        MapPoint current = startingPoint;
        while (current.coord.row < _mapLength - 1)
        {
            MapCoord next = GenerateNextCoord(current);
            MapPoint nextPoint = GetOrCreatePoint(next.col, next.row);
            current.AddChildPoint(nextPoint);
            current = nextPoint;
        }
    }

    private MapCoord GenerateNextCoord(MapPoint current)
    {
        int col = current.coord.col;
        int left = Math.Max(0, col - 1);
        int right = Math.Min(col + 1, 6);

        var offsets = new List<int> { -1, 0, 1 };
        offsets.StableShuffle(_rng);

        foreach (int offset in offsets)
        {
            int row = current.coord.row + 1;
            int candidateCol = offset switch
            {
                -1 => left,
                0 => col,
                1 => right,
                _ => throw new InvalidOperationException("This isn't possible"),
            };

            if (!HasInvalidCrossover(current, candidateCol))
            {
                return new MapCoord(candidateCol, row);
            }
        }

        throw new InvalidOperationException("Cannot find next node");
    }

    /// <summary>检测"X 型"路径交叉：若目标列上已有一个节点，且那个节点有一个子节点正好穿到 current 的列，
    /// 说明两条路径会视觉交叉，禁止。</summary>
    private bool HasInvalidCrossover(MapPoint current, int targetCol)
    {
        int delta = targetCol - current.coord.col;
        if (delta == 0 || delta == 7)
        {
            return false;
        }

        MapPoint? existing = Grid[targetCol, current.coord.row];
        if (existing is null)
        {
            return false;
        }

        foreach (MapPoint child in existing.Children)
        {
            int childDelta = child.coord.col - existing.coord.col;
            if (childDelta == -delta)
            {
                return true;
            }
        }

        return false;
    }

    private void GenerateMap()
    {
        for (int i = 0; i < Iterations; i++)
        {
            MapPoint start = GetOrCreatePoint(_rng.NextInt(0, MapWidth), 1);
            if (i == 1)
            {
                while (startMapPoints.Contains(start))
                {
                    start = GetOrCreatePoint(_rng.NextInt(0, MapWidth), 1);
                }
            }

            startMapPoints.Add(start);
            PathGenerate(start);
        }

        ForEachInRow(GetRowCount() - 1, x => x.AddChildPoint(BossMapPoint));
        if (SecondBossMapPoint is { } secondBoss)
        {
            BossMapPoint.AddChildPoint(secondBoss);
        }

        ForEachInRow(1, x => StartingMapPoint.AddChildPoint(x));
    }

    private void ForEachInRow(int rowIndex, Action<MapPoint> processor)
    {
        for (int col = 0; col < Grid.GetLength(0); col++)
        {
            if (Grid[col, rowIndex] is { } point)
            {
                processor(point);
            }
        }
    }

    private void AssignPointTypes()
    {
        ForEachInRow(GetRowCount() - 1, p =>
        {
            p.PointType = MapPointType.RestSite;
            p.CanBeModified = false;
        });

        ForEachInRow(GetRowCount() - 7, p =>
        {
            p.PointType = MapPointType.Treasure;
            p.CanBeModified = false;
        });

        ForEachInRow(1, p =>
        {
            p.PointType = MapPointType.Monster;
            p.CanBeModified = false;
        });

        var toAssign = new List<MapPointType>();
        toAssign.AddRange(Enumerable.Repeat(MapPointType.RestSite, _pointTypeCounts.NumOfRests));
        toAssign.AddRange(Enumerable.Repeat(MapPointType.Shop, _pointTypeCounts.NumOfShops));
        toAssign.AddRange(Enumerable.Repeat(MapPointType.Elite, _pointTypeCounts.NumOfElites));
        toAssign.AddRange(Enumerable.Repeat(MapPointType.Unknown, _pointTypeCounts.NumOfUnknowns));

        var queue = new Queue<MapPointType>(toAssign);
        AssignRemainingTypesToRandomPoints(queue);

        foreach (MapPoint point in GetAllMapPoints().Where(p => p.PointType == MapPointType.Unassigned))
        {
            point.PointType = MapPointType.Monster;
        }

        BossMapPoint.PointType = MapPointType.Boss;
        StartingMapPoint.PointType = MapPointType.Ancient;
        if (SecondBossMapPoint is { } secondBossPoint)
        {
            secondBossPoint.PointType = MapPointType.Boss;
        }
    }

    /// <summary>最多 3 轮，每轮把剩余队列里的类型尽量分配给随机打乱的未分配点。</summary>
    private void AssignRemainingTypesToRandomPoints(Queue<MapPointType> pointTypesToBeAssigned)
    {
        for (int round = 0; round < 3; round++)
        {
            if (pointTypesToBeAssigned.Count == 0)
            {
                break;
            }

            List<MapPoint> unassigned = GetAllMapPoints().Where(p => p.PointType == MapPointType.Unassigned).ToList();
            unassigned.StableShuffle(_rng);

            foreach (MapPoint point in unassigned)
            {
                if (pointTypesToBeAssigned.Count == 0)
                {
                    break;
                }

                point.PointType = GetNextValidPointType(pointTypesToBeAssigned, point);
            }
        }
    }

    /// <summary>从队列头部找起，找到第一个对该点合法（或被标记为"忽略规则"）的类型，取出返回；
    /// 途中跳过的类型重新入队（保留在队列里，下一个点再试）。若整个队列都不合法，返回 Unassigned
    /// （该点这一轮保持未分配，留给下一轮或"补 Monster"兜底）。</summary>
    private MapPointType GetNextValidPointType(Queue<MapPointType> pointTypesQueue, MapPoint mapPoint)
    {
        int count = pointTypesQueue.Count;
        for (int i = 0; i < count; i++)
        {
            MapPointType candidate = pointTypesQueue.Dequeue();
            if (_pointTypeCounts.ShouldIgnoreMapPointRulesForMapPointType(candidate) || IsValidPointType(candidate, mapPoint))
            {
                return candidate;
            }

            pointTypesQueue.Enqueue(candidate);
        }

        return MapPointType.Unassigned;
    }

    public bool IsValidPointType(MapPointType pointType, MapPoint mapPoint)
    {
        return IsValidForUpper(pointType, mapPoint)
            && IsValidForLower(pointType, mapPoint)
            && IsValidWithParents(pointType, mapPoint)
            && IsValidWithChildren(pointType, mapPoint)
            && IsValidWithSiblings(pointType, mapPoint);
    }

    /// <summary>这些类型不能出现在地图下半部分（越靠近开局的行）。</summary>
    private static bool IsValidForLower(MapPointType pointType, MapPoint mapPoint)
    {
        return mapPoint.coord.row >= 6 || !LowerMapPointRestrictions.Contains(pointType);
    }

    /// <summary>这些类型不能出现在地图上半部分（越靠近 Boss 的行）。</summary>
    private bool IsValidForUpper(MapPointType pointType, MapPoint mapPoint)
    {
        return mapPoint.coord.row < _mapLength - 3 || !UpperMapPointRestrictions.Contains(pointType);
    }

    /// <summary>不能与父节点连续出现相同类型（含子节点方向，因为父子是同一条限制集合）。</summary>
    private static bool IsValidWithParents(MapPointType pointType, MapPoint mapPoint)
    {
        if (!ParentMapPointRestrictions.Contains(pointType))
        {
            return true;
        }

        return !mapPoint.parents.Concat(mapPoint.Children).Any(p => p.PointType == pointType);
    }

    /// <summary>不能与任意子节点连续出现相同类型。</summary>
    private static bool IsValidWithChildren(MapPointType pointType, MapPoint mapPoint)
    {
        if (!ChildMapPointRestrictions.Contains(pointType))
        {
            return true;
        }

        return !mapPoint.Children.Any(p => p.PointType == pointType);
    }

    /// <summary>这些类型互相之间不能是"兄弟"（共享同一父节点的不同节点）。</summary>
    private static bool IsValidWithSiblings(MapPointType pointType, MapPoint mapPoint)
    {
        if (!SiblingPointTypeRestrictions.Contains(pointType))
        {
            return true;
        }

        return !GetSiblings(mapPoint).Any(p => p.PointType == pointType);
    }

    private static IEnumerable<MapPoint> GetSiblings(MapPoint mapPoint)
    {
        return mapPoint.parents.SelectMany(p => p.Children).Where(sibling => !ReferenceEquals(sibling, mapPoint));
    }
}
