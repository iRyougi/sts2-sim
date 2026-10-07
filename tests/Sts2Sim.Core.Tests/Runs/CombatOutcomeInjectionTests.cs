using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public sealed class CombatOutcomeInjectionTests
{
    [Theory]
    [InlineData("died")]
    [InlineData("survived")]
    [InlineData("live-rejected")]
    public async Task Injected_outcome_applies_only_in_imagination(string row)
    {
        const string seed = "M2DINJECTION01";
        RunReplayDemo.EnsureModelsRegistered();
        var original = new RunState(seed, 10);
        original.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), original));
        original.AddVisitedMapCoord(original.Map.StartingMapPoint.coord);
        RunState run = row == "live-rejected" ? original.CloneExact() : original.CloneReseeded(42);
        MapPoint target = run.Map.StartingMapPoint.Children.OrderBy(point => point.coord.col).First();
        Assert.True(target.PointType == MapPointType.Monster, $"seed={seed}, coord={target.coord}");
        var source = new InjectionSource(row == "died");
        var driver = new RunDriver(run, source);
        if (row == "live-rejected")
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => driver.RunOneCombatFromTransplantAsync(target));
            return;
        }
        CombatRoom room = await driver.RunOneCombatFromTransplantAsync(target);
        Assert.True(run.IsGameOver == (row == "died"), $"seed={seed}, row={row}");
        Assert.True(run.Players[0].Creature.IsDead == (row == "died"), $"seed={seed}, row={row}");
        if (row == "died") Assert.Empty(room.GeneratedRewards);
        else
        {
            Assert.True(room.Won, $"seed={seed}");
            Assert.NotEmpty(room.GeneratedRewards);
            Assert.True(run.Players[0].Creature.CurrentHp == 7, $"seed={seed}, hp={run.Players[0].Creature.CurrentHp}");
            Assert.True(run.Players[0].PlayerCombatState!.TurnNumber == source.StartTurn, $"seed={seed}");
            // Approved missing invariant: an opening victory must keep its real result,
            // with no sampling, HP overwrite, or second conclusion.
            RunState opening = original.CloneReseeded(43);
            Player owner = opening.Players[0];
            var hourglass = (MercuryHourglass)ModelDb.Relic<MercuryHourglass>().MutableClone();
            hourglass.AssignOwner(owner);
            owner.AddRelicInternal(hourglass);
            var rejectedDeath = new InjectionSource(died: true);
            MapPoint openingTarget = opening.Map.GetPoint(target.coord)!;
            int hpBeforeOpening = owner.Creature.CurrentHp;
            CombatRoom openingRoom = await new RunDriver(opening, rejectedDeath)
                .RunOneCombatFromTransplantAsync(openingTarget, (_, state) =>
                {
                    foreach (var enemy in state.Enemies) enemy.SetCurrentHpInternal(3);
                });
            Assert.True(openingRoom.Won && !opening.IsGameOver && openingRoom.GeneratedRewards.Count > 0 &&
                owner.Creature.CurrentHp == hpBeforeOpening && rejectedDeath.Calls == 0,
                $"seed={seed}, opening victory must settle without injection");
        }
    }

    private sealed class InjectionSource(bool died) : IRunDecisionSource, ICombatOutcomeOverride
    {
        public int StartTurn { get; private set; }
        public int Calls { get; private set; }
        public InjectedCombatOutcome? TryInject(RunState run, CombatRoom room)
        {
            Calls++;
            StartTurn = run.Players[0].PlayerCombatState!.TurnNumber;
            return new InjectedCombatOutcome(died, 7);
        }
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => Task.FromResult(options[0]);
        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            throw new InvalidOperationException("Injected combat must not execute a turn.");
    }
}
