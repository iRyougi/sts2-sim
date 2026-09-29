using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class DowsingTests : IDisposable
{
    public DowsingTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Metadata_MatchesSource()
    {
        var card = (Dowsing)ModelDb.Card<Dowsing>().MutableClone();

        Assert.Equal(-1, card.EnergyCost);
        Assert.Equal(CardType.Quest, card.Type);
        Assert.Equal(CardRarity.Quest, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(new[] { CardKeyword.Unplayable }, card.Keywords);
        Assert.Equal(0, card.MaxUpgradeLevel);
    }

    [Fact]
    public async Task FifthUnknownCombat_EntersWithAbundance_WhileNestedCombatIsIgnored()
    {
        var runState = new RunState("task11-dowsing", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        MapPoint point = runState.Map.StartingMapPoint;
        point.PointType = MapPointType.Unknown;
        Assert.True(runState.AddVisitedMapCoord(point.coord));
        var dowsing = (Dowsing)ModelDb.Card<Dowsing>().MutableClone();
        dowsing.AssignOwner(player);
        player.Deck.AddInternal(dowsing);

        for (int roomNumber = 1; roomNumber <= 4; roomNumber++)
        {
            await EnterTopLevelEventAsync(runState);
            Assert.Contains(dowsing, player.Deck.Cards);
        }

        var eventContainer = new EventRoom(() =>
            (EventModel)ModelDb.Event<JungleMazeAdventure>().MutableClone());
        runState.PushRoom(eventContainer);
        var nestedCombat = new CombatRoom(() =>
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        runState.PushRoom(nestedCombat);
        await nestedCombat.Enter(runState);
        await nestedCombat.Exit(runState);
        Assert.Same(nestedCombat, runState.PopCurrentRoom());
        Assert.Same(eventContainer, runState.PopCurrentRoom());
        Assert.Contains(dowsing, player.Deck.Cards);

        var fifthCombat = new CombatRoom(() =>
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        runState.PushRoom(fifthCombat);
        await fifthCombat.Enter(runState);

        Assert.DoesNotContain(dowsing, player.Deck.Cards);
        Assert.Single(player.Deck.Cards.OfType<Abundance>());
        Assert.Single(player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).OfType<Abundance>());
    }

    private static async Task EnterTopLevelEventAsync(RunState runState)
    {
        var room = new EventRoom(() =>
            (EventModel)ModelDb.Event<JungleMazeAdventure>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        Assert.Same(room, runState.PopCurrentRoom());
    }
}
