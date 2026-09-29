namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Seapunk : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 47, 44);
    public override int MaxInitialHp => Value(AscensionLevel.ToughEnemies, 49, 46);
    private int SeaKickDamage => Value(AscensionLevel.DeadlyEnemies, 13, 11);
    private int BubbleBlock => Value(AscensionLevel.ToughEnemies, 8, 7);
    private int BubbleStrength => Value(AscensionLevel.DeadlyEnemies, 2, 1);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var seaKick = new MoveState("SEA_KICK_MOVE", SeaKick, new SingleAttackIntent(() => SeaKickDamage));
        var spinningKick = new MoveState("SPINNING_KICK_MOVE", SpinningKick, new MultiAttackIntent(2, 4));
        var bubbleBurp = new MoveState("BUBBLE_BURP_MOVE", BubbleBurp, new BuffIntent(), new DefendIntent());
        seaKick.FollowUpState = spinningKick;
        spinningKick.FollowUpState = bubbleBurp;
        bubbleBurp.FollowUpState = seaKick;
        return new MonsterMoveStateMachine([seaKick, spinningKick, bubbleBurp], seaKick);
    }

    private Task SeaKick(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(SeaKickDamage).FromMonster(this).Execute();

    private Task SpinningKick(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(2).WithHitCount(4).FromMonster(this).Execute();

    private async Task BubbleBurp(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.GainBlock(Creature.CombatState!, Creature, BubbleBlock, ValueProp.Move, null, null);
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, BubbleStrength, Creature, null);
    }

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
