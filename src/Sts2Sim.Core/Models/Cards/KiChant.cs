using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>检视抽牌堆顶 N 张牌，选 1 张入手并弃掉其余（原版 <c>KiChant</c>）。</summary>
public sealed class KiChant : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Ki];
    private int Cards => IsUpgraded ? 4 : 3;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int count = Cards;
        CardPile drawPile = Owner.PlayerCombatState!.DrawPile;
        if (drawPile.Cards.Count == 0)
        {
            await CardPileCmd.ShuffleIfNecessary(CombatState!, Owner);
        }
        else if (drawPile.Cards.Count < count && Owner.PlayerCombatState.DiscardPile.Cards.Any())
        {
            List<CardModel> alreadyInDraw = drawPile.Cards.ToList();
            await CardPileCmd.Shuffle(CombatState!, Owner);
            foreach (CardModel existing in alreadyInDraw.Where(c => ReferenceEquals(c.Pile, drawPile)).Reverse())
            {
                CardPileCmd.Add(existing, PileType.Draw, CardPilePosition.Top);
            }
        }

        List<CardModel> topCards = drawPile.Cards.Take(count).ToList();
        if (topCards.Count == 0)
            return;

        IReadOnlyList<CardModel> picked = await CardSelectCmd.FromCombatPile(
            CombatState!,
            Owner,
            drawPile,
            1,
            1,
            this,
            topCards.Contains,
            cancelable: false);
        foreach (CardModel item in picked)
        {
            CardPileCmd.Add(item, PileType.Hand);
        }

        List<CardModel> remaining = topCards.Except(picked).Where(c => ReferenceEquals(c.Pile, drawPile)).ToList();
        if (remaining.Count > 0)
        {
            await CardCmd.Discard(remaining);
        }
    }
}
