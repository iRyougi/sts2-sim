namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class BowlbugEgg : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 23, 21);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 24, 22);
    private int Damage => Ascension(AscensionLevel.DeadlyEnemies, 8, 7);
    private int Block => Ascension(AscensionLevel.DeadlyEnemies, 8, 7);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var bite = new MoveState("BITE_MOVE", Bite, new SingleAttackIntent(Damage), new DefendIntent());
        bite.FollowUpState = bite;
        return new MonsterMoveStateMachine(new[] { bite }, bite);
    }

    private async Task Bite(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Damage).FromMonster(this).Execute();
        await CreatureCmd.GainBlock(Creature.CombatState!, Creature, Block, ValueProp.Move, null, null);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
