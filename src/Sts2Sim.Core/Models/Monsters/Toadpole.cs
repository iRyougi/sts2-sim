namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Toadpole : MonsterModel
{
    private bool _isFront;

    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 22, 21);
    public override int MaxInitialHp => Value(AscensionLevel.ToughEnemies, 26, 25);
    private int SpikeSpitDamage => Value(AscensionLevel.DeadlyEnemies, 4, 3);
    private int WhirlDamage => Value(AscensionLevel.DeadlyEnemies, 8, 7);

    public bool IsFront
    {
        get => _isFront;
        set
        {
            AssertMutable();
            _isFront = value;
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var spikeSpit = new MoveState("SPIKE_SPIT_MOVE", SpikeSpit, new MultiAttackIntent(() => SpikeSpitDamage, () => 3));
        var whirl = new MoveState("WHIRL_MOVE", Whirl, new SingleAttackIntent(() => WhirlDamage));
        var spiken = new MoveState("SPIKEN_MOVE", Spiken, new BuffIntent());
        var initial = new ConditionalBranchState("INIT_MOVE")
            .AddBranch(_ => !IsFront, whirl.Id)
            .AddBranch(_ => IsFront, spiken.Id);
        whirl.FollowUpState = spiken;
        spiken.FollowUpState = spikeSpit;
        spikeSpit.FollowUpState = whirl;
        return new MonsterMoveStateMachine([initial, spiken, spikeSpit, whirl], initial);
    }

    private async Task SpikeSpit(IReadOnlyList<Creature> _)
    {
        await PowerCmd.Apply<ThornsPower>(Creature.CombatState!, Creature, -2m, Creature, null);
        await DamageCmd.Attack(SpikeSpitDamage).WithHitCount(3).FromMonster(this).Execute();
    }

    private Task Whirl(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(WhirlDamage).FromMonster(this).Execute();

    private Task Spiken(IReadOnlyList<Creature> _) =>
        PowerCmd.Apply<ThornsPower>(Creature.CombatState!, Creature, 2m, Creature, null);

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_isFront);

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
