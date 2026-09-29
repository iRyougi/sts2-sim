namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class SneakyGremlin : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 11, 10);
    public override int MaxInitialHp => Value(AscensionLevel.ToughEnemies, 15, 14);
    private int TackleDamage => Value(AscensionLevel.DeadlyEnemies, 10, 9);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var spawned = new MoveState("SPAWNED_MOVE", Spawned, new StunIntent());
        var tackle = new MoveState("TACKLE_MOVE", Tackle, new SingleAttackIntent(() => TackleDamage));
        spawned.FollowUpState = tackle;
        tackle.FollowUpState = tackle;
        return new MonsterMoveStateMachine([spawned, tackle], spawned);
    }

    private Task Spawned(IReadOnlyList<Creature> _) => Task.CompletedTask;

    private Task Tackle(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(TackleDamage).FromMonster(this).Execute();

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
