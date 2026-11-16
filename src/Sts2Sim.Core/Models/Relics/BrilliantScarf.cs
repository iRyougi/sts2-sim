using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BrilliantScarf : RelicModel
{
    private int _cardsPlayed;
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ShouldModifyCost(card)) return false;
        modifiedCost = 0m;
        return true;
    }
    public override bool TryModifyStarCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ShouldModifyCost(card)) return false;
        modifiedCost = 0m;
        return true;
    }
    // v0.113.1：CardsVar 5→6，第 6 张免费；原文还要求牌在手牌或出牌堆。
    private const int CardsThreshold = 6;
    private bool ShouldModifyCost(CardModel card) =>
        card.CombatState is Combat.CombatState { } combat && combat.IsLiveCombat() &&
        card.Owner.Creature == Owner.Creature && _cardsPlayed == CardsThreshold - 1 &&
        card.Pile?.Type is PileType.Hand or PileType.Play;
    public override Task AfterCardPlayed(CardPlay cardPlay) { if (cardPlay.Card.Owner == Owner && !cardPlay.IsAutoPlay) _cardsPlayed++; return Task.CompletedTask; }
    public override Task BeforeSideTurnStart(Combat.CombatSide side, IReadOnlyList<Entities.Creatures.Creature> participants) { if (participants.Contains(Owner.Creature)) _cardsPlayed = 0; return Task.CompletedTask; }
    public override Task AfterCombatEnd() { _cardsPlayed = 0; return Task.CompletedTask; }
    internal override void AppendCombatStateDescription(ref Combat.StateDescription.CombatStateDescriptionBuilder builder, Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(_cardsPlayed);
}
