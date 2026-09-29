namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Inklet : MonsterModel
{
    private bool _middleInklet;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 12, 11);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 18, 17);

    public bool MiddleInklet
    {
        get => _middleInklet;
        set
        {
            AssertMutable();
            _middleInklet = value;
        }
    }

    private int JabDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 4, 3);

    private int WhirlwindDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 3, 2);

    private int PiercingGazeDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 11, 10);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<SlipperyPower>(
            Creature.CombatState!,
            Creature,
            1m,
            Creature,
            cardSource: null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var jab = new MoveState(
            "JAB_MOVE",
            JabMove,
            new SingleAttackIntent(JabDamage));
        var whirlwind = new MoveState(
            "WHIRLWIND_MOVE",
            WhirlwindMove,
            new MultiAttackIntent(WhirlwindDamage, 3));
        var piercingGaze = new MoveState(
            "PIERCING_GAZE_MOVE",
            PiercingGazeMove,
            new SingleAttackIntent(PiercingGazeDamage));
        var random = new RandomBranchState("RAND");
        random.AddBranch(piercingGaze, MoveRepeatType.CannotRepeat, 1f);
        random.AddBranch(whirlwind, MoveRepeatType.CannotRepeat, 1f);
        jab.FollowUpState = random;
        whirlwind.FollowUpState = jab;
        piercingGaze.FollowUpState = jab;
        return new MonsterMoveStateMachine(
            new MonsterState[] { jab, whirlwind, piercingGaze, random },
            MiddleInklet ? whirlwind : jab);
    }

    private async Task JabMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(JabDamage).FromMonster(this).Execute();
    }

    private async Task WhirlwindMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(WhirlwindDamage).WithHitCount(3).FromMonster(this).Execute();
    }

    private async Task PiercingGazeMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PiercingGazeDamage).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
