namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class AxeRubyRaider : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 21, 20);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 23, 22);

    private int SwingDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 6, 5);

    private int SwingBlock => AscensionValue(AscensionLevel.DeadlyEnemies, 6, 5);

    private int BigSwingDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 13, 12);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var swing1 = new MoveState(
            "SWING_1",
            SwingMove,
            new SingleAttackIntent(SwingDamage),
            new DefendIntent());
        var swing2 = new MoveState(
            "SWING_2",
            SwingMove,
            new SingleAttackIntent(SwingDamage),
            new DefendIntent());
        var bigSwing = new MoveState(
            "BIG_SWING",
            BigSwingMove,
            new SingleAttackIntent(BigSwingDamage));
        swing1.FollowUpState = swing2;
        swing2.FollowUpState = bigSwing;
        bigSwing.FollowUpState = swing1;
        return new MonsterMoveStateMachine(new MonsterState[] { swing1, swing2, bigSwing }, swing1);
    }

    private async Task SwingMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SwingDamage).FromMonster(this).Execute();
        await CreatureCmd.GainBlock(
            Creature.CombatState!,
            Creature,
            SwingBlock,
            ValueProp.Move,
            cardSource: null,
            cardPlay: null);
    }

    private async Task BigSwingMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BigSwingDamage).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
