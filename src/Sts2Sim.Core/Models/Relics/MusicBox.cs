using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MusicBox : RelicModel
{
    private bool _wasUsedThisTurn;
    private CardModel? _cardBeingPlayed;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (_cardBeingPlayed is null && !_wasUsedThisTurn && cardPlay.Card.Owner == Owner && cardPlay.Card.Type == CardType.Attack)
            _cardBeingPlayed = cardPlay.Card;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card, _cardBeingPlayed) || Owner.Creature.CombatState is null) return;
        var copy = (CardModel)cardPlay.Card.MutableClone();
        copy.AssignOwner(Owner);
        copy.AddKeywordInternal(CardKeyword.Ethereal);
        await CardPileCmd.Generate(Owner.Creature.CombatState, copy, PileType.Hand, Owner);
        _wasUsedThisTurn = true;
        _cardBeingPlayed = null;
    }

    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Entities.Creatures.Creature> participants)
    {
        if (participants.Contains(Owner.Creature)) { _wasUsedThisTurn = false; _cardBeingPlayed = null; }
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd() { _wasUsedThisTurn = false; _cardBeingPlayed = null; return Task.CompletedTask; }

    internal override void RestoreCombatCloneReferencesFrom(
        RelicModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        if (source is MusicBox { _cardBeingPlayed: { } original } && cardMap.TryGetValue(original, out CardModel? clone))
            _cardBeingPlayed = clone;
    }

    internal override void AppendCombatStateDescription(ref Combat.StateDescription.CombatStateDescriptionBuilder builder, Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_wasUsedThisTurn);
        context.AppendCardReferences(ref builder, [_cardBeingPlayed]);
    }
}
