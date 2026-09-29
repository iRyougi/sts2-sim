using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>
/// Removes up to five upgradable cards on pickup and returns one random stored card, upgraded,
/// after each survived combat. Deviation #215 is limited to cross-process serialization of the
/// stored-card list; the complete in-memory lifecycle is represented.
/// </summary>
public sealed class PaelsTooth : RelicModel
{
    private List<CardModel> _removedCards = new();

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        IReadOnlyList<CardModel> selected = (await CardSelectCmd.SelectCardsAsync(
                Owner.RunState,
                Owner,
                Owner.Deck.Cards.Where(card => card.IsRemovable && card.IsUpgradable),
                minCount: 5,
                maxCount: 5,
                source: this))
            .OrderBy(card => card.Id.Entry, StringComparer.Ordinal)
            .ToList();
        foreach (CardModel card in selected)
        {
            var stored = (CardModel)card.MutableClone();
            stored.AssignOwner(Owner);
            _removedCards.Add(stored);
            await CardPileCmd.RemoveFromDeck(Owner, card);
        }
    }

    public override async Task AfterCombatEnd()
    {
        if (Owner.Creature.IsDead || _removedCards.Count == 0)
            return;

        int index = Owner.PlayerRng.Rewards.NextInt(_removedCards.Count);
        CardModel card = _removedCards[index];
        card.AssignOwner(Owner);
        if (card.IsUpgradable)
        {
            CardCmd.Upgrade(card);
        }
        await CardPileCmd.AddToDeck(card);
        _removedCards.RemoveAt(index);
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _removedCards = _removedCards
            .Select(card => (CardModel)card.MutableClone())
            .ToList();
    }

    internal override void RestoreCombatCloneReferencesFrom(
        RelicModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        foreach (CardModel card in _removedCards)
        {
            card.AssignOwner(Owner);
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_removedCards.Count);
        foreach (CardModel card in _removedCards)
        {
            var cardBuilder = new CombatStateDescriptionBuilder();
            CombatStateDescription.AppendCard(ref cardBuilder, card, card.CombatState as Combat.CombatState);
            CombatStateDescriptionDigest fingerprint = cardBuilder.Build();
            builder.Append(fingerprint.First);
            builder.Append(fingerprint.Second);
        }
    }
}