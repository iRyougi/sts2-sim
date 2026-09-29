namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class FossilStalker : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 54, 51);
    public override int MaxInitialHp => Value(AscensionLevel.ToughEnemies, 56, 53);
    private int TackleDamage => Value(AscensionLevel.DeadlyEnemies, 11, 9);
    private int LatchDamage => Value(AscensionLevel.DeadlyEnemies, 14, 12);
    private int LashDamage => Value(AscensionLevel.DeadlyEnemies, 4, 3);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<SuckPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var tackle = new MoveState("TACKLE_MOVE", Tackle, new SingleAttackIntent(() => TackleDamage), new DebuffIntent());
        var latch = new MoveState("LATCH_MOVE", Latch, new SingleAttackIntent(() => LatchDamage));
        var lash = new MoveState("LASH_MOVE", Lash, new MultiAttackIntent(() => LashDamage, () => 2));
        var random = new RandomBranchState("RAND");
        random.AddBranch(latch, 2);
        random.AddBranch(tackle, 2);
        random.AddBranch(lash, 2);
        tackle.FollowUpState = random;
        latch.FollowUpState = random;
        lash.FollowUpState = random;
        return new MonsterMoveStateMachine([random, tackle, latch, lash], latch);
    }

    private async Task Tackle(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(TackleDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 1m, Creature, null);
        }
    }

    private Task Latch(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(LatchDamage).FromMonster(this).Execute();

    private Task Lash(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(LashDamage).WithHitCount(2).FromMonster(this).Execute();

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
