using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Powers;

public sealed class StormPower : PowerModel
{
    private Dictionary<CardModel, int> _amountsForPlayedCards = new(ReferenceEqualityComparer.Instance);
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner.Player && cardPlay.Card.Type == CardType.Power)
            _amountsForPlayedCards.Add(cardPlay.Card, Amount);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player ||
            !_amountsForPlayedCards.Remove(cardPlay.Card, out int lightning) || lightning <= 0)
            return;
        for (int i = 0; i < lightning; i++)
            await OrbCmd.Channel<LightningOrb>(Owner.CombatState!, Owner.Player!);
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _amountsForPlayedCards = new Dictionary<CardModel, int>(
            _amountsForPlayedCards, ReferenceEqualityComparer.Instance);
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source, IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        _amountsForPlayedCards = new Dictionary<CardModel, int>(ReferenceEqualityComparer.Instance);
        foreach ((CardModel card, int amount) in ((StormPower)source)._amountsForPlayedCards)
            if (cardMap.TryGetValue(card, out CardModel? clone))
                _amountsForPlayedCards.Add(clone, amount);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context) =>
        context.AssertTransientEmpty(_amountsForPlayedCards.Count == 0, nameof(_amountsForPlayedCards));
}
