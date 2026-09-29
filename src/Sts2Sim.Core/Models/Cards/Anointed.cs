using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust,把抽牌堆中Rare稀有度卡补满手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Anointed</c>）：
/// 不经过玩家选择,直接随机取够数量的 Rare 卡加入手牌。升级后追加 Retain（不是移除 Exhaust）。</summary>
public sealed class Anointed : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int count = CardPile.MaxCardsInHand - Owner.PlayerCombatState!.Hand.Cards.Count;
        List<CardModel> rareCards = Owner.PlayerCombatState!.DrawPile.Cards.Where(c => c.Rarity == CardRarity.Rare).ToList();
        CombatState!.RunState.Rng.CombatCardSelection.Shuffle(rareCards);
        foreach (CardModel card in rareCards.Take(count))
        {
            CardPileCmd.Add(card, PileType.Hand);
        }

        await Task.CompletedTask;
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
