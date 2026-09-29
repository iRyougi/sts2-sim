using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

public sealed class SharedEventMechanismTests : IDisposable
{
    public SharedEventMechanismTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }
    public void Dispose() => ModelDb.ResetForTests();
    [Fact]
    public async Task UniformEventRewardsUseOverrideForCardAndUpgradeRolls()
    {
        var run = new RunState("shared-reward-options", new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var rng = new Rng(24);
        int before = player.PlayerRng.Rewards.Counter;
        var cards = CardFactory.CreateForReward(player, 3,
            new CardCreationOptions([player.Character.CardPool], CardCreationSource.Other,
                CardRarityOddsType.Uniform, c => c.Rarity == CardRarity.Common).WithRngOverride(rng));
        Assert.Equal(3, cards.Count);
        Assert.Equal(6, rng.Counter);
        Assert.Equal(before, player.PlayerRng.Rewards.Counter);
        var noUpgrades = CardFactory.CreateForReward(player, 3,
            CardCreationOptions.ForNonCombatWithUniformOdds([player.Character.CardPool],
                c => c.Rarity == CardRarity.Common).WithRngOverride(rng));
        Assert.Equal(9, rng.Counter);
        Assert.All(noUpgrades, c => Assert.False(c.IsUpgraded));
        await RelicCmd.Obtain(ModelDb.Relic<BingBong>(), player);
        int deckBefore = player.Deck.Cards.Count;
        var reward = new CardReward(player, noUpgrades);
        reward.Populate(run);
        await reward.SelectOption(reward.Options[0]);
        Assert.Equal(deckBefore + 2, player.Deck.Cards.Count);
    }

    [Fact]
    public async Task PotionLockRejectsPlayerActionsButAllowsTheEventTrade()
    {
        var run = new RunState("shared-potion-lock", new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var potion = player.AddPotionInternal(ModelDb.Potion<FruitJuice>());
        player.CanUseOrRemovePotions = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.Use(potion, player, player.Creature));
        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.Discard(potion));
        Assert.Contains(potion, player.PotionSlots);
        await PotionCmd.DiscardForEvent(potion);
        Assert.DoesNotContain(potion, player.PotionSlots);
    }

    [Fact]
    public void TradeEligibilityExcludesPickupStarterAndEventRelics()
    {
        Assert.True(ModelDb.Relic<Anchor>().IsTradable);
        Assert.False(ModelDb.Relic<Strawberry>().IsTradable);
        Assert.False(ModelDb.Relic<RingOfTheSnake>().IsTradable);
        Assert.False(ModelDb.Relic<ChosenCheese>().IsTradable);
    }
}
