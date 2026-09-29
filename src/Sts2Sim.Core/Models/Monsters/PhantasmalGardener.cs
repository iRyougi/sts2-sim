namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class PhantasmalGardener : MonsterModel
{
    private int _enlargeTriggers;

    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 27, 26);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 32, 31);

    public int EnlargeTriggers
    {
        get => _enlargeTriggers;
        set
        {
            AssertMutable();
            _enlargeTriggers = value;
        }
    }

    public float CurrentScale { get; private set; } = 1f;

    private int BiteDamage => Ascension(AscensionLevel.DeadlyEnemies, 5, 5);
    private int LashDamage => Ascension(AscensionLevel.DeadlyEnemies, 7, 7);
    private int FlailRepeat => Ascension(AscensionLevel.DeadlyEnemies, 3, 3);
    private int EnlargeStrength => Ascension(AscensionLevel.DeadlyEnemies, 3, 2);
    private int SkittishAmount => Ascension(AscensionLevel.ToughEnemies, 7, 6);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<SkittishPower>(Creature.CombatState!, Creature, SkittishAmount, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var bite = new MoveState("BITE_MOVE", Bite, new SingleAttackIntent(BiteDamage));
        var lash = new MoveState("LASH_MOVE", Lash, new SingleAttackIntent(LashDamage));
        var flail = new MoveState("FLAIL_MOVE", Flail, new MultiAttackIntent(1, FlailRepeat));
        var enlarge = new MoveState("ENLARGE_MOVE", Enlarge, new BuffIntent());
        var initial = new ConditionalBranchState("INIT_MOVE")
            .AddBranch(owner => owner.SlotName == "first", flail.Id)
            .AddBranch(owner => owner.SlotName == "second", bite.Id)
            .AddBranch(owner => owner.SlotName == "third", lash.Id)
            .AddBranch(owner => owner.SlotName == "fourth", enlarge.Id);
        bite.FollowUpState = lash;
        lash.FollowUpState = flail;
        flail.FollowUpState = enlarge;
        enlarge.FollowUpState = bite;
        return new MonsterMoveStateMachine([initial, bite, lash, flail, enlarge], initial);
    }

    private Task Bite(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(BiteDamage).FromMonster(this).Execute();

    private Task Lash(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(LashDamage).FromMonster(this).Execute();

    private Task Flail(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(1).WithHitCount(FlailRepeat).FromMonster(this).Execute();

    private async Task Enlarge(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, EnlargeStrength, Creature, null);
        EnlargeTriggers++;
        CurrentScale = 1f + 0.1f * MathF.Log(EnlargeTriggers + 1f);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_enlargeTriggers);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
