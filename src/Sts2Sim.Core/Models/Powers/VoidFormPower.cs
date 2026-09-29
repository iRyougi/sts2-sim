using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Powers;

public sealed class VoidFormPower : PowerModel
{
    private int _cardsPlayedThisTurn;

    public int CardsPlayedThisTurn => _cardsPlayedThisTurn;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        HideTemporaryZeroCost();
        return Task.CompletedTask;
    }

    public override Task BeforePowerAmountChanged(
        PowerModel power,
        decimal amount,
        Creature target,
        Creature? applier,
        CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            HideTemporaryZeroCost();
        }
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (ShouldSkip(card))
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    public override bool TryModifyStarCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (ShouldSkip(card))
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner &&
            !cardPlay.IsAutoPlay &&
            cardPlay.IsLastInSeries)
        {
            _cardsPlayedThisTurn++;
        }
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            _cardsPlayedThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_cardsPlayedThisTurn);
    }

    private bool ShouldSkip(CardModel card) =>
        card.Owner.Creature != Owner ||
        card.Pile?.Type is not (PileType.Hand or PileType.Play) ||
        _cardsPlayedThisTurn >= Amount;

    private void HideTemporaryZeroCost() => _cardsPlayedThisTurn = 999_999_999;
}
