namespace Sts2Sim.Core.Tests.Odds;

using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

public class UnknownMapPointOddsTests
{
    [Fact]
    public void BaseOdds_MatchGameConstants()
    {
        var odds = new UnknownMapPointOdds(new Rng(1u), NullOddsHooks.Instance);
        Assert.Equal(0.1f, odds.MonsterOdds);
        Assert.Equal(-1f, odds.EliteOdds);
        Assert.Equal(0.02f, odds.TreasureOdds);
        Assert.Equal(0.03f, odds.ShopOdds);
    }

    [Fact]
    public void EventOdds_IsOneMinusPositiveNonEventOdds()
    {
        var odds = new UnknownMapPointOdds(new Rng(1u), NullOddsHooks.Instance);
        // 1 - (0.1 + 0.02 + 0.03)；Elite 是 -1 不计入
        Assert.Equal(0.85f, odds.EventOdds, 5);
    }

    /// <summary>偏离 #327 的回归锁。上游首局分支对前三个 ? 房间提前 return，
    /// 那三次**不消耗 RNG**——正是它让两侧 RNG 流从第一个 ? 房间起就永久错位。
    /// 本项目不移植该分支，所以每一次 Roll 都必须消耗一次 RNG。
    /// 这里只查消耗、不复算赔率，避免"测试复算实现"。</summary>
    [Fact]
    public void Roll_ConsumesRngOnEveryUnknownPoint_WithoutFirstRunShortcut()
    {
        var rng = new Rng(1u);
        var odds = new UnknownMapPointOdds(rng, NullOddsHooks.Instance);
        for (int i = 0; i < 3; i++)
        {
            int before = rng.Counter;
            odds.Roll(Array.Empty<RoomType>());
            Assert.True(rng.Counter > before, $"第 {i + 1} 次 Roll 没有消耗 RNG");
        }
    }

    [Fact]
    public void Roll_MatchesManualCumulativeDistribution()
    {
        var rng = new Rng(555u);
        var twin = new Rng(555u);
        var odds = new UnknownMapPointOdds(rng, NullOddsHooks.Instance);
        float roll = twin.NextFloat();
        // 累积区间（字典插入序 Monster, Elite(跳过), Treasure, Shop）：
        // roll <= 0.10 → Monster；<= 0.12 → Treasure；<= 0.15 → Shop；否则 Event
        RoomType expected = roll <= 0.1f ? RoomType.Monster
            : roll <= 0.12f ? RoomType.Treasure
            : roll <= 0.15f ? RoomType.Shop
            : RoomType.Event;
        Assert.Equal(expected, odds.Roll(Array.Empty<RoomType>()));
    }

    [Fact]
    public void Roll_GrowsUnrolledOdds_AndResetsRolledType()
    {
        var odds = new UnknownMapPointOdds(new Rng(555u), NullOddsHooks.Instance);
        RoomType rolled = odds.Roll(Array.Empty<RoomType>());
        // roll 中的房型重置为基础值；未 roll 中的房型概率增加各自的基础值
        if (rolled != RoomType.Monster)
        {
            Assert.Equal(0.2f, odds.MonsterOdds, 5);
        }
        else
        {
            Assert.Equal(0.1f, odds.MonsterOdds, 5);
        }
        if (rolled != RoomType.Shop)
        {
            Assert.Equal(0.06f, odds.ShopOdds, 5);
        }
    }

    [Fact]
    public void Blacklist_ExcludesRoomType()
    {
        var odds = new UnknownMapPointOdds(new Rng(555u), NullOddsHooks.Instance);
        // 拉黑 Monster 后即使 roll 落在 Monster 区间也不会返回 Monster
        for (int i = 0; i < 50; i++)
        {
            RoomType r = odds.Roll(new[] { RoomType.Monster });
            Assert.NotEqual(RoomType.Monster, r);
        }
    }

    [Fact]
    public void ResetToBase_RestoresAllOdds()
    {
        var odds = new UnknownMapPointOdds(new Rng(555u), NullOddsHooks.Instance);
        for (int i = 0; i < 5; i++) odds.Roll(Array.Empty<RoomType>());
        odds.ResetToBase();
        Assert.Equal(0.1f, odds.MonsterOdds);
        Assert.Equal(0.02f, odds.TreasureOdds);
        Assert.Equal(0.03f, odds.ShopOdds);
    }

    [Fact]
    public void HookRemovingRoomType_PreventsRollAndGrowth()
    {
        var odds = new UnknownMapPointOdds(new Rng(555u), new NoMonsterHooks());
        for (int i = 0; i < 20; i++)
        {
            RoomType r = odds.Roll(Array.Empty<RoomType>());
            Assert.NotEqual(RoomType.Monster, r);
            // Monster 不在 hook 过滤后的集合里 → 既不会被 roll 中，也不参与增长
            Assert.Equal(0.1f, odds.MonsterOdds, 5);
        }
    }

    [Fact]
    public void HookModifyingIncrease_ScalesUnrolledGrowth()
    {
        var odds = new UnknownMapPointOdds(new Rng(555u), new DoubleIncreaseHooks());
        RoomType rolled = odds.Roll(Array.Empty<RoomType>());
        // 未被 roll 中的房型按 hook 加倍后的基础值增长
        if (rolled != RoomType.Shop)
        {
            Assert.Equal(0.03f + 0.06f, odds.ShopOdds, 5);
        }
        if (rolled != RoomType.Treasure)
        {
            Assert.Equal(0.02f + 0.04f, odds.TreasureOdds, 5);
        }
    }

    private sealed class NoMonsterHooks : IOddsHooks
    {
        public bool ShouldForcePotionReward(RoomType roomType) => false;

        public IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes)
            => roomTypes.Where(t => t != RoomType.Monster).ToHashSet();

        public float ModifyOddsIncreaseForUnrolledRoomType(RoomType roomType, float increase) => increase;
    }

    private sealed class DoubleIncreaseHooks : IOddsHooks
    {
        public bool ShouldForcePotionReward(RoomType roomType) => false;

        public IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes) => roomTypes;

        public float ModifyOddsIncreaseForUnrolledRoomType(RoomType roomType, float increase) => increase * 2f;
    }
}
