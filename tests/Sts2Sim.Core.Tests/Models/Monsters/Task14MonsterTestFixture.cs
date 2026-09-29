namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;

internal sealed class Task14MonsterTestFixture : IDisposable
{
    public Task14MonsterTestFixture(Type monsterType)
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            monsterType,
            typeof(ArtifactPower),
            typeof(FrailPower),
            typeof(SlowPower),
            typeof(StrengthPower),
            typeof(TerritorialPower),
            typeof(VulnerablePower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static (CombatState CombatState, T Monster, IReadOnlyList<Creature> Targets) CreateCombat<T>(
        int ascensionLevel,
        string seed = "task14-monster",
        int targetCount = 1)
        where T : MonsterModel
    {
        var runState = new RunState(seed, new Overgrowth(), ascensionLevel);
        var combatState = new CombatState(runState);
        var targets = Enumerable.Range(0, targetCount)
            .Select(_ => Creature.CreateStandaloneForTests(1_000_000, 1_000_000))
            .ToArray();
        foreach (Creature target in targets)
        {
            combatState.AddPlayerCreature(target);
        }

        var monster = (T)ModelDb.Monster<T>().MutableClone();
        combatState.AddMonster(monster, CombatSide.Enemy);
        monster.SetUpForCombat();
        monster.RollMove(targets);
        return (combatState, monster, targets);
    }

    public static async Task PerformAndRoll(MonsterModel monster, IReadOnlyList<Creature> targets)
    {
        await monster.PerformMove();
        monster.RollMove(targets);
    }

    public static void ForceMove(MonsterModel monster, string stateId) =>
        monster.SetMoveImmediate((Sts2Sim.Core.MonsterMoves.MoveState)monster.MoveStateMachine!.States[stateId], forceTransition: true);
}
