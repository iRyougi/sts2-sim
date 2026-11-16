namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class InfestedPrism : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 171, 161);
    public override int MaxInitialHp => MinInitialHp;
    private int JabDamage => Ascension(AscensionLevel.DeadlyEnemies, 17, 15);
    private int WhirlwindDamage => Ascension(AscensionLevel.DeadlyEnemies, 6, 5);
    private int PulsateDamage => Ascension(AscensionLevel.DeadlyEnemies, 10, 8);
    private int SparkAmount => Ascension(AscensionLevel.DeadlyEnemies, 3, 2);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<VitalSparkPower>(Creature.CombatState!, Creature, SparkAmount, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var jab = new MoveState("JAB_MOVE", Jab, new SingleAttackIntent(JabDamage));
        var jab2 = new MoveState("JAB_MOVE_2", Jab, new SingleAttackIntent(JabDamage));
        var whirlwind = new MoveState("WHIRLWIND_MOVE", Whirlwind, new MultiAttackIntent(WhirlwindDamage, 3));
        var pulsate = new MoveState("PULSATE_MOVE", Pulsate, new SingleAttackIntent(PulsateDamage), new DebuffIntent(), new BuffIntent());
        jab.FollowUpState = jab2;
        jab2.FollowUpState = whirlwind;
        whirlwind.FollowUpState = pulsate;
        pulsate.FollowUpState = jab;
        return new MonsterMoveStateMachine(new MonsterState[] { jab, jab2, whirlwind, pulsate }, jab);
    }

    private Task Jab(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(JabDamage).FromMonster(this).Execute();

    private Task Whirlwind(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(WhirlwindDamage).WithHitCount(3).FromMonster(this).Execute();

    private async Task Pulsate(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PulsateDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
            await PowerCmd.Apply<VulnerablePower>(Creature.CombatState!, target, 1m, Creature, null);
        await PowerCmd.Apply<VitalSparkPower>(Creature.CombatState!, Creature, SparkAmount, Creature, null);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
