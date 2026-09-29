namespace Sts2Sim.Core.Tests.Odds;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")] // 创建 AbstractModel 子类实例(构造器读 ModelDb),须与注册表变更测试串行
public class HookOddsAdapterTests
{
    private sealed class FakeRunState(params AbstractModel[] listeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => listeners;

        public RunRngSet Rng { get; } = new("hook_odds_adapter_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    /// <summary>模拟"强制药水掉落"类遗物效果。</summary>
    private sealed class PotionMagnet : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => false;

        public override bool ShouldForcePotionReward(RoomType roomType) => true;
    }

    /// <summary>模拟"?房间不再出怪物战"类效果。</summary>
    private sealed class PeacefulCharm : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => false;

        public override IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes)
        {
            return roomTypes.Where(t => t != RoomType.Monster).ToHashSet();
        }
    }

    /// <summary>模拟"未 roll 中房型概率增长翻倍"类效果。</summary>
    private sealed class LuckyCoin : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => false;

        public override float ModifyOddsIncreaseForUnrolledRoomType(RoomType roomType, float oddsIncrease)
        {
            return oddsIncrease * 2f;
        }
    }

    /// <summary>把 roomTypes 清空的恶性 hook——用于锁定游戏固有崩溃面。</summary>
    private sealed class VoidCharm : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => false;

        public override IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes)
        {
            return new HashSet<RoomType>();
        }
    }

    [Fact]
    public void NoListeners_BehavesLikeNullOddsHooks()
    {
        var adapter = new HookOddsAdapter(new FakeRunState());
        var withAdapter = new PotionRewardOdds(new Rng(88u), adapter);
        var withNull = new PotionRewardOdds(new Rng(88u), NullOddsHooks.Instance);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(withNull.Roll(RoomType.Monster), withAdapter.Roll(RoomType.Monster));
        }
        Assert.Equal(withNull.CurrentValue, withAdapter.CurrentValue);
    }

    [Fact]
    public void PotionMagnet_ForcesPotionRoll_ThroughFullHookChain()
    {
        var adapter = new HookOddsAdapter(new FakeRunState(new PotionMagnet()));
        var rng = new Rng(1u);
        var odds = new PotionRewardOdds(rng, adapter);
        float before = odds.CurrentValue;
        Assert.True(odds.Roll(RoomType.Monster));
        Assert.Equal(before, odds.CurrentValue);
        Assert.Equal(0, rng.Counter);
    }

    [Fact]
    public void PeacefulCharm_ExcludesMonsterFromUnknownRoomRolls()
    {
        var adapter = new HookOddsAdapter(new FakeRunState(new PeacefulCharm()));
        var odds = new UnknownMapPointOdds(new Rng(555u), adapter);
        for (int i = 0; i < 50; i++)
        {
            Assert.NotEqual(RoomType.Monster, odds.Roll(Array.Empty<RoomType>()));
        }
    }

    [Fact]
    public void LuckyCoin_DoublesOddsGrowthForUnrolledTypes()
    {
        var adapter = new HookOddsAdapter(new FakeRunState(new LuckyCoin()));
        var odds = new UnknownMapPointOdds(new Rng(555u), adapter);
        RoomType rolled = odds.Roll(Array.Empty<RoomType>());
        // 未 roll 中的房型:+= 基础值 × 2(Monster 基础 0.1 → 0.1 + 0.2 = 0.3)
        if (rolled != RoomType.Monster)
        {
            Assert.Equal(0.3f, odds.MonsterOdds, 5);
        }
        if (rolled != RoomType.Shop)
        {
            Assert.Equal(0.09f, odds.ShopOdds, 5); // 0.03 + 0.06
        }
    }

    [Fact]
    public void EmptyRoomTypeSet_Throws_KnownGameCrashSurface()
    {
        // Plan 01 交接的已知前置约束:hook 把 roomTypes 清空时,
        // UnknownMapPointOdds.Roll 内 roomTypes.Order().First() 抛 InvalidOperationException。
        // 游戏原版同样会崩——这是行为一致性的一部分,不做"修复";
        // Plan 06 铺内容时任何实现 ModifyUnknownMapPointRoomTypes 的实体不得清空集合。
        var adapter = new HookOddsAdapter(new FakeRunState(new VoidCharm()));
        var odds = new UnknownMapPointOdds(new Rng(555u), adapter);
        Assert.Throws<InvalidOperationException>(() => odds.Roll(Array.Empty<RoomType>()));
    }
}
