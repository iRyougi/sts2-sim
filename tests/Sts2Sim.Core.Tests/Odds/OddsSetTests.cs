namespace Sts2Sim.Core.Tests.Odds;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

public class OddsSetTests
{
    [Fact]
    public void PlayerOddsSet_UsesRewardsRngForBothOdds()
    {
        var rngSet = new PlayerRngSet(42u);
        var set = new PlayerOddsSet(rngSet, new AscensionManager(0), NullOddsHooks.Instance);
        // 两个 Odds 共用 Rewards 流（与游戏一致）：各 roll 一次 → Rewards.Counter = 2
        set.CardRarity.Roll(CardRarityOddsType.RegularEncounter);
        set.PotionReward.Roll(RoomType.Monster);
        Assert.Equal(2, rngSet.Rewards.Counter);
    }

    [Fact]
    public void PlayerOddsSet_SerializeRestore_RoundTrips()
    {
        var rngSet = new PlayerRngSet(42u);
        var set = new PlayerOddsSet(rngSet, new AscensionManager(0), NullOddsHooks.Instance);
        set.CardRarity.Roll(CardRarityOddsType.RegularEncounter);
        set.PotionReward.Roll(RoomType.Monster);

        var save = set.ToSerializable();
        var restored = PlayerOddsSet.FromSerializable(save, rngSet, new AscensionManager(0), NullOddsHooks.Instance);
        Assert.Equal(set.CardRarity.CurrentValue, restored.CardRarity.CurrentValue);
        Assert.Equal(set.PotionReward.CurrentValue, restored.PotionReward.CurrentValue);
    }

    [Fact]
    public void RunOddsSet_SerializeRestore_RoundTrips()
    {
        var rng = new Rng(9u);
        var set = new RunOddsSet(rng, NullOddsHooks.Instance);
        set.UnknownMapPoint.Roll(Array.Empty<RoomType>());

        var save = set.ToSerializable();
        var restored = RunOddsSet.FromSerializable(save, rng, NullOddsHooks.Instance);
        Assert.Equal(set.UnknownMapPoint.MonsterOdds, restored.UnknownMapPoint.MonsterOdds);
        Assert.Equal(set.UnknownMapPoint.EliteOdds, restored.UnknownMapPoint.EliteOdds);
        Assert.Equal(set.UnknownMapPoint.TreasureOdds, restored.UnknownMapPoint.TreasureOdds);
        Assert.Equal(set.UnknownMapPoint.ShopOdds, restored.UnknownMapPoint.ShopOdds);
    }
}
