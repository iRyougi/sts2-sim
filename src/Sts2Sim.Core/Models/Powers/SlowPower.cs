namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.ValueProps;

/// <summary>Increases powered attack damage received by the owner by 10 percent per card already played.</summary>
public sealed class SlowPower : PowerModel, ICombatStateDescriptionContributor
{
    private int _cardsPlayedSinceOwnerTurnStart;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        _cardsPlayedSinceOwnerTurnStart++;
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return target == Owner && props.IsPoweredAttack()
            ? 1m + (0.1m * _cardsPlayedSinceOwnerTurnStart)
            : 1m;
    }

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            _cardsPlayedSinceOwnerTurnStart = 0;
        }

        return Task.CompletedTask;
    }

    void ICombatStateDescriptionContributor.AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        builder.Append(_cardsPlayedSinceOwnerTurnStart);
}
