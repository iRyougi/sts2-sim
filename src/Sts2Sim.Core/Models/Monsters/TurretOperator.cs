namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class TurretOperator : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 51, 41);
    public override int MaxInitialHp => MinInitialHp;
    private int Damage => Value(AscensionLevel.DeadlyEnemies, 4, 3);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var firstUnload = new MoveState("UNLOAD_MOVE", Unload, new MultiAttackIntent(Damage, 5));
        var secondUnload = new MoveState("UNLOAD_MOVE_2", Unload, new MultiAttackIntent(Damage, 5));
        var reload = new MoveState("RELOAD_MOVE", Reload, new BuffIntent());
        firstUnload.FollowUpState = secondUnload;
        secondUnload.FollowUpState = reload;
        reload.FollowUpState = firstUnload;
        return new MonsterMoveStateMachine([firstUnload, secondUnload, reload], firstUnload);
    }

    private Task Unload(IReadOnlyList<Creature> _) => DamageCmd.Attack(Damage).WithHitCount(5).FromMonster(this).Execute();
    private Task Reload(IReadOnlyList<Creature> _) => PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 1m, Creature, null);

    private int Value(AscensionLevel level, int ascensionValue, int normalValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, ascensionValue, normalValue) ?? normalValue;
}
