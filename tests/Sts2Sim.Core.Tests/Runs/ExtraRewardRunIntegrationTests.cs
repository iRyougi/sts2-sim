using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

public sealed class ExtraRewardRunIntegrationTests : IDisposable
{
    private const string OneFloorSeed = "extra-reward-integration";

    public ExtraRewardRunIntegrationTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RunEngine_OneFloorCombat_TakesAmethystAubergineExtraGold()
    {
        Player control = await RunEngineOneFloorAsync(OwnedExtraRewardRelic.None);
        Player relicOwner = await RunEngineOneFloorAsync(OwnedExtraRewardRelic.AmethystAubergine);

        Assert.Equal(15, relicOwner.Gold - control.Gold);
    }

    [Fact]
    public async Task RunEngine_OneFloorCombat_TakesPrayerWheelExtraCard()
    {
        Player control = await RunEngineOneFloorAsync(OwnedExtraRewardRelic.None);
        Player relicOwner = await RunEngineOneFloorAsync(OwnedExtraRewardRelic.PrayerWheel);

        Assert.Equal(1, relicOwner.Deck.Cards.Count - control.Deck.Cards.Count);
    }

    [Fact]
    public async Task RunDriver_OneFloorCombat_TakesAmethystAubergineExtraGoldThroughDefaultRewardPolicy()
    {
        Player control = await RunDriverOneFloorAsync(OwnedExtraRewardRelic.None);
        Player relicOwner = await RunDriverOneFloorAsync(OwnedExtraRewardRelic.AmethystAubergine);

        Assert.Equal(15, relicOwner.Gold - control.Gold);
    }

    [Fact]
    public async Task RunDriver_OneFloorCombat_TakesPrayerWheelExtraCardThroughDefaultRewardPolicy()
    {
        Player control = await RunDriverOneFloorAsync(OwnedExtraRewardRelic.None);
        Player relicOwner = await RunDriverOneFloorAsync(OwnedExtraRewardRelic.PrayerWheel);

        Assert.Equal(1, relicOwner.Deck.Cards.Count - control.Deck.Cards.Count);
    }

    private static async Task<Player> RunEngineOneFloorAsync(OwnedExtraRewardRelic ownedRelic)
    {
        (RunState runState, Player player) = await CreateOneFloorCombatRunAsync(ownedRelic);
        var engine = new RunEngine(runState, PickFirstByCoord);

        RunEngine.Result result = await engine.RunAsync(maxFloors: 1);

        Assert.Equal(1, result.FloorsVisited);
        Assert.False(runState.IsGameOver);
        return player;
    }

    private static async Task<Player> RunDriverOneFloorAsync(OwnedExtraRewardRelic ownedRelic)
    {
        (RunState runState, Player player) = await CreateOneFloorCombatRunAsync(ownedRelic);
        var driver = new RunDriver(runState, new DefaultRewardDecisionSource());

        RunDriver.Result result = await driver.RunAsync(maxFloors: 1);

        Assert.Equal(1, result.FloorsVisited);
        Assert.False(runState.IsGameOver);
        return player;
    }

    private static async Task<(RunState RunState, Player Player)> CreateOneFloorCombatRunAsync(
        OwnedExtraRewardRelic ownedRelic)
    {
        var runState = new RunState(OneFloorSeed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        player.Creature.SetMaxHpInternal(10_000m);
        player.Creature.HealInternal(10_000m);
        PickFirstByCoord(runState.Map.StartingMapPoint.Children).PointType = MapPointType.Monster;

        switch (ownedRelic)
        {
            case OwnedExtraRewardRelic.None:
                break;
            case OwnedExtraRewardRelic.AmethystAubergine:
                await RelicCmd.Obtain(ModelDb.Relic<AmethystAubergine>(), player);
                break;
            case OwnedExtraRewardRelic.PrayerWheel:
                await RelicCmd.Obtain(ModelDb.Relic<PrayerWheel>(), player);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ownedRelic), ownedRelic, null);
        }

        return (runState, player);
    }

    private static MapPoint PickFirstByCoord(IEnumerable<MapPoint> points) =>
        points.OrderBy(point => point.coord.col).First();

    private sealed class DefaultRewardDecisionSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(PickFirstByCoord(options));

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
        {
            Player player = state.Players[0];
            CardModel? playableCard = player.PlayerCombatState!.Hand.Cards
                .FirstOrDefault(card => card.CanPlay(out _));
            if (playableCard is null)
            {
                return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
            }

            Creature? target = playableCard.TargetType == TargetType.AnyEnemy
                ? state.HittableEnemies.FirstOrDefault()
                : null;
            return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(playableCard, target));
        }
    }

    private enum OwnedExtraRewardRelic
    {
        None,
        AmethystAubergine,
        PrayerWheel,
    }
}
