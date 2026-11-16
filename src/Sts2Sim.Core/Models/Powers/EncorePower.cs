using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>限制本回合剩余可打出的卡牌数量并在回合结束移除（原版 <c>EncorePower</c>）。</summary>
public sealed class EncorePower : PowerModel
{
    private List<CardModel> _cardsBeingPlayed = new();
    private int _numberOfCardsPlayedSinceApplied;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => Math.Max(0, Amount - _numberOfCardsPlayedSinceApplied);

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card.Owner.Creature, Owner))
        {
            _cardsBeingPlayed.Add(cardPlay.Card);
        }
        return Task.CompletedTask;
    }

    public override bool ShouldPlay(CardModel card, bool isAutoPlay)
    {
        if (!ReferenceEquals(card.Owner.Creature, Owner))
            return true;
        return _numberOfCardsPlayedSinceApplied + _cardsBeingPlayed.Count < Amount;
    }

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card.Owner.Creature, Owner) && _cardsBeingPlayed.Remove(cardPlay.Card))
        {
            _numberOfCardsPlayedSinceApplied++;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _cardsBeingPlayed = new List<CardModel>();
    }

    internal override IEnumerable<CardModel> EnumerateCombatCloneCards() => _cardsBeingPlayed;

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        var sourcePower = (EncorePower)source;
        _numberOfCardsPlayedSinceApplied = sourcePower._numberOfCardsPlayedSinceApplied;
        _cardsBeingPlayed = sourcePower._cardsBeingPlayed.Select(card => cardMap[card]).ToList();
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_numberOfCardsPlayedSinceApplied);
        context.AppendCardReferences(ref builder, _cardsBeingPlayed);
    }
}
