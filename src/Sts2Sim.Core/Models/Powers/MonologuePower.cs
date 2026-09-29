using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Powers;

public sealed class MonologuePower : PowerModel
{
    private Dictionary<CardModel, int> _amountsForPlayedCards =
        new(ReferenceEqualityComparer.Instance);
    private int _strengthApplied;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.None;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner)
        {
            _amountsForPlayedCards[cardPlay.Card] = Amount;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player ||
            !_amountsForPlayedCards.Remove(cardPlay.Card, out int amount))
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(
            Owner.CombatState!, Owner, amount, Owner, null);
        _strengthApplied += amount;
    }

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        int strengthToRevoke = _strengthApplied;
        await PowerCmd.Remove(this);
        if (strengthToRevoke != 0)
        {
            await PowerCmd.Apply<StrengthPower>(
                Owner.CombatState!, Owner, -strengthToRevoke, Owner, null);
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _amountsForPlayedCards = new Dictionary<CardModel, int>(
            _amountsForPlayedCards, ReferenceEqualityComparer.Instance);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_strengthApplied);
        context.AssertTransientEmpty(
            _amountsForPlayedCards.Count == 0,
            nameof(_amountsForPlayedCards));
    }
}
