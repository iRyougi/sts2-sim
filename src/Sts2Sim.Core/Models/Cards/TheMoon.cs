using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>生成小刀直到填满手牌；若本牌已升级则将生成的小刀升级（原版 <c>TheMoon</c>）。</summary>
public sealed class TheMoon : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int count = Math.Max(0, CardPile.MaxCardsInHand - Owner.PlayerCombatState!.Hand.Cards.Count);
        IEnumerable<CardModel> generated = await Shiv.CreateInHand(Owner, count, CombatState!);
        if (!IsUpgraded)
            return;

        foreach (CardModel card in generated)
        {
            if (card.IsUpgradable)
            {
                card.Upgrade();
            }
        }
    }
}
