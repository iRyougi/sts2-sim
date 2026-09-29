namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;

internal sealed class Task13MonsterTestFixture : IDisposable
{
    public Task13MonsterTestFixture(Type monsterType)
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            monsterType,
            typeof(StrengthPower),
            typeof(FrailPower),
            typeof(ShrinkPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static (T Monster, IReadOnlyList<Creature> Targets) CreateCombat<T>(
        int ascensionLevel,
        int targetCount = 1)
        where T : MonsterModel
    {
        var runState = new RunState("task13-monster", new Overgrowth(), ascensionLevel);
        var combatState = new CombatState(runState);
        var targets = Enumerable.Range(0, targetCount)
            .Select(_ => Creature.CreateStandaloneForTests(200, 200))
            .ToArray();
        foreach (Creature target in targets)
        {
            combatState.AddPlayerCreature(target);
        }

        var monster = (T)ModelDb.Monster<T>().MutableClone();
        combatState.AddMonster(monster, CombatSide.Enemy);
        monster.SetUpForCombat();
        monster.RollMove(targets);
        return (monster, targets);
    }

    public static async Task PerformAndRoll(MonsterModel monster, IReadOnlyList<Creature> targets)
    {
        await monster.PerformMove();
        monster.RollMove(targets);
    }
}
