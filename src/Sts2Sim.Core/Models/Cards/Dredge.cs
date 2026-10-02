using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Dredge</c>：1 费消耗技能，从弃牌堆选 min(3, 手牌剩余空位) 张放回手牌；升级获得保留。</summary>
public sealed class Dredge : CardModel, ICardChoiceBaseValueProvider
{
    private const int Cards = 3;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int count = Math.Min(Cards, CardPile.MaxCardsInHand - Owner.PlayerCombatState!.Hand.Cards.Count);
        if (count <= 0)
            return;

        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromCombatPile(
            CombatState!, Owner, Owner.PlayerCombatState!.DiscardPile, count, count, this);
        foreach (CardModel card in selected)
            CardPileCmd.Add(card, PileType.Hand);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
