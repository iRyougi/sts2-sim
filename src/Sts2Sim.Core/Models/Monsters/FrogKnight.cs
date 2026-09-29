namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class FrogKnight : MonsterModel
{
    private bool _charged;
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 199, 191);
    public override int MaxInitialHp => MinInitialHp;
    private int StrikeDamage => Ascension(AscensionLevel.DeadlyEnemies, 23, 21);
    private int LashDamage => Ascension(AscensionLevel.DeadlyEnemies, 14, 13);
    private int ChargeDamage => Ascension(AscensionLevel.DeadlyEnemies, 40, 35);
    private int Plating => Ascension(AscensionLevel.ToughEnemies, 19, 15);
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<PlatingPower>(Creature.CombatState!, Creature, Plating, Creature, null);
        _charged = false;
    }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var queen = new MoveState("FOR_THE_QUEEN", ForTheQueen, new BuffIntent());
        var strike = new MoveState("STRIKE_DOWN_EVIL", Strike, new SingleAttackIntent(StrikeDamage));
        var lash = new MoveState("TONGUE_LASH", Lash, new SingleAttackIntent(LashDamage), new DebuffIntent());
        var charge = new MoveState("BEETLE_CHARGE", Charge, new SingleAttackIntent(ChargeDamage));
        var half = new ConditionalBranchState("HALF_HEALTH")
            .AddBranch(_ => _charged || Creature.CurrentHp >= Creature.MaxHp / 2, lash.Id)
            .AddBranch(_ => !_charged && Creature.CurrentHp < Creature.MaxHp / 2, charge.Id);
        queen.FollowUpState = half; strike.FollowUpState = queen; lash.FollowUpState = strike; charge.FollowUpState = lash;
        return new MonsterMoveStateMachine(new MonsterState[] { queen, strike, lash, charge, half }, lash);
    }
    private Task ForTheQueen(IReadOnlyList<Creature> targets) => PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 5m, Creature, null);
    private Task Strike(IReadOnlyList<Creature> targets) => DamageCmd.Attack(StrikeDamage).FromMonster(this).Execute();
    private async Task Lash(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(LashDamage).FromMonster(this).Execute();
        foreach (Creature target in targets) await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 2m, Creature, null);
    }
    private Task Charge(IReadOnlyList<Creature> targets) { _charged = true; return DamageCmd.Attack(ChargeDamage).FromMonster(this).Execute(); }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_charged);

    private int Ascension(AscensionLevel level, int high, int low) => Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
