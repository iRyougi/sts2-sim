using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public sealed class RestSiteRunDriverTests : IDisposable
{
    public RestSiteRunDriverTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight),
            typeof(ByrdonisEgg), typeof(ByrdSwoop), typeof(Byrdpip),
            typeof(MeatCleaver),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RunEngine_DefaultRestSitePolicy_SmithsFirstUpgradableCard()
    {
        RunState runState = NewRunState("rest-engine");
        MakeFirstReachableRoomARestSite(runState);
        CardModel expected = runState.Players[0].Deck.Cards.First(card => card.IsUpgradable);
        var engine = new RunEngine(runState, options => options.OrderBy(point => point.coord.col).First());

        await engine.RunAsync(maxFloors: 1);

        Assert.True(expected.IsUpgraded);
        Assert.Equal(1, runState.Players[0].Deck.Cards.Count(card => card.IsUpgraded));
    }

    [Fact]
    public async Task RunDriver_DefaultRestSitePolicy_SmithsFirstUpgradableCard()
    {
        RunState runState = NewRunState("rest-driver");
        MakeFirstReachableRoomARestSite(runState);
        CardModel expected = runState.Players[0].Deck.Cards.First(card => card.IsUpgradable);
        var driver = new RunDriver(runState, new FirstChoiceSource());

        await driver.RunAsync(maxFloors: 1);

        Assert.True(expected.IsUpgraded);
        Assert.Equal(1, runState.Players[0].Deck.Cards.Count(card => card.IsUpgraded));
    }

    [Fact]
    public async Task RunEngine_DefaultRestSitePolicy_HatchesWhenEggExists()
    {
        RunState runState = NewRunState("rest-engine-hatch");
        MakeFirstReachableRoomARestSite(runState);
        AddDeckEgg(runState.Players[0]);
        var engine = new RunEngine(runState, options => options.OrderBy(point => point.coord.col).First());

        await engine.RunAsync(maxFloors: 1);

        Assert.Single(runState.Players[0].Relics.OfType<Byrdpip>());
        Assert.Empty(runState.Players[0].Deck.Cards.OfType<ByrdonisEgg>());
        Assert.Single(runState.Players[0].Deck.Cards.OfType<ByrdSwoop>());
    }

    [Fact]
    public async Task RunDriver_DefaultRestSitePolicy_HatchesWhenEggExists()
    {
        RunState runState = NewRunState("rest-driver-hatch");
        MakeFirstReachableRoomARestSite(runState);
        AddDeckEgg(runState.Players[0]);
        var driver = new RunDriver(runState, new FirstChoiceSource());

        await driver.RunAsync(maxFloors: 1);

        Assert.Single(runState.Players[0].Relics.OfType<Byrdpip>());
        Assert.Empty(runState.Players[0].Deck.Cards.OfType<ByrdonisEgg>());
        Assert.Single(runState.Players[0].Deck.Cards.OfType<ByrdSwoop>());
    }

    [Fact]
    public async Task DefaultDecisionSource_UsesHatchThenPreservesSmithAndHealFallbacks()
    {
        IRunDecisionSource source = new FirstChoiceSource();
        RunState hatchRun = NewRunState("rest-source-hatch");
        AddDeckEgg(hatchRun.Players[0]);
        Assert.IsType<RestSiteDecision.Hatch>(
            await source.ChooseRestSiteActionAsync(
                hatchRun.Players[0],
                new Sts2Sim.Core.Rooms.RestSiteRoom().GetAvailableDecisions(hatchRun, hatchRun.Players[0])));

        RunState smithRun = NewRunState("rest-source-smith");
        RestSiteDecision.Smith smith = Assert.IsType<RestSiteDecision.Smith>(
            await source.ChooseRestSiteActionAsync(
                smithRun.Players[0],
                new Sts2Sim.Core.Rooms.RestSiteRoom().GetAvailableDecisions(smithRun, smithRun.Players[0])));
        Assert.Same(smithRun.Players[0].Deck.Cards.First(card => card.IsUpgradable), smith.Card);

        RunState healRun = NewRunState("rest-source-heal");
        foreach (CardModel card in healRun.Players[0].Deck.Cards.Where(card => card.IsUpgradable))
        {
            card.Upgrade();
        }
        Assert.IsType<RestSiteDecision.Heal>(
            await source.ChooseRestSiteActionAsync(
                healRun.Players[0],
                new Sts2Sim.Core.Rooms.RestSiteRoom().GetAvailableDecisions(healRun, healRun.Players[0])));
    }

    [Fact]
    public async Task RunEngine_DefaultRestSitePolicy_UsesOfferedCookForMeatCleaver()
    {
        RunState runState = NewRunState("rest-engine-cook");
        MakeFirstReachableRoomARestSite(runState);
        Player player = runState.Players[0];
        await RelicCmd.Obtain(ModelDb.Relic<MeatCleaver>(), player);
        int maxHpBefore = player.Creature.MaxHp;
        int deckCountBefore = player.Deck.Cards.Count;
        var engine = new RunEngine(runState, options => options.OrderBy(point => point.coord.col).First());

        await engine.RunAsync(maxFloors: 1);

        Assert.Equal(maxHpBefore + 5, player.Creature.MaxHp);
        Assert.Equal(deckCountBefore - 2, player.Deck.Cards.Count);
    }

    [Fact]
    public async Task RunDriver_ReceivesAuthoritativeCookSnapshotAndRejectsUnavailableChoice()
    {
        RunState runState = NewRunState("rest-driver-cook-snapshot");
        MakeFirstReachableRoomARestSite(runState);
        Player player = runState.Players[0];
        await RelicCmd.Obtain(ModelDb.Relic<MeatCleaver>(), player);
        int maxHpBefore = player.Creature.MaxHp;
        var source = new CookFromSnapshotSource();
        var driver = new RunDriver(runState, source);

        await driver.RunAsync(maxFloors: 1);

        Assert.NotNull(source.ReceivedCandidates);
        Assert.Contains(source.ReceivedCandidates!, decision => decision is RestSiteDecision.Cook);
        Assert.Equal(maxHpBefore + 5, player.Creature.MaxHp);

        RunState foreignRun = NewRunState("rest-driver-foreign-cook");
        MakeFirstReachableRoomARestSite(foreignRun);
        var foreignDriver = new RunDriver(foreignRun, new ForeignCookSource());
        await Assert.ThrowsAsync<InvalidOperationException>(() => foreignDriver.RunAsync(maxFloors: 1));
    }

    private static void AddDeckEgg(Player player)
    {
        var egg = (ByrdonisEgg)ModelDb.Card<ByrdonisEgg>().MutableClone();
        egg.AssignOwner(player);
        player.Deck.AddInternal(egg);
    }

    private static RunState NewRunState(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return runState;
    }

    private static void MakeFirstReachableRoomARestSite(RunState runState)
    {
        runState.Map.StartingMapPoint.Children
            .OrderBy(point => point.coord.col)
            .First()
            .PointType = MapPointType.RestSite;
    }

    private sealed class FirstChoiceSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(options.OrderBy(point => point.coord.col).First());

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
    }

    private sealed class CookFromSnapshotSource : IRunDecisionSource
    {
        public IReadOnlyList<RestSiteDecision>? ReceivedCandidates { get; private set; }

        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(options.OrderBy(point => point.coord.col).First());

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

        public Task<RestSiteDecision> ChooseRestSiteActionAsync(
            Player player,
            IReadOnlyList<RestSiteDecision> candidates)
        {
            ReceivedCandidates = candidates;
            return Task.FromResult<RestSiteDecision>(
                candidates.OfType<RestSiteDecision.Cook>().Single());
        }
    }

    private sealed class ForeignCookSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(options.OrderBy(point => point.coord.col).First());

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

        public Task<RestSiteDecision> ChooseRestSiteActionAsync(
            Player player,
            IReadOnlyList<RestSiteDecision> candidates) =>
            Task.FromResult<RestSiteDecision>(new RestSiteDecision.Cook());
    }
}

