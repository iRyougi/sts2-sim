using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Map;

/// <summary>
/// A1 SwarmingElites：精英目标数从 5 变 8。
/// 真实公式 <c>(int)Math.Round(5f * (has ? 1.6f : 1f))</c>（`MapPointTypeCounts.cs:15`）。
/// </summary>
[Collection("ModelDb")]
public sealed class SwarmingElitesTests : IDisposable
{
    public SwarmingElitesTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 8)]
    [InlineData(10, 8)]
    public void NumOfElites_FollowsSwarmingElites(int ascensionLevel, int expected)
    {
        var counts = new MapPointTypeCounts(
            unknownCount: 12,
            restCount: 7,
            ascension: new AscensionManager(ascensionLevel));

        Assert.Equal(expected, counts.NumOfElites);
    }

    /// <summary>加档位判断不能消耗 mapRng——否则同种子下 A0 的地图会漂移。</summary>
    [Fact]
    public void GetMapPointTypes_ConsumesTheSameRngAtEveryAscension()
    {
        var a0 = new Rng(4242UL);
        var a10 = new Rng(4242UL);

        MapPointTypeCounts fromA0 = new Overgrowth().GetMapPointTypes(a0, new AscensionManager(0));
        MapPointTypeCounts fromA10 = new Overgrowth().GetMapPointTypes(a10, new AscensionManager(10));

        Assert.Equal(a0.Counter, a10.Counter);
        Assert.Equal(fromA0.NumOfUnknowns, fromA10.NumOfUnknowns);
        Assert.Equal(fromA0.NumOfRests, fromA10.NumOfRests);
        Assert.Equal(5, fromA0.NumOfElites);
        Assert.Equal(8, fromA10.NumOfElites);
    }

    /// <summary>A10 的实际地图上精英点数量应当明显多于 A0。</summary>
    [Fact]
    public void ActOneMap_HasMoreElitesAtAscensionTen()
    {
        int a0 = CountElites(new RunState("swarming-elites", new Overgrowth()));
        int a10 = CountElites(new RunState("swarming-elites", new Overgrowth(), ascensionLevel: 10));

        Assert.True(a10 > a0, $"A10 精英点 {a10} 应多于 A0 的 {a0}");
    }

    private static int CountElites(RunState run) =>
        run.Map.GetAllMapPoints().Count(p => p.PointType == MapPointType.Elite);
}
