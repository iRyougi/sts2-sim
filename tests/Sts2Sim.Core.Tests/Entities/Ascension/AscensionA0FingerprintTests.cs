using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Ascension;

/// <summary>
/// Plan 08b-3g 的安全网：A10 词缀落地过程中，<c>ascensionLevel: 0</c> 的行为必须逐位不变。
/// <para>
/// Ascension 工作中的例外是 Inflation（Task 4）——当时的移牌费用硬编码成了 A6+ 的值，
/// A0 本来就是错的，那处修复由 <c>InflationRemovalCostTests</c> 单独固定，不在本指纹内。
/// 独立地图保真修复（2026-09-12：按成功剪枝数停止）允许刷新布局快照；并非 Ascension 语义变化。
/// </para>
/// <para>
/// 反面做法（把 NumOfElites 直接改成 8、把 GoldReward 元组直接改成 Poverty 数值）
/// 必须让本文件失败。如果它没失败，说明指纹写漏了，要先补指纹再继续。
/// </para>
/// </summary>
[Collection("ModelDb")]
public sealed class AscensionA0FingerprintTests : IDisposable
{
    public AscensionA0FingerprintTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    /// <summary>A0 三幕地图的点类型布局，按固定种子记录。</summary>
    [Theory]
    [InlineData(0, "a0-fingerprint-map")]
    [InlineData(1, "a0-fingerprint-map")]
    [InlineData(2, "a0-fingerprint-map")]
    public void A0_ActMapLayout_IsStableAcrossTheAscensionWork(int actIndex, string seed)
    {
        string first = DescribeMap(BuildRun(seed, actIndex).Map);
        string second = DescribeMap(BuildRun(seed, actIndex).Map);

        // 同种子必须自洽——这一条现在就成立，用来保证下面的快照是可复现的。
        Assert.Equal(first, second);
        // Exact snapshots also include separately reviewed source-fidelity repairs. The 2026-09-12
        // successful-prune-count fix changes A0 topology without changing Ascension semantics.
        Assert.Equal(Snapshot.MapLayout[actIndex], first);
    }

    /// <summary>A0 的精英目标数恒为 5——SwarmingElites 落地后 A0 侧不能变。</summary>
    [Fact]
    public void A0_EliteCount_IsFive()
    {
        Assert.Equal(5, new MapPointTypeCounts(unknownCount: 12, restCount: 7).NumOfElites);
    }

    /// <summary>A0 的战斗金币区间与抽取次数。Poverty 落地后 A0 侧不能变。</summary>
    [Theory]
    [InlineData(RoomType.Monster, 10, 20)]
    [InlineData(RoomType.Elite, 35, 45)]
    [InlineData(RoomType.Boss, 100, 100)]
    public void A0_CombatGoldReward_StaysInTheRecordedRange(RoomType roomType, int min, int max)
    {
        (RunState run, Player player) = BuildRunWithPlayer("a0-fingerprint-gold");
        int before = player.PlayerRng.Rewards.Counter;

        var reward = new GoldReward(roomType, player);
        reward.Populate(run);

        Assert.InRange(reward.Amount, min, max);
        // 上游正比例战斗金币始终抽取一次，包括 Boss 的 [100, 100]；零比例单独由回归锁覆盖。
        Assert.Equal(before + 1, player.PlayerRng.Rewards.Counter);
    }

    /// <summary>A0 地图上不存在第二 Boss 节点。</summary>
    [Fact]
    public void A0_MapHasNoSecondBossNode()
    {
        RunState run = BuildRun("a0-fingerprint-map", actIndex: 2);

        Assert.Single(run.Map.GetAllMapPoints().Append(run.Map.BossMapPoint),
            p => p.PointType == MapPointType.Boss);
    }

    private static RunState BuildRun(string seed, int actIndex)
    {
        var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()]);
        for (int i = 0; i < actIndex; i++)
        {
            run.AdvanceToNextAct();
        }
        return run;
    }

    private static (RunState, Player) BuildRunWithPlayer(string seed)
    {
        var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()]);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        return (run, player);
    }

    private static string DescribeMap(ActMap map) => string.Join(
        "|",
        map.GetAllMapPoints()
            .OrderBy(p => p.coord.row).ThenBy(p => p.coord.col)
            .Select(p => $"{p.coord.col},{p.coord.row}:{p.PointType}"));

    /// <summary>Fixed layout snapshots; Act 1/3 refreshed for the independently reviewed successful-prune-count repair.</summary>
    private static class Snapshot
    {
        public static readonly string[] MapLayout =
        [
            "0,1:Monster|5,1:Monster|0,2:Monster|1,2:Unknown|5,2:Shop|6,2:Unknown|0,3:Monster|2,3:Shop|5,3:Monster|6,3:Monster|0,4:Shop|1,4:Unknown|2,4:Monster|6,4:Unknown|0,5:Monster|1,5:Unknown|2,5:Monster|6,5:Unknown|0,6:Monster|1,6:RestSite|5,6:Elite|6,6:RestSite|0,7:Elite|1,7:Monster|4,7:RestSite|6,7:Unknown|0,8:Monster|1,8:RestSite|3,8:Monster|5,8:RestSite|0,9:Treasure|2,9:Treasure|4,9:Treasure|0,10:RestSite|1,10:Unknown|3,10:Monster|0,11:Monster|2,11:Unknown|0,12:Unknown|1,12:Monster|3,12:RestSite|0,13:Elite|1,13:Monster|2,13:Monster|4,13:Unknown|0,14:Monster|1,14:Elite|2,14:Unknown|5,14:Elite|1,15:RestSite|5,15:RestSite",
            "0,1:Monster|2,1:Monster|3,1:Monster|5,1:Monster|0,2:Monster|2,2:Monster|3,2:Unknown|6,2:Monster|0,3:Monster|1,3:Unknown|2,3:Monster|6,3:Monster|0,4:Monster|1,4:Unknown|6,4:Shop|0,5:Unknown|2,5:Monster|6,5:Unknown|0,6:Monster|1,6:Unknown|3,6:Monster|6,6:Elite|0,7:RestSite|1,7:Elite|2,7:Elite|3,7:RestSite|6,7:Unknown|0,8:Treasure|1,8:Treasure|3,8:Treasure|6,8:Treasure|0,9:RestSite|1,9:Monster|4,9:RestSite|6,9:Elite|0,10:Monster|1,10:Monster|2,10:RestSite|4,10:Unknown|6,10:Unknown|0,11:Unknown|1,11:RestSite|2,11:Unknown|4,11:RestSite|6,11:Unknown|0,12:Monster|1,12:Elite|3,12:Shop|6,12:Monster|0,13:Shop|1,13:Monster|3,13:Monster|6,13:Monster|1,14:RestSite|3,14:RestSite|6,14:RestSite",
            "2,1:Monster|6,1:Monster|1,2:Monster|6,2:Unknown|1,3:Monster|5,3:Monster|6,3:Unknown|0,4:Monster|2,4:Monster|4,4:Monster|6,4:Monster|0,5:Shop|2,5:Unknown|3,5:Monster|6,5:Unknown|0,6:RestSite|2,6:RestSite|6,6:Shop|0,7:Treasure|1,7:Treasure|5,7:Treasure|0,8:Elite|1,8:RestSite|2,8:Monster|4,8:Shop|6,8:Elite|0,9:Unknown|1,9:Monster|3,9:Unknown|6,9:RestSite|0,10:RestSite|2,10:Unknown|4,10:Unknown|6,10:Unknown|1,11:Unknown|3,11:Elite|4,11:Unknown|6,11:Unknown|1,12:Elite|2,12:Monster|4,12:Monster|6,12:Elite|1,13:RestSite|3,13:RestSite|5,13:RestSite",
        ];
    }
}
