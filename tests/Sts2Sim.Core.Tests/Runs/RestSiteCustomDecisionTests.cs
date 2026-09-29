using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public sealed class RestSiteCustomDecisionTests : IDisposable
{
    public RestSiteCustomDecisionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RunDriver_HealDecision_HealsWithoutUpgrading()
    {
        var runState = new RunState("rest-driver-heal", new Overgrowth());
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.Creature.LoseHpInternal(player.Creature.MaxHp - 1, default(ValueProp));
        int hpBefore = player.Creature.CurrentHp;
        runState.Map.StartingMapPoint.Children.OrderBy(point => point.coord.col).First().PointType =
            MapPointType.RestSite;
        var driver = new RunDriver(runState, new HealSource());

        await driver.RunAsync(maxFloors: 1);

        Assert.True(player.Creature.CurrentHp > hpBefore);
        Assert.DoesNotContain(player.Deck.Cards, card => card.IsUpgraded);
    }

    private sealed class HealSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(options.OrderBy(point => point.coord.col).First());

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

        public Task<RestSiteDecision> ChooseRestSiteActionAsync(
            Player player,
            IReadOnlyList<RestSiteDecision> candidates) =>
            Task.FromResult<RestSiteDecision>(new RestSiteDecision.Heal());
    }
}
