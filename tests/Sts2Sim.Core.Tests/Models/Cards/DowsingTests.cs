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
using Sts2Sim.Core.Hooks;
using System.Text.Json;

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
        Assert.Equal("4", Assert.Single(VisibleContentProjection.ForCard(card).Values, v => v.Name == "Rooms").Value);
        Assert.Equal(0, Assert.Single(VisibleContentProjection.LegacyCard(card).NumericDeltas!, v => v.Name == "RoomsRemaining").Delta);
    }

    [Fact]
    public async Task FourthUnknownRoom_FiltersCombatAndTransformsBeforeEntry_WhileNestedRoomsAreIgnored()
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

        var originalTypes = new HashSet<RoomType>
            { RoomType.Monster, RoomType.Elite, RoomType.Event, RoomType.Treasure, RoomType.Shop };
        for (int roomNumber = 1; roomNumber <= 3; roomNumber++)
        {
            Assert.Equal(originalTypes, Hook.ModifyUnknownMapPointRoomTypes(runState, originalTypes));
            var rules = runState.CaptureVisibleUnknownMapPointRules(point);
            Assert.True(rules.Allows(RoomType.Monster));
            Assert.True(rules.Allows(RoomType.Elite));
            await EnterTopLevelEventAsync(runState);
            runState.RecordVisibleUnknownMapPointEntry(RoomType.Event, rules);
            Assert.Contains(dowsing, player.Deck.Cards);
            Assert.Equal((4 - roomNumber).ToString(), Assert.Single(
                VisibleContentProjection.ForCard(dowsing).Values, v => v.Name == "Rooms").Value);
            Assert.Equal(-roomNumber, Assert.Single(VisibleContentProjection.LegacyCard(dowsing).NumericDeltas!,
                v => v.Name == "RoomsRemaining").Delta);
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
        point.PointType = MapPointType.Monster;
        await EnterTopLevelEventAsync(runState);
        Assert.Contains(dowsing, player.Deck.Cards);
        point.PointType = MapPointType.Unknown;
        player.Deck.RemoveInternal(dowsing);
        await dowsing.BeforeRoomEntered(eventContainer);
        player.Deck.AddInternal(dowsing);
        Assert.Equal("1", Assert.Single(VisibleContentProjection.ForCard(dowsing).Values, v => v.Name == "Rooms").Value);

        Assert.Equal(new HashSet<RoomType> { RoomType.Event, RoomType.Treasure, RoomType.Shop },
            Hook.ModifyUnknownMapPointRoomTypes(runState, originalTypes));
        Assert.Equal(5, originalTypes.Count);
        runState.Odds.UnknownMapPoint.MonsterOdds = 1f;
        runState.Odds.UnknownMapPoint.EliteOdds = 1f;
        runState.Odds.UnknownMapPoint.TreasureOdds = 0f;
        runState.Odds.UnknownMapPoint.ShopOdds = 0f;
        var fourthRules = runState.CaptureVisibleUnknownMapPointRules(point);
        Assert.False(fourthRules.Allows(RoomType.Monster));
        Assert.False(fourthRules.Allows(RoomType.Elite));
        int beforeRoll = runState.Rng.UnknownMapPoint.Counter;
        RoomType fourthType = RoomFactory.ResolveRoomType(runState, point);
        Assert.Equal(RoomType.Event, fourthType);
        Assert.Equal(beforeRoll + 1, runState.Rng.UnknownMapPoint.Counter);
        var rngJsonOptions = new JsonSerializerOptions { IncludeFields = true };
        string runRng = JsonSerializer.Serialize(runState.Rng.ToSerializable(), rngJsonOptions);
        string playerRng = JsonSerializer.Serialize(player.PlayerRng.ToSerializable(), rngJsonOptions);
        var fourthRoom = new EntryProbeRoom(() =>
        {
            Assert.DoesNotContain(dowsing, player.Deck.Cards);
            Abundance abundance = Assert.Single(player.Deck.Cards.OfType<Abundance>());
            Assert.Same(player, abundance.Owner);
            Assert.Same(player.Deck, abundance.Pile);
        });
        runState.PushRoom(fourthRoom);
        await fourthRoom.Enter(runState);
        Assert.Same(fourthRoom, runState.PopCurrentRoom());
        runState.RecordVisibleUnknownMapPointEntry(fourthType, fourthRules);
        Assert.DoesNotContain(dowsing, player.Deck.Cards);
        Assert.Single(player.Deck.Cards.OfType<Abundance>());
        Assert.Equal(runRng, JsonSerializer.Serialize(runState.Rng.ToSerializable(), rngJsonOptions));
        Assert.Equal(playerRng, JsonSerializer.Serialize(player.PlayerRng.ToSerializable(), rngJsonOptions));
        RunState reseeded = runState.CloneReseeded(919);
        // Three allowed event visits increase combat odds; the fourth forbidden visit does not.
        Assert.Equal(0.4f, reseeded.Odds.UnknownMapPoint.MonsterOdds);
        Assert.Equal(-4f, reseeded.Odds.UnknownMapPoint.EliteOdds);
        Assert.Equal(1f, runState.CloneExact().Odds.UnknownMapPoint.MonsterOdds);
        runState.Odds.UnknownMapPoint.MonsterOdds = 0.91f;
        runState.Odds.UnknownMapPoint.EliteOdds = 0.73f;
        RunState perturbed = runState.CloneReseeded(919);
        Assert.Equal(reseeded.Odds.UnknownMapPoint.MonsterOdds, perturbed.Odds.UnknownMapPoint.MonsterOdds);
        Assert.Equal(reseeded.Odds.UnknownMapPoint.EliteOdds, perturbed.Odds.UnknownMapPoint.EliteOdds);
    }

    private static async Task EnterTopLevelEventAsync(RunState runState)
    {
        var room = new EventRoom(() =>
            (EventModel)ModelDb.Event<JungleMazeAdventure>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        Assert.Same(room, runState.PopCurrentRoom());
    }

    private sealed class EntryProbeRoom(Action inspect) : AbstractRoom
    {
        public override RoomType RoomType => RoomType.Event;
        public override ModelId? ModelId => null;
        public override Task EnterInternal(RunState? runState) { inspect(); return Task.CompletedTask; }
        public override Task Exit(RunState? runState) => Task.CompletedTask;
    }
}
