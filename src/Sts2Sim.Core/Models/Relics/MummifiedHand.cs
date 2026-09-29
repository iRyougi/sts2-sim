using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>木乃伊之手，己方打出Power牌时随机选1张手牌本回合免费；逐字移植
/// （<c>MegaCrit.Sts2.Core.Models.Relics.MummifiedHand</c>）优先选"仍需要花费能量或星愿"的牌，
/// 选不到再任选一张。偏离 #137：真实源码有四级候选池兜底（先按"计入全局费用修饰符后仍需花费"过滤，
/// 选不到再退化到"基础费用非0"，各自都有一层"限定手牌里符合条件的" vs "手牌全体"的子兜底）——
/// 本项目没有费用修饰符叠加链（延续偏离 #32），"计入全局修饰符后的费用"和"基础费用"在本项目下恒等价，
/// 四级合并成两级："优先选仍需花费能量/星愿的牌，选不到则任选一张"，选中概率分布与真实游戏一致。</summary>
public sealed class MummifiedHand : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner ||
            cardPlay.Card.Type != CardType.Power)
        {
            return Task.CompletedTask;
        }

        IReadOnlyList<CardModel> hand = Owner.PlayerCombatState!.Hand.Cards;
        CardModel? selected =
            Owner.RunState.Rng.CombatCardSelection.NextItem(hand.Where(CostsEnergyOrStars))
            ?? Owner.RunState.Rng.CombatCardSelection.NextItem(hand);
        selected?.MakeTemporaryFreeThisTurn();
        return Task.CompletedTask;
    }

    private static bool CostsEnergyOrStars(CardModel card) =>
        card.EnergyCost > 0 || card.CostsXEnergy || card.HasStarCost;
}
