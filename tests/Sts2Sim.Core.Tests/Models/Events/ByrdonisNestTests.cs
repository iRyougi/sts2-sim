using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class ByrdonisNestTests : IDisposable
{
    public ByrdonisNestTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();


    [Fact]
    public void IsAllowed_RejectsPlayersWhoAlreadyHaveAnEventPet()
    {
        var runState = new RunState("byrdonis-is-allowed", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        ByrdonisNest nest = ModelDb.Event<ByrdonisNest>();

        Assert.True(nest.IsAllowed(runState));

        AddToDeck<ByrdonisEgg>(player);

        Assert.False(nest.IsAllowed(runState));
    }
    [Fact]
    public async Task Eat_GainsSevenMaxHpAndFinishes()
    {
        (_, Player player, ByrdonisNest ev) = Setup("byrdonis-eat");
        int maxHpBefore = player.Creature.MaxHp;

        await Choose(ev, "EAT");

        Assert.Equal(maxHpBefore + 7, player.Creature.MaxHp);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Take_AddsOwnedMutableEggAndIsHiddenAfterPlayerHasEventPet()
    {
        (_, Player player, ByrdonisNest ev) = Setup("byrdonis-take");
        Assert.Equal(new[] { "EAT", "TAKE" }, ev.CurrentOptions.Select(option => option.Key));

        await Choose(ev, "TAKE");

        ByrdonisEgg egg = Assert.Single(player.Deck.Cards.OfType<ByrdonisEgg>());
        Assert.Same(player, egg.Owner);
        Assert.False(egg.IsCanonical);
        Assert.True(player.HasEventPet());
        Assert.True(ev.IsFinished);

        (_, _, ByrdonisNest locked) = Setup(
            "byrdonis-locked",
            owner => AddToDeck<ByrdonisEgg>(owner));
        Assert.Equal(new[] { "EAT" }, locked.CurrentOptions.Select(option => option.Key));
    }

    private static (RunState RunState, Player Player, ByrdonisNest Event) Setup(
        string seed,
        Action<Player>? configure = null)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        configure?.Invoke(player);
        var ev = (ByrdonisNest)ModelDb.Event<ByrdonisNest>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static TCard AddToDeck<TCard>(Player player) where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Deck);
        return card;
    }

    private static Task Choose(ByrdonisNest ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
