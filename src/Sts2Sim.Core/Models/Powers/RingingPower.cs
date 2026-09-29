using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Marks the owner's combat cards and permits only the first marked card play each turn.</summary>
public sealed class RingingPower : PowerModel
{
    private HashSet<CardModel> _ringingCards = new(ReferenceEqualityComparer.Instance);

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public bool IsRinging(CardModel card) => _ringingCards.Contains(card);

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        foreach (CardModel card in Owner.Player!.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards))
        {
            _ringingCards.Add(card);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card.Owner.Creature == Owner)
        {
            _ringingCards.Add(card);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardEntryAborted(CardModel card)
    {
        _ringingCards.Remove(card);
        return Task.CompletedTask;
    }

    public override bool ShouldPlay(CardModel card, bool isAutoPlay)
    {
        if (card.Owner.Creature != Owner || !_ringingCards.Contains(card))
        {
            return true;
        }

        return card.Owner.PlayerCombatState!.CardPlaysStartedThisTurn == 0;
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        _ringingCards.Clear();
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _ringingCards = new HashSet<CardModel>(ReferenceEqualityComparer.Instance);
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        var sourcePower = (RingingPower)source;
        foreach (CardModel card in sourcePower._ringingCards)
        {
            if (cardMap.TryGetValue(card, out CardModel? clone))
            {
                _ringingCards.Add(clone);
            }
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        context.AppendCardReferences(ref builder, _ringingCards);
    }
}
