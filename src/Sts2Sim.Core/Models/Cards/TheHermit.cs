using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>从手牌选 1 张牌弃置（诅咒牌则消耗），再抽 2（升级 3）张牌并消耗（原版 <c>TheHermit</c>）。</summary>
public sealed class TheHermit : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int Cards => IsUpgraded ? 3 : 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHandForDiscard(CombatState!, Owner, 1, this);
        foreach (CardModel card in selected)
        {
            if (card.Type == CardType.Curse)
            {
                await CardPileCmd.Exhaust(CombatState!, card);
            }
            else
            {
                await CardCmd.Discard(card);
            }
        }

        await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
    }
}
