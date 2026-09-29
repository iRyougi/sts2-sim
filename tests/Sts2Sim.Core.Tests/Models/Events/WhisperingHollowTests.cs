using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class WhisperingHollowTests : IDisposable
{
    public WhisperingHollowTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();


    [Fact]
    public void IsAllowed_RequiresAtLeastFortyFourGold()
    {
        var runState = new RunState("whispering-is-allowed", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        WhisperingHollow hollow = ModelDb.Event<WhisperingHollow>();

        player.Gold = 44;
        Assert.True(hollow.IsAllowed(runState));

        player.Gold = 43;
        Assert.False(hollow.IsAllowed(runState));
    }
    [Fact]
    public async Task Gold_RollsInclusiveTwentySixToFortyFourCostsFromEventRng()
    {
        var costs = new HashSet<int>();
        for (int index = 0; index < 128; index++)
        {
            (_, Player player, WhisperingHollow ev) = Setup($"whispering-cost-{index}");
            int goldBefore = player.Gold;

            await Choose(ev, "GOLD");

            costs.Add(goldBefore - player.Gold);
        }

        Assert.All(costs, cost => Assert.InRange(cost, 26, 44));
        Assert.Contains(26, costs);
        Assert.Contains(44, costs);
    }

    [Fact]
    public async Task Gold_ClampsLowGoldAndOffersTwoAlreadyPopulatedPotionRewards()
    {
        (_, Player player, WhisperingHollow ev) = Setup("whispering-gold");
        player.Gold = 10;
        int rewardsCounterBefore = player.PlayerRng.Rewards.Counter;

        await Choose(ev, "GOLD");

        Assert.Equal(0, player.Gold);
        Assert.True(ev.TryDequeuePendingRewardOffer(out RewardsSet? rewards));
        Assert.NotNull(Assert.IsType<PotionReward>(rewards.Potion).Potion);
        PotionReward extra = Assert.IsType<PotionReward>(Assert.Single(rewards.ExtraRewards));
        Assert.NotNull(extra.Potion);
        Assert.Equal(rewardsCounterBefore + 4, player.PlayerRng.Rewards.Counter);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Hug_TransformsFirstCandidateWithEventRngAndUsesRunDamageHooks()
    {
        (_, Player player, WhisperingHollow ev) = Setup("whispering-hug");
        await RelicCmd.Obtain(ModelDb.Relic<TungstenRod>(), player);
        CardModel source = player.Deck.Cards.First(card => card.IsTransformable);
        int hpBefore = player.Creature.CurrentHp;
        int eventCounterBefore = ev.Rng.Counter;
        int rewardCounterBefore = player.PlayerRng.Rewards.Counter;

        await Choose(ev, "HUG");

        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, source));
        Assert.Equal(hpBefore - 8, player.Creature.CurrentHp);
        Assert.Equal(eventCounterBefore + 1, ev.Rng.Counter);
        Assert.Equal(rewardCounterBefore, player.PlayerRng.Rewards.Counter);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Hug_WithNoTransformableCard_StillDealsNineAndFinishes()
    {
        (_, Player player, WhisperingHollow ev) = Setup(
            "whispering-hug-empty",
            owner =>
            {
                foreach (CardModel card in owner.Deck.Cards.ToList())
                {
                    CardPileCmd.Remove(card);
                }
            });
        int hpBefore = player.Creature.CurrentHp;
        int eventCounterBefore = ev.Rng.Counter;

        await Choose(ev, "HUG");

        Assert.Empty(player.Deck.Cards);
        Assert.Equal(hpBefore - 9, player.Creature.CurrentHp);
        Assert.Equal(eventCounterBefore, ev.Rng.Counter);
        Assert.True(ev.IsFinished);
    }

    private static (RunState RunState, Player Player, WhisperingHollow Event) Setup(
        string seed,
        Action<Player>? configure = null)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        configure?.Invoke(player);
        var ev = (WhisperingHollow)ModelDb.Event<WhisperingHollow>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static Task Choose(WhisperingHollow ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
