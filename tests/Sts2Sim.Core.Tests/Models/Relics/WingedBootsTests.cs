using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class WingedBootsTests : IDisposable
{
    public WingedBootsTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void IsAllowed_OnlyInSinglePlayerAndFreeTravelStartsEnabled()
    {
        RunState runState = CreateRun("winged-boots-allowed", out Player owner);
        WingedBoots canonical = ModelDb.Relic<WingedBoots>();

        Assert.True(canonical.IsAllowed(runState));

        Player foreign = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(foreign);
        Assert.False(canonical.IsAllowed(runState));
    }

    [Fact]
    public async Task MapTravelAndRoomEnter_CountThreeNonChildMovesThenDisableFreeTravel()
    {
        RunState runState = CreateRun("winged-boots-travel", out Player owner);
        await RelicCmd.Obtain(ModelDb.Relic<WingedBoots>(), owner);
        WingedBoots relic = Assert.Single(owner.Relics.OfType<WingedBoots>());
        MapPoint start = runState.Map.StartingMapPoint;
        Assert.True(runState.AddVisitedMapCoord(start.coord));
        MapPoint current = start.Children.OrderBy(point => point.coord).First();
        Assert.True(runState.AddVisitedMapCoord(current.coord));
        await new RestSiteRoom().Enter(runState);
        Assert.Equal(0, relic.TimesUsed);

        for (int use = 1; use <= 3; use++)
        {
            MapPoint[] travelable = MapTravel.GetTravelablePointsFrom(runState, current).ToArray();
            MapPoint chosen = Assert.Single(
                travelable.Where(point => !current.Children.Contains(point)).Take(1));
            Assert.True(runState.AddVisitedMapCoord(chosen.coord));

            await new RestSiteRoom().Enter(runState);

            Assert.Equal(use, relic.TimesUsed);
            current = chosen;
        }

        Assert.True(relic.IsUsedUp);
        Assert.Equal(
            current.Children.OrderBy(point => point.coord),
            MapTravel.GetTravelablePointsFrom(runState, current).OrderBy(point => point.coord));
    }

    [Fact]
    public async Task FirstEntryWithoutHistoryAndNormalChildMove_DoNotConsumeUse()
    {
        RunState runState = CreateRun("winged-boots-normal", out Player owner);
        await RelicCmd.Obtain(ModelDb.Relic<WingedBoots>(), owner);
        WingedBoots relic = Assert.Single(owner.Relics.OfType<WingedBoots>());

        await new RestSiteRoom().Enter(runState);
        Assert.Equal(0, relic.TimesUsed);

        MapPoint start = runState.Map.StartingMapPoint;
        Assert.True(runState.AddVisitedMapCoord(start.coord));
        MapPoint child = start.Children.OrderBy(point => point.coord).First();
        Assert.Contains(child, MapTravel.GetTravelablePointsFrom(runState, start));
        Assert.True(runState.AddVisitedMapCoord(child.coord));
        await new RestSiteRoom().Enter(runState);

        Assert.Equal(0, relic.TimesUsed);
        Assert.False(relic.IsUsedUp);
    }

    [Fact]
    public async Task ForcedCombatNestedInEvent_ConsumesOneUseForSingleFreeTravel()
    {
        RunState runState = CreateRun("winged-boots-nested-combat", out Player owner);
        owner.Creature.SetMaxHpInternal(10_000m);
        owner.Creature.HealInternal(10_000m);
        await RelicCmd.Obtain(ModelDb.Relic<WingedBoots>(), owner);
        WingedBoots relic = Assert.Single(owner.Relics.OfType<WingedBoots>());
        MapPoint start = runState.Map.StartingMapPoint;
        Assert.True(runState.AddVisitedMapCoord(start.coord));
        MapPoint current = start.Children.OrderBy(point => point.coord).First();
        Assert.True(runState.AddVisitedMapCoord(current.coord));
        MapPoint chosen = Assert.Single(
            MapTravel.GetTravelablePointsFrom(runState, current)
                .Where(point => !current.Children.Contains(point))
                .Take(1));
        Assert.True(runState.AddVisitedMapCoord(chosen.coord));
        var room = new EventRoom(() =>
            (EventModel)ModelDb.Event<DenseVegetation>().MutableClone());
        runState.PushRoom(room);

        await room.Enter(runState);
        Assert.Equal(1, relic.TimesUsed);
        await room.Event.ChooseOption(
            room.Event.CurrentOptions.Single(option => option.Key == "REST"));
        await new RunEngine(runState, points => points[0]).DriveEventAsync(room);

        Assert.Equal(1, relic.TimesUsed);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Null(owner.PlayerCombatState);
    }

    [Fact]
    public void Metadata_MatchesAncientThreeUseRelic()
    {
        WingedBoots relic = ModelDb.Relic<WingedBoots>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.Equal(0, relic.TimesUsed);
        Assert.False(relic.IsUsedUp);
    }

    private static RunState CreateRun(string seed, out Player owner)
    {
        var runState = new RunState(seed, new Overgrowth());
        owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        return runState;
    }
}