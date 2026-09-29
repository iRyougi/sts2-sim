namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Fogmog : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 78, 74);

    public override int MaxInitialHp => MinInitialHp;

    private int SwipeDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 9, 8);

    private int HeadbuttDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 16, 14);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var illusion = new MoveState(
            "ILLUSION_MOVE",
            IllusionMove,
            new SummonIntent());
        var swipe = new MoveState(
            "SWIPE_MOVE",
            SwipeMove,
            new SingleAttackIntent(SwipeDamage),
            new BuffIntent());
        var swipeRandom = new MoveState(
            "SWIPE_RANDOM_MOVE",
            SwipeMove,
            new SingleAttackIntent(SwipeDamage),
            new BuffIntent());
        var headbutt = new MoveState(
            "HEADBUTT_MOVE",
            HeadbuttMove,
            new SingleAttackIntent(HeadbuttDamage));
        var branch = new RandomBranchState("BRANCH");
        branch.AddBranch(swipeRandom, MoveRepeatType.CannotRepeat, 0.4f);
        branch.AddBranch(headbutt, MoveRepeatType.CannotRepeat, 0.6f);
        illusion.FollowUpState = swipe;
        swipe.FollowUpState = branch;
        swipeRandom.FollowUpState = headbutt;
        headbutt.FollowUpState = swipe;
        return new MonsterMoveStateMachine(
            new MonsterState[] { illusion, swipe, swipeRandom, branch, headbutt },
            illusion);
    }

    private async Task IllusionMove(IReadOnlyList<Creature> targets)
    {
        if (!Creature.CombatState!.IsLiveCombat())
        {
            return;
        }

        var eye = (EyeWithTeeth)ModelDb.Monster<EyeWithTeeth>().MutableClone();
        await CreatureCmd.Add(
            eye,
            Creature.CombatState,
            Creature.Side,
            "illusion");
    }

    private async Task SwipeMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SwipeDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            1m,
            Creature,
            cardSource: null);
    }

    private async Task HeadbuttMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(HeadbuttDamage).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
