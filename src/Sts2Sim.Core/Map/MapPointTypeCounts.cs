using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Map;

/// <summary>各点类型的目标数量。逐字移植（<c>MegaCrit.Sts2.Core.Map.MapPointTypeCounts</c>）。
/// <para>
/// 真实游戏走全局 <c>AscensionHelper.HasAscension</c>；本模拟器要并行跑，改为构造时传入
/// <see cref="AscensionManager"/>。<c>NumOfElites</c> 是纯算术，不消耗任何 RNG——
/// 这保证了同种子下 A0 的地图不会因为本改动而漂移。
/// </para></summary>
public sealed class MapPointTypeCounts
{
    public HashSet<MapPointType> PointTypesThatIgnoreRules { get; init; } = new();

    /// <summary>由构造函数按档位算出。改成只读（真实游戏是 <c>init</c>）是刻意的：
    /// 全仓库没有任何对象初始化器写它，留着 <c>init</c> 反而给了一条能悄悄绕过档位判断的路。</summary>
    public int NumOfElites { get; }

    public int NumOfShops { get; } = 3;

    public int NumOfUnknowns { get; }

    public int NumOfRests { get; }

    private MapPointTypeCounts(MapPointTypeCounts source)
    {
        NumOfElites = source.NumOfElites;
        NumOfShops = source.NumOfShops;
        NumOfUnknowns = source.NumOfUnknowns;
        NumOfRests = source.NumOfRests;
        PointTypesThatIgnoreRules = new HashSet<MapPointType>(source.PointTypesThatIgnoreRules);
    }

    internal MapPointTypeCounts CloneForRun() => new(this);
    public bool ShouldIgnoreMapPointRulesForMapPointType(MapPointType pointType) =>
        PointTypesThatIgnoreRules.Contains(pointType);

    /// <summary>逐字移植：真实游戏每个 act 的"?"点数量都用这个公式（均值 12，标准差 1，夹在 [10,14]）。</summary>
    public static int StandardRandomUnknownCount(Rng rng) => rng.NextGaussianInt(12, 1, 10, 14);

    public MapPointTypeCounts(int unknownCount, int restCount, AscensionManager? ascension = null)
    {
        NumOfUnknowns = unknownCount;
        NumOfRests = restCount;
        // 逐字对照上游的计算：精英点基础数量乘以档位倍率后四舍五入；Swarming Elites 的倍率为 1.6。
        NumOfElites = (int)Math.Round(
            5f * (ascension?.HasLevel(AscensionLevel.SwarmingElites) == true ? 1.6f : 1f));
    }
}
