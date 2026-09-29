namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

public sealed class HardenedShellPower : PowerModel
{
    private decimal _damageReceivedThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public int DisplayAmount => (int)Math.Max(0m, Amount - _damageReceivedThisTurn);

    public override decimal ModifyHpLostBeforeOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (!ReferenceEquals(target, Owner) || amount == 0m)
        {
            return amount;
        }

        return Math.Min(amount, Math.Max(0m, Amount - _damageReceivedThisTurn));
    }

    public override Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (ReferenceEquals(target, Owner) && result.UnblockedDamage > 0)
        {
            _damageReceivedThisTurn += result.UnblockedDamage;
        }

        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        _damageReceivedThisTurn = 0m;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_damageReceivedThisTurn);
}
