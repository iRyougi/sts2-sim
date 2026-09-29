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
        if (card.Owner == Owner && _cardsPlayed == 4) { modifiedCost = 0m; return true; }
        return false;
    }
    public override bool TryModifyStarCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner == Owner && _cardsPlayed == 4) { modifiedCost = 0m; return true; }
        return false;
    }
    public override Task AfterCardPlayed(CardPlay cardPlay) { if (cardPlay.Card.Owner == Owner && !cardPlay.IsAutoPlay) _cardsPlayed++; return Task.CompletedTask; }
    public override Task BeforeSideTurnStart(Combat.CombatSide side, IReadOnlyList<Entities.Creatures.Creature> participants) { if (participants.Contains(Owner.Creature)) _cardsPlayed = 0; return Task.CompletedTask; }
    public override Task AfterCombatEnd() { _cardsPlayed = 0; return Task.CompletedTask; }
    internal override void AppendCombatStateDescription(ref Combat.StateDescription.CombatStateDescriptionBuilder builder, Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(_cardsPlayed);
}
