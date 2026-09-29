namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

/// <summary>Raises the combat energy cost of the owner's attack cards until its side turn ends.</summary>
public sealed class TangledPower : PowerModel
{
    private HashSet<CardModel> _affectedCards = new(ReferenceEqualityComparer.Instance);

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        foreach (CardModel card in Owner.Player!.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards))
        {
            MarkIfOwnedAttack(card);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        MarkIfOwnedAttack(card);
        return Task.CompletedTask;
    }

    public override Task AfterCardEntryAborted(CardModel card)
    {
        _affectedCards.Remove(card);
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        if (card.Owner != Owner.Player || card.CostsXEnergy || !_affectedCards.Contains(card))
        {
            modifiedCost = originalCost;
            return false;
        }

        modifiedCost = originalCost + Amount;
        return true;
    }

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        _affectedCards.Clear();
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _affectedCards = new HashSet<CardModel>(ReferenceEqualityComparer.Instance);
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        var sourcePower = (TangledPower)source;
        foreach (CardModel card in sourcePower._affectedCards)
        {
            if (cardMap.TryGetValue(card, out CardModel? clone))
            {
                _affectedCards.Add(clone);
            }
        }
    }

    private void MarkIfOwnedAttack(CardModel card)
    {
        if (card.Owner == Owner.Player && card.Type == CardType.Attack)
        {
            _affectedCards.Add(card);
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        context.AppendCardReferences(ref builder, _affectedCards);
    }
}
