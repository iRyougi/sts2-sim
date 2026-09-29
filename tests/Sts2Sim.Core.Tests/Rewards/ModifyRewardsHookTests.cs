using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rewards;

[Collection("ModelDb")]
public sealed class ModifyRewardsHookTests : IDisposable
{
    public ModifyRewardsHookTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(DivineRight),
            typeof(Begone), typeof(CosmicIndifference), typeof(CollisionCourse),
            typeof(Automation), typeof(ChildOfTheStars), typeof(Charge),
            typeof(SevenStars), typeof(BigBang), typeof(BundleOfJoy),
            typeof(AmethystAubergine), typeof(PrayerWheel), typeof(WhiteStar), typeof(Circlet),
            typeof(RoyaltiesPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RewardsSet_GenerateFor_IncludesPopulatedGoldRewardFromAmethystAubergine()
    {
        (Player player, RunState runState) = CreatePlayer("modify-rewards-aubergine");
        await RelicCmd.Obtain(ModelDb.Relic<AmethystAubergine>(), player);

        RewardsSet rewards = RewardsSet.GenerateFor(player, RoomType.Monster, runState);

        GoldReward extraGold = Assert.IsType<GoldReward>(Assert.Single(rewards.ExtraRewards));
        Assert.Equal(15, extraGold.Amount);
        // The native post-population ModifyRewards hook adds an already populated
        // fixed reward, so it must not advance Rewards beyond the same run without it.
        (Player baselinePlayer, RunState baselineRun) = CreatePlayer("modify-rewards-aubergine");
        RewardsSet.GenerateFor(baselinePlayer, RoomType.Monster, baselineRun);
        Assert.Equal(baselinePlayer.PlayerRng.Rewards.Counter, player.PlayerRng.Rewards.Counter);
        int goldBefore = player.Gold;
        await rewards.Gold.Take();
        var next = Assert.IsType<RewardDecisionClassification.Automatic>(
            RewardDecisionClassifier.Classify(rewards));
        var extraDecision = Assert.IsType<RewardDecision.ResolveExtra>(next.Decision);
        Assert.Same(extraGold, extraDecision.Reward);
        await extraGold.Take();
        Assert.Equal(goldBefore + rewards.Gold.Amount + 15, player.Gold);
        Assert.False(rewards.Card.IsResolved);
    }

    [Fact]
    public async Task RewardsSet_GenerateFor_IncludesPopulatedRegularCardRewardFromPrayerWheelOnlyForMonsterRooms()
    {
        (Player player, RunState runState) = CreatePlayer("modify-rewards-prayer-wheel");
        await RelicCmd.Obtain(ModelDb.Relic<PrayerWheel>(), player);

        RewardsSet monsterRewards = RewardsSet.GenerateFor(player, RoomType.Monster, runState);
        RewardsSet eliteRewards = RewardsSet.GenerateFor(player, RoomType.Elite, runState);

        CardReward extraCards = Assert.IsType<CardReward>(Assert.Single(monsterRewards.ExtraRewards));
        Assert.Equal(3, extraCards.Options.Count);
        Assert.Empty(eliteRewards.ExtraRewards);
    }

    [Fact]
    public async Task RewardsSet_GenerateFor_IncludesPopulatedBossCardRewardFromWhiteStarOnlyForEliteRooms()
    {
        (Player player, RunState runState) = CreatePlayer("modify-rewards-white-star");
        await RelicCmd.Obtain(ModelDb.Relic<WhiteStar>(), player);

        RewardsSet eliteRewards = RewardsSet.GenerateFor(player, RoomType.Elite, runState);
        RewardsSet monsterRewards = RewardsSet.GenerateFor(player, RoomType.Monster, runState);

        CardReward extraCards = Assert.IsType<CardReward>(Assert.Single(eliteRewards.ExtraRewards));
        Assert.Equal(3, extraCards.Options.Count);
        Assert.Empty(monsterRewards.ExtraRewards);
    }

    [Fact]
    public async Task RewardsSet_GenerateFor_DispatchesToRoyaltiesPowerInActiveCombat()
    {
        (Player player, RunState runState) = CreatePlayer("modify-rewards-royalties");
        var combatState = new CombatState(runState);
        player.ResetCombatState();
        combatState.AddPlayerCreature(player.Creature);
        await PowerCmd.Apply<RoyaltiesPower>(combatState, player.Creature, 30m, player.Creature, null);

        RewardsSet rewards = RewardsSet.GenerateFor(player, RoomType.Monster, runState);

        GoldReward extraGold = Assert.IsType<GoldReward>(Assert.Single(rewards.ExtraRewards));
        Assert.Equal(30, extraGold.Amount);
    }

    private static (Player Player, RunState RunState) CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (player, runState);
    }
}
