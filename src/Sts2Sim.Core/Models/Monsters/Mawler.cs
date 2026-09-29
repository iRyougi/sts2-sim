namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Mawler : MonsterModel
{
    private const int ClawRepeat = 2;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 76, 72);

    public override int MaxInitialHp => MinInitialHp;

    private int RipAndTearDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 16, 14);

    private int ClawDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 5, 4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var ripAndTear = new MoveState(
            "RIP_AND_TEAR_MOVE",
            RipAndTearMove,
            new SingleAttackIntent(RipAndTearDamage));
        var roar = new MoveState("ROAR_MOVE", RoarMove, new DebuffIntent());
        var claw = new MoveState(
            "CLAW_MOVE",
            ClawMove,
            new MultiAttackIntent(ClawDamage, ClawRepeat));
        var random = new RandomBranchState("RAND");
        ripAndTear.FollowUpState = random;
        roar.FollowUpState = random;
        claw.FollowUpState = random;
        random.AddBranch(ripAndTear, MoveRepeatType.CannotRepeat);
        random.AddBranch(roar, MoveRepeatType.UseOnlyOnce);
        random.AddBranch(claw, MoveRepeatType.CannotRepeat);
        return new MonsterMoveStateMachine(
            new MonsterState[] { ripAndTear, roar, claw, random },
            claw);
    }

    private async Task RipAndTearMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(RipAndTearDamage).FromMonster(this).Execute();
    }

    private async Task RoarMove(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<VulnerablePower>(
                Creature.CombatState!,
                target,
                3m,
                Creature,
                cardSource: null);
        }
    }

    private async Task ClawMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ClawDamage).WithHitCount(ClawRepeat).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
