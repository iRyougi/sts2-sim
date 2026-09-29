namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class Nibbit : MonsterModel
{
    private bool _isFront;
    private bool _isAlone;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 44, 42);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 48, 46);

    public bool IsFront
    {
        get => _isFront;
        set
        {
            AssertMutable();
            _isFront = value;
        }
    }

    public bool IsAlone
    {
        get => _isAlone;
        set
        {
            AssertMutable();
            _isAlone = value;
        }
    }

    private int ButtDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 13, 12);

    private int SliceDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 7, 6);

    private int SliceBlock => AscensionValue(AscensionLevel.ToughEnemies, 6, 5);

    private int HissStrengthGain => AscensionValue(AscensionLevel.DeadlyEnemies, 3, 2);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var butt = new MoveState(
            "BUTT_MOVE",
            ButtMove,
            new SingleAttackIntent(ButtDamage));
        var slice = new MoveState(
            "SLICE_MOVE",
            SliceMove,
            new SingleAttackIntent(SliceDamage),
            new DefendIntent());
        var hiss = new MoveState(
            "HISS_MOVE",
            HissMove,
            new BuffIntent());
        var initial = new ConditionalBranchState("INIT_MOVE");
        if (IsAlone)
        {
            initial.AddBranch(
                creature => ((Nibbit)creature.Monster!).IsAlone,
                butt.Id);
        }
        else
        {
            initial.AddBranch(
                creature => !((Nibbit)creature.Monster!).IsFront,
                hiss.Id);
            initial.AddBranch(
                creature => ((Nibbit)creature.Monster!).IsFront,
                slice.Id);
        }
        butt.FollowUpState = slice;
        slice.FollowUpState = hiss;
        hiss.FollowUpState = butt;
        return new MonsterMoveStateMachine(
            new MonsterState[] { initial, butt, slice, hiss },
            initial);
    }

    private async Task ButtMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ButtDamage).FromMonster(this).Execute();
    }

    private async Task SliceMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SliceDamage).FromMonster(this).Execute();
        await CreatureCmd.GainBlock(
            Creature.CombatState!,
            Creature,
            SliceBlock,
            ValueProp.Move,
            cardSource: null,
            cardPlay: null);
    }

    private async Task HissMove(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            HissStrengthGain,
            Creature,
            cardSource: null);
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
