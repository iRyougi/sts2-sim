using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>弃掉所有手牌并抽 7（升级 9）张牌（手牌为空时直接抽牌），消耗（原版 <c>WheelOfFortune</c>）。</summary>
public sealed class WheelOfFortune : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int Cards => IsUpgraded ? 9 : 7;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IReadOnlyList<CardModel> hand = Owner.PlayerCombatState!.Hand.Cards;
        if (hand.Count > 0)
        {
            await CardCmd.DiscardAndDraw(hand.ToArray(), Cards);
        }
        else
        {
            await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
        }
    }
}
