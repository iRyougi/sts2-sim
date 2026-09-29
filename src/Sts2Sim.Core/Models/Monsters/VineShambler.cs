namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class VineShambler : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 64, 61);

    public override int MaxInitialHp => MinInitialHp;

    private int GraspingVinesDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 9, 8);

    private int SwipeDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 7, 6);

    private int ChompDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 18, 16);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var graspingVines = new MoveState(
            "GRASPING_VINES_MOVE",
            GraspingVinesMove,
            new SingleAttackIntent(GraspingVinesDamage),
            new CardDebuffIntent());
        var swipe = new MoveState(
            "SWIPE_MOVE",
            SwipeMove,
            new MultiAttackIntent(SwipeDamage, 2));
        var chomp = new MoveState(
            "CHOMP_MOVE",
            ChompMove,
            new SingleAttackIntent(ChompDamage));
        swipe.FollowUpState = graspingVines;
        graspingVines.FollowUpState = chomp;
        chomp.FollowUpState = swipe;
        return new MonsterMoveStateMachine(
            new MonsterState[] { graspingVines, swipe, chomp },
            swipe);
    }

    private async Task GraspingVinesMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(GraspingVinesDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<TangledPower>(
                Creature.CombatState!,
                target,
                1m,
                Creature,
                cardSource: null);
        }
    }

    private async Task SwipeMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SwipeDamage).WithHitCount(2).FromMonster(this).Execute();
    }

    private async Task ChompMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ChompDamage).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
