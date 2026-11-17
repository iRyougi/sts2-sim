using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>消耗全部手牌，并从抽牌堆顶自动打出等量张牌；升级时打出前先升级（原版 <c>TheDevil</c>）。</summary>
public sealed class TheDevil : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        List<CardModel> cards = Owner.PlayerCombatState!.Hand.Cards.ToList();
        foreach (CardModel card in cards)
        {
            await CardPileCmd.Exhaust(CombatState!, card);
        }

        await AutoPlayCmd.FromTopOfDrawPile(
            CombatState!,
            Owner,
            cards.Count,
            forceExhaust: false,
            cardModification: c =>
            {
                if (IsUpgraded && c.IsUpgradable)
                {
                    c.Upgrade();
                }
            });
    }
}
