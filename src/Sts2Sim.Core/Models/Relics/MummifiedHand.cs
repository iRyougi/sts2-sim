using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>木乃伊之手，己方打出Power牌时随机选1张手牌免费。逐字移植
/// （<c>MegaCrit.Sts2.Core.Models.Relics.MummifiedHand</c>）：按基础正费用与实际正费用的
/// 四层候选池依次兜底，能量免费在本回合结束或首次打出时清除。</summary>
public sealed class MummifiedHand : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState?.IsLiveCombat() != true)
        {
            return Task.CompletedTask;
        }

        if (cardPlay.Card.Owner != Owner ||
            cardPlay.Card.Type != CardType.Power)
        {
            return Task.CompletedTask;
        }

        IReadOnlyList<CardModel> hand = Owner.PlayerCombatState!.Hand.Cards;
        List<CardModel> basePositive = hand.Where(card => card.HasPositiveBaseEnergyOrStarCost).ToList();
        CardModel? selected =
            Owner.RunState.Rng.CombatCardSelection.NextItem(basePositive.Where(CostsEnergyOrStars))
            ?? Owner.RunState.Rng.CombatCardSelection.NextItem(hand.Where(CostsEnergyOrStars))
            ?? Owner.RunState.Rng.CombatCardSelection.NextItem(basePositive)
            ?? Owner.RunState.Rng.CombatCardSelection.NextItem(hand);
        selected?.SetToFreeThisTurn();
        return Task.CompletedTask;
    }

    private static bool CostsEnergyOrStars(CardModel card) =>
        (!card.CostsXEnergy && card.EnergyCost > 0) || (!card.CostsXStar && card.StarCost > 0);
}
