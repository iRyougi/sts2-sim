using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class AfterimagePower : PowerModel
{
    private Dictionary<CardModel, int> _amountsForPlayedCards =
        new(ReferenceEqualityComparer.Instance);

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner)
        {
            _amountsForPlayedCards.Add(cardPlay.Card, Amount);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (_amountsForPlayedCards.Remove(cardPlay.Card, out int amount) && amount > 0)
        {
            await CreatureCmd.GainBlock(
                Owner.CombatState!, Owner, amount, ValueProp.Unpowered, null, null);
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _amountsForPlayedCards = new Dictionary<CardModel, int>(
            _amountsForPlayedCards, ReferenceEqualityComparer.Instance);
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Sts2Sim.Core.Entities.Creatures.Creature, Sts2Sim.Core.Entities.Creatures.Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        var sourcePower = (AfterimagePower)source;
        _amountsForPlayedCards = new Dictionary<CardModel, int>(ReferenceEqualityComparer.Instance);
        foreach ((CardModel card, int amount) in sourcePower._amountsForPlayedCards)
        {
            if (cardMap.TryGetValue(card, out CardModel? clone))
            {
                _amountsForPlayedCards.Add(clone, amount);
            }
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        context.AssertTransientEmpty(_amountsForPlayedCards.Count == 0, nameof(_amountsForPlayedCards));
}
