using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class LostCofferTests : IDisposable
{
    public LostCofferTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task ArchivedXWDT4877APQF_NeowOffersRecordedCardsAndPotion()
    {
        // F1 archive identities, independent of the rarity implementation.
        var run = new RunState("XWDT4877APQF", ascensionLevel: 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
        var room = (EventRoom)RoomFactory.CreateAncientEventRoom(run);
        run.PushRoom(room);
        await room.Enter(run);
        await room.Event.ChooseOption(room.Event.CurrentOptions.Single(o => o.Key == "LostCoffer"));
        Assert.True(room.Event.TryDequeuePendingRewardOffer(out var rewards));
        Assert.Equal(new[] { "LEG_SWEEP", "PINPOINT", "PREDATOR" }, rewards!.Card.Options.Select(c => c.Id.Entry));
        Assert.Equal("STRENGTH_POTION", rewards.Potion!.Potion!.Id.Entry);
        await room.Exit(run);
        run.PopCurrentRoom();
    }
    [Fact]
    public async Task AfterObtained_OffersOnePopulatedCardChoiceAndPotionReward()
    {
        (RunState runState, Player player) = CreateRun("lost-coffer");
        EventRoom room = await EnterEventRoom(runState);
        int deckCountBefore = player.Deck.Cards.Count;

        await RelicCmd.Obtain(ModelDb.Relic<LostCoffer>(), player);

        Assert.True(room.Event.TryDequeuePendingRewardOffer(out RewardsSet? rewards));
        Assert.NotNull(rewards);
        Assert.False(room.Event.TryDequeuePendingRewardOffer(out _));
        Assert.Equal(3, rewards.Card.Options.Count);
        Assert.Equal(3, rewards.Card.Options.Select(card => card.Id).Distinct().Count());
        Assert.All(rewards.Card.Options, card =>
        {
            Assert.False(card.IsCanonical);
            Assert.Same(player, card.Owner);
            Assert.False(card.IsColorless);
            Assert.False(card.IsMultiplayerOnly);
        });
        Assert.NotNull(rewards.Potion);
        Assert.NotNull(rewards.Potion.Potion);

        CardModel chosen = rewards.Card.Options[0];
        await rewards.Card.SelectOption(chosen);
        await rewards.Potion.Take();

        CardModel added = Assert.Single(player.Deck.Cards.Skip(deckCountBefore));
        Assert.Equal(chosen.Id, added.Id);
        Assert.Same(player, added.Owner);
        PotionModel ownedPotion = Assert.IsAssignableFrom<PotionModel>(
            Assert.Single(player.PotionSlots, potion => potion is not null));
        Assert.Same(player, ownedPotion.Owner);
    }

    [Fact]
    public void Metadata_MatchesAncientUponPickupBehavior()
    {
        LostCoffer relic = ModelDb.Relic<LostCoffer>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static async Task<EventRoom> EnterEventRoom(RunState runState)
    {
        var room = new EventRoom(() =>
            (EventModel)ModelDb.Event<JungleMazeAdventure>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        return room;
    }
}
