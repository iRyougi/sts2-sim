namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class BowlbugNectar : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 36, 35);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 39, 38);
    private int BuffStrength => Ascension(AscensionLevel.DeadlyEnemies, 16, 15);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var first = new MoveState("THRASH_MOVE", Thrash, new SingleAttackIntent(3));
        var buff = new MoveState("BUFF_MOVE", Buff, new BuffIntent());
        var second = new MoveState("THRASH2_MOVE", Thrash, new SingleAttackIntent(3));
        first.FollowUpState = buff;
        buff.FollowUpState = second;
        second.FollowUpState = second;
        return new MonsterMoveStateMachine(new MonsterState[] { first, buff, second }, first);
    }

    private Task Thrash(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(3).FromMonster(this).Execute();

    private Task Buff(IReadOnlyList<Creature> targets) =>
        PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, BuffStrength, Creature, null);

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
