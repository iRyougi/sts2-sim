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
    private decimal _strength = 1m;
    private decimal _strengthApplied;

    internal decimal Strength
    {
        get => _strength;
        set => _strength = Math.Min(value, 999999999m);
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType =>
        (int)_strengthApplied != 0 ? PowerStackType.Counter : PowerStackType.None;

    public override int DisplayAmount => (int)_strengthApplied;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner)
        {
            _amountsForPlayedCards.Add(cardPlay.Card, (int)_strength);
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
        _strengthApplied = Math.Min(_strengthApplied + (decimal)(int)_strength, 999999999m);
    }

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        await PowerCmd.Remove(this);
        await PowerCmd.Apply<StrengthPower>(
            Owner.CombatState!, Owner, -_strengthApplied, Owner, null);
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
        base.AppendCombatStateDescription(ref builder, context);
        builder.Append(_strength);
        builder.Append(_strengthApplied);
        context.AssertTransientEmpty(
            _amountsForPlayedCards.Count == 0,
            nameof(_amountsForPlayedCards));
    }
}
