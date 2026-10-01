using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Map;

/// <summary>The fixed single-route act map created by GoldenCompass.</summary>
public sealed class GoldenPathActMap : ActMap
{
    private static readonly MapPointType[] DefaultPointTypes =
    [
        MapPointType.Monster, MapPointType.Unknown, MapPointType.Monster,
        MapPointType.RestSite, MapPointType.Monster, MapPointType.RestSite,
        MapPointType.Unknown, MapPointType.Treasure, MapPointType.Unknown,
        MapPointType.Treasure, MapPointType.Unknown, MapPointType.Shop,
        MapPointType.Elite, MapPointType.RestSite, MapPointType.Elite,
        MapPointType.RestSite,
    ];

    public override MapPoint BossMapPoint { get; }
    public override MapPoint StartingMapPoint { get; }
    protected override MapPoint?[,] Grid { get; }

    public GoldenPathActMap(IRunState runState)
    {
        List<MapPointType> pointTypes = DefaultPointTypes.ToList();
        if (runState.Players.Count > 1)
            pointTypes.RemoveAt(2);

        Grid = new MapPoint[7, pointTypes.Count + 1];
        BossMapPoint = new MapPoint(GetColumnCount() / 2, GetRowCount())
        {
            PointType = MapPointType.Boss,
        };
        StartingMapPoint = new MapPoint(GetColumnCount() / 2, 0)
        {
            PointType = MapPointType.Ancient,
        };

        for (int i = 0; i < pointTypes.Count; i++)
        {
            var point = new MapPoint(3, i + 1) { PointType = pointTypes[i] };
            Grid[3, i + 1] = point;
            if (i > 0)
                Grid[3, i]!.AddChildPoint(point);
        }

        startMapPoints.Add(Grid[3, 1]!);
        Grid[3, GetRowCount() - 1]!.AddChildPoint(BossMapPoint);
        StartingMapPoint.AddChildPoint(Grid[3, 1]!);
    }
}
