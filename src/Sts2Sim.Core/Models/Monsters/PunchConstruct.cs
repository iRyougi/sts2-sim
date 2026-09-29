namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class PunchConstruct : MonsterModel
{
    private bool _startsWithFastPunch;
    private int _startingHpReduction;

    public bool StartsWithFastPunch
    {
        get => _startsWithFastPunch;
        set
        {
            AssertMutable();
            _startsWithFastPunch = value;
        }
    }

    public int StartingHpReduction
    {
        get => _startingHpReduction;
        set
        {
            AssertMutable();
            _startingHpReduction = value;
        }
    }

    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 60, 55);
    public override int MaxInitialHp => MinInitialHp;

    private int StrongDamage => Value(AscensionLevel.DeadlyEnemies, 16, 14);
    private int FastDamage => Value(AscensionLevel.DeadlyEnemies, 6, 5);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<ArtifactPower>(Creature.CombatState!, Creature, 1m, Creature, null);
        if (StartingHpReduction > 0)
            Creature.SetCurrentHpInternal(Math.Max(1, Creature.CurrentHp - StartingHpReduction));
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var ready = new MoveState("READY_MOVE", Ready, new DefendIntent());
        var fast = new MoveState(
            "FAST_PUNCH_MOVE",
            Fast,
            new MultiAttackIntent(FastDamage, 2),
            new DebuffIntent());
        var strong = new MoveState("STRONG_PUNCH_MOVE", Strong, new SingleAttackIntent(StrongDamage));

        ready.FollowUpState = fast;
        fast.FollowUpState = strong;
        strong.FollowUpState = ready;

        return new MonsterMoveStateMachine([ready, fast, strong], StartsWithFastPunch ? fast : ready);
    }

    private Task Ready(IReadOnlyList<Creature> _) =>
        CreatureCmd.GainBlock(Creature.CombatState!, Creature, 10m, ValueProp.Move, null, null);

    private Task Strong(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(StrongDamage).FromMonster(this).Execute();

    private async Task Fast(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(FastDamage).WithHitCount(2).FromMonster(this).Execute();

        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 1m, Creature, null);
        }
    }

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
