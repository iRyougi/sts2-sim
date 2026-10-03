namespace Sts2Sim.Core.Map;

/// <summary>
/// 地图上的一个节点。逐字移植图结构部分（<c>MegaCrit.Sts2.Core.Map.MapPoint</c>）。
/// 偏离 #53：不含 <c>BFS_FindPath</c>/<c>GetFirstCommonDescendant</c>/<c>GetCommonAncestor</c>/
/// <c>GetLastJunctionLength</c>/<c>LeftChild</c>/<c>RightChild</c>——地图生成算法
/// （<c>StandardActMap</c>/<c>MapPathPruning</c>）不使用它们，它们服务于未移植的 UI/历史记录功能。
/// </summary>
public sealed class MapPoint : IComparable<MapPoint>
{
    private readonly List<Models.AbstractModel> _quests = [];
    public IReadOnlyList<Models.AbstractModel> Quests => _quests.AsReadOnly();
    public void AddQuest(Models.AbstractModel model) => _quests.Add(model);
    public void RemoveQuest(Models.AbstractModel model) => _quests.Remove(model);

    internal MapPoint CloneForRun(IReadOnlyDictionary<Models.AbstractModel, Models.AbstractModel> modelMap)
    {
        var clone = new MapPoint(coord.col, coord.row)
        {
            PointType = PointType,
            CanBeModified = CanBeModified,
        };
        foreach (Models.AbstractModel quest in _quests)
        {
            if (!modelMap.TryGetValue(quest, out Models.AbstractModel? clonedQuest))
                throw new InvalidOperationException($"Cannot clone map quest {quest.GetType().Name} without an owned model mapping.");
            clone._quests.Add(clonedQuest);
        }
        return clone;
    }

    public readonly HashSet<MapPoint> parents = new();

    public MapCoord coord;

    /// <summary>地图生成完成后，用于防止后续系统（未移植的 relic/act 效果）修改此点类型。</summary>
    public bool CanBeModified { get; set; } = true;

    public MapPointType PointType { get; set; }

    public HashSet<MapPoint> Children { get; } = new();

    public MapPoint(int col, int row)
    {
        coord = new MapCoord(col, row);
    }

    public void AddChildPoint(MapPoint child)
    {
        Children.Add(child);
        child.parents.Add(this);
    }

    public void RemoveChildPoint(MapPoint child)
    {
        Children.Remove(child);
        child.parents.Remove(this);
    }

    public int CompareTo(MapPoint? other) => other is null ? 1 : coord.CompareTo(other.coord);

    public override string ToString() => $"Point[{coord.col},{coord.row}]";
}
