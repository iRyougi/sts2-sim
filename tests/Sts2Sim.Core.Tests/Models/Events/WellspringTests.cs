using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class WellspringTests : IDisposable
{
    public WellspringTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Bottle_UniformlyDrawsOneNonEventPotionFromRewardsRng()
    {
        (_, Player player, Wellspring ev) = Setup("wellspring-bottle");
        // 候选来源与生产一致：Character.PotionPool ∪ SharedPotionPool（偏离 #301）。
        // 此前这里用 ModelDb.All<PotionModel>() 的扁平池，会把别的角色的专属药水算作候选。
        IReadOnlyList<PotionModel> eligible = PotionFactory.GetOutOfCombatPool(player);
        Type expectedType = player.PlayerRng.Rewards.CloneExact().NextItem(eligible)!.GetType();
        int counterBefore = player.PlayerRng.Rewards.Counter;

        await Choose(ev, "BOTTLE");

        Assert.True(ev.TryDequeuePendingRewardOffer(out RewardsSet? rewards));
        PotionReward reward = Assert.IsType<PotionReward>(rewards.Potion);
        PotionModel potion = Assert.IsAssignableFrom<PotionModel>(reward.Potion);
        Assert.IsType(expectedType, potion);
        Assert.NotSame(ModelDb.Get(potion.GetType()), potion);
        Assert.False(potion.IsCanonical);
        Assert.NotEqual(PotionRarity.Event, potion.Rarity);
        Assert.Equal(counterBefore + 1, player.PlayerRng.Rewards.Counter);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Bottle_WithNoEligiblePotion_FinishesWithoutOfferingAReward()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Where(type => !typeof(PotionModel).IsAssignableFrom(type)));
        (_, _, Wellspring ev) = Setup("wellspring-bottle-empty");

        await Choose(ev, "BOTTLE");

        Assert.False(ev.HasPendingRewardOffers);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Bathe_RemovesFirstRemovableCardAndAddsOneOwnedGuilty()
    {
        (_, Player player, Wellspring ev) = Setup("wellspring-bathe");
        CardModel selected = player.Deck.Cards.First(card => card.IsRemovable);
        CardModel[] retained = player.Deck.Cards.Where(card => !ReferenceEquals(card, selected)).ToArray();
        int countBefore = player.Deck.Cards.Count;

        await Choose(ev, "BATHE");

        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, selected));
        Assert.All(retained, card => Assert.Contains(card, player.Deck.Cards));
        Guilty guilty = Assert.Single(player.Deck.Cards.OfType<Guilty>());
        Assert.Same(player, guilty.Owner);
        Assert.False(guilty.IsCanonical);
        Assert.Equal(countBefore, player.Deck.Cards.Count);
        Assert.True(ev.IsFinished);
    }

    private static (RunState RunState, Player Player, Wellspring Event) Setup(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        var ev = (Wellspring)ModelDb.Event<Wellspring>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static Task Choose(Wellspring ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
