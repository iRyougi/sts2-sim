namespace Sts2Sim.Core.Tests.Entities;

using Sts2Sim.Core.Entities.Ascension;

public class AscensionManagerTests
{
    [Fact]
    public void HasLevel_IsCumulative()
    {
        // 设计文档 v2：Ascension 是累积等级制——A7 意味着 A1..A7 全部生效
        var a7 = new AscensionManager(AscensionLevel.Scarcity);
        Assert.True(a7.HasLevel(AscensionLevel.SwarmingElites)); // A1
        Assert.True(a7.HasLevel(AscensionLevel.Scarcity));       // A7
        Assert.False(a7.HasLevel(AscensionLevel.ToughEnemies));  // A8
        Assert.False(a7.HasLevel(AscensionLevel.DoubleBoss));    // A10
    }

    [Fact]
    public void A0_HasNoLevels()
    {
        var a0 = new AscensionManager(0);
        Assert.False(a0.HasLevel(AscensionLevel.SwarmingElites));
        // 反直觉但与游戏一致：HasLevel(None) 在 A0 也为 true（0 >= 0）
        Assert.True(a0.HasLevel(AscensionLevel.None));
    }

    [Fact]
    public void MaxAscension_IsTen()
    {
        Assert.Equal(10, AscensionManager.maxAscensionAllowed);
    }
}
