namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class SewerClam : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 58, 56);
    public override int MaxInitialHp => MinInitialHp;
    private int JetDamage => Value(AscensionLevel.DeadlyEnemies, 11, 10);
    private int Plating => Value(AscensionLevel.ToughEnemies, 9, 8);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<PlatingPower>(Creature.CombatState!, Creature, Plating, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var pressurize = new MoveState("PRESSURIZE_MOVE", Pressurize, new BuffIntent());
        var jet = new MoveState("JET_MOVE", Jet, new SingleAttackIntent(() => JetDamage));
        pressurize.FollowUpState = jet;
        jet.FollowUpState = pressurize;
        return new MonsterMoveStateMachine([pressurize, jet], jet);
    }

    private Task Pressurize(IReadOnlyList<Creature> _) =>
        PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 4m, Creature, null);

    private Task Jet(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(JetDamage).FromMonster(this).Execute();

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
