namespace Sts2Sim.Core.Map;

/// <summary>负责创建地图节点的抽象基类。逐字移植（<c>MegaCrit.Sts2.Core.Map.ActMap</c>）。</summary>
public abstract class ActMap
{
    public readonly HashSet<MapPoint> startMapPoints = new();

    public abstract MapPoint BossMapPoint { get; }

    public abstract MapPoint StartingMapPoint { get; }

    /// <summary>A10 DoubleBoss 的第二个 Boss 节点；非 DoubleBoss 时为 null。
    /// 逐字对照 <c>MegaCrit.Sts2.Core.Map.ActMap.cs:277</c>。</summary>
    public virtual MapPoint? SecondBossMapPoint => null;

    protected abstract MapPoint?[,] Grid { get; }

    internal abstract ActMap CloneForRun(
        IReadOnlyDictionary<Models.AbstractModel, Models.AbstractModel> modelMap,
        Random.Rng? replacementRng = null);

    protected (MapPoint?[,] Grid, Dictionary<MapPoint, MapPoint> Points) ClonePointGraph(
        IReadOnlyDictionary<Models.AbstractModel, Models.AbstractModel> modelMap)
    {
        int columns = Grid.GetLength(0);
        int rows = Grid.GetLength(1);
        var copied = new MapPoint?[columns, rows];
        var points = new Dictionary<MapPoint, MapPoint>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<MapPoint>();
        MapPoint Copy(MapPoint original)
        {
            if (!points.TryGetValue(original, out MapPoint? clone))
            {
                clone = original.CloneForRun(modelMap);
                points.Add(original, clone);
                pending.Enqueue(original);
            }
            return clone;
        }
        for (int col = 0; col < columns; col++)
            for (int row = 0; row < rows; row++)
                copied[col, row] = Grid[col, row] is { } point ? Copy(point) : null;
        Copy(StartingMapPoint);
        Copy(BossMapPoint);
        if (SecondBossMapPoint is { } secondBoss) Copy(secondBoss);
        foreach (MapPoint start in startMapPoints) Copy(start);
        while (pending.TryDequeue(out MapPoint? original))
        {
            foreach (MapPoint child in original.Children) Copy(child);
            foreach (MapPoint parent in original.parents) Copy(parent);
        }
        foreach ((MapPoint original, MapPoint clone) in points)
        {
            // Each HashSet has its own observable enumeration order. AddChildPoint would
            // rebuild parent order from another node's traversal instead of copying it.
            foreach (MapPoint child in original.Children) clone.Children.Add(points[child]);
            foreach (MapPoint parent in original.parents) clone.parents.Add(points[parent]);
        }
        return (copied, points);
    }

    public int GetColumnCount() => Grid.GetLength(0);

    public int GetRowCount() => Grid.GetLength(1);

    public IEnumerable<MapPoint> GetAllMapPoints()
    {
        for (int col = 0; col < GetColumnCount(); col++)
        {
            for (int row = 0; row < GetRowCount(); row++)
            {
                if (Grid[col, row] is { } point)
                {
                    yield return point;
                }
            }
        }
    }

    public IEnumerable<MapPoint> GetPointsInRow(int row)
    {
        if (row < 0 || row >= GetRowCount())
        {
            yield break;
        }

        for (int col = 0; col < GetColumnCount(); col++)
        {
            if (Grid[col, row] is { } point)
            {
                yield return point;
            }
        }
    }

    public virtual MapPoint? GetPoint(MapCoord coord) => GetPoint(coord.col, coord.row);

    public MapPoint? GetPoint(int col, int row)
    {
        if (col == BossMapPoint.coord.col && row == BossMapPoint.coord.row)
        {
            return BossMapPoint;
        }

        if (col == StartingMapPoint.coord.col && row == StartingMapPoint.coord.row)
        {
            return StartingMapPoint;
        }

        if (SecondBossMapPoint is { } secondBoss &&
            col == secondBoss.coord.col && row == secondBoss.coord.row)
        {
            return secondBoss;
        }

        if (col >= 0 && col < Grid.GetLength(0) && row >= 0 && row < Grid.GetLength(1))
        {
            return Grid[col, row];
        }

        return null;
    }

    public bool HasPoint(MapCoord coord)
    {
        if (coord.col == BossMapPoint.coord.col && coord.row == BossMapPoint.coord.row)
        {
            return true;
        }

        if (coord.col == StartingMapPoint.coord.col && coord.row == StartingMapPoint.coord.row)
        {
            return true;
        }

        if (SecondBossMapPoint is { } secondBossPoint &&
            coord.col == secondBossPoint.coord.col && coord.row == secondBossPoint.coord.row)
        {
            return true;
        }

        if (coord.col < 0 || coord.col >= Grid.GetLength(0) || coord.row < 0 || coord.row >= Grid.GetLength(1))
        {
            return false;
        }

        return Grid[coord.col, coord.row] != null;
    }
}
