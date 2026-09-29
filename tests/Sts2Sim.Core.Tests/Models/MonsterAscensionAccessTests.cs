namespace Sts2Sim.Core.Tests.Models;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class MonsterAscensionAccessTests : IDisposable
{
    public MonsterAscensionAccessTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(AscensionProbeMonster) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task PerformMove_ReadsAscensionThroughCombatStateRunStateInterface()
    {
        int nonAscensionValue = await PerformProbeMove(0);
        int toughEnemiesValue = await PerformProbeMove((int)AscensionLevel.ToughEnemies);

        Assert.Equal(7, nonAscensionValue);
        Assert.Equal(13, toughEnemiesValue);
    }

    private static async Task<int> PerformProbeMove(int ascensionLevel)
    {
        IRunState runState = new RunState("monster-ascension-access", new Overgrowth(), ascensionLevel);
        var combatState = new CombatState(runState);
        var monster = (AscensionProbeMonster)ModelDb.Monster<AscensionProbeMonster>().MutableClone();

        combatState.AddMonster(monster, CombatSide.Enemy);
        monster.SetUpForCombat();
        monster.RollMove(Array.Empty<Creature>());

        await monster.PerformMove();

        Assert.Same(runState, monster.Creature.CombatState!.RunState);
        return monster.ObservedValue;
    }

    private sealed class AscensionProbeMonster : MonsterModel
    {
        public override int MinInitialHp => 10;

        public override int MaxInitialHp => 10;

        public int ObservedValue { get; private set; }

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            var probe = new MoveState(
                "ASCENSION_PROBE",
                _ =>
                {
                    ObservedValue = Creature.CombatState!.RunState.Ascension.GetValueIfAscension(
                        AscensionLevel.ToughEnemies,
                        ascensionValue: 13,
                        fallbackValue: 7);
                    return Task.CompletedTask;
                });
            probe.FollowUpState = probe;
            return new MonsterMoveStateMachine(new[] { probe }, probe);
        }
    }
}
