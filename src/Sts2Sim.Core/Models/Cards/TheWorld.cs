using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>随机将弃牌堆至多 5（升级 7）张牌移入手牌并消耗（原版 <c>TheWorld</c>）。</summary>
public sealed class TheWorld : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int Cards => IsUpgraded ? 7 : 5;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int remainingSpace = CardPile.MaxCardsInHand - Owner.PlayerCombatState!.Hand.Cards.Count;
        int count = Math.Max(0, Math.Min(Cards, remainingSpace));
        List<CardModel> picked = Owner.PlayerCombatState.DiscardPile.Cards
            .TakeRandom(count, Owner.RunState.Rng.CombatCardSelection)
            .ToList();
        await CardPileCmd.AddAsync(CombatState!, picked, PileType.Hand);
    }
}
