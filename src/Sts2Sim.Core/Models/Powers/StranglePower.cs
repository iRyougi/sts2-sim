using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class StranglePower : PowerModel
{
    private Dictionary<CardModel, int> _amountsForPlayedCards =
        new(ReferenceEqualityComparer.Instance);

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Applier?.Player is { } player && cardPlay.Card.Owner == player)
            _amountsForPlayedCards.Add(cardPlay.Card, Amount);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (_amountsForPlayedCards.Remove(cardPlay.Card, out int amount))
            await CreatureCmd.Damage(Owner.CombatState!, new[] { Owner }, amount,
                ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _amountsForPlayedCards = new Dictionary<CardModel, int>(
            _amountsForPlayedCards,
            ReferenceEqualityComparer.Instance);
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        var sourcePower = (StranglePower)source;
        _amountsForPlayedCards = new Dictionary<CardModel, int>(ReferenceEqualityComparer.Instance);
        foreach ((CardModel sourceCard, int amount) in sourcePower._amountsForPlayedCards)
        {
            if (!cardMap.TryGetValue(sourceCard, out CardModel? clonedCard))
            {
                throw new InvalidOperationException(
                    "Cannot clone StranglePower with an active snapshot for a card outside combat piles.");
            }

            _amountsForPlayedCards.Add(clonedCard, amount);
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        context.AssertTransientEmpty(
            _amountsForPlayedCards.Count == 0,
            nameof(_amountsForPlayedCards));
    }
}
