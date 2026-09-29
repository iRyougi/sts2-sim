namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class TwigSlimeS : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 8, 7);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 12, 11);

    private int TackleDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 5, 4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var tackle = new MoveState(
            "TACKLE_MOVE",
            TackleMove,
            new SingleAttackIntent(TackleDamage));
        tackle.FollowUpState = tackle;
        return new MonsterMoveStateMachine(new MonsterState[] { tackle }, tackle);
    }

    private async Task TackleMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(TackleDamage).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
