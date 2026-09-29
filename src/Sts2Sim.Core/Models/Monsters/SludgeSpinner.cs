namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class SludgeSpinner : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 41, 37);
    public override int MaxInitialHp => Value(AscensionLevel.ToughEnemies, 42, 39);
    private int OilSprayDamage => Value(AscensionLevel.DeadlyEnemies, 9, 8);
    private int SlamDamage => Value(AscensionLevel.DeadlyEnemies, 12, 11);
    private int RageDamage => Value(AscensionLevel.DeadlyEnemies, 7, 6);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var oil = new MoveState("OIL_SPRAY_MOVE", OilSpray, new SingleAttackIntent(() => OilSprayDamage), new DebuffIntent());
        var slam = new MoveState("SLAM_MOVE", Slam, new SingleAttackIntent(() => SlamDamage));
        var rage = new MoveState("RAGE_MOVE", Rage, new SingleAttackIntent(() => RageDamage), new BuffIntent());
        var random = new RandomBranchState("RAND");
        random.AddBranch(oil, MoveRepeatType.CannotRepeat);
        random.AddBranch(slam, MoveRepeatType.CannotRepeat);
        random.AddBranch(rage, MoveRepeatType.CannotRepeat);
        oil.FollowUpState = random;
        slam.FollowUpState = random;
        rage.FollowUpState = random;
        return new MonsterMoveStateMachine([random, oil, slam, rage], oil);
    }

    private async Task OilSpray(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(OilSprayDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 1m, Creature, null);
        }
    }

    private Task Slam(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(SlamDamage).FromMonster(this).Execute();

    private async Task Rage(IReadOnlyList<Creature> _)
    {
        await DamageCmd.Attack(RageDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
