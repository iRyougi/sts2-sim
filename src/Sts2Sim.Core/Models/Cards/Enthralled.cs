using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Enthralled : CardModel
{
    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedByModifiers => false;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Eternal };

    public override bool ShouldPlay(CardModel card, bool isAutoPlay)
    {
        if (!ReferenceEquals(card.Owner, Owner) || Pile?.Type != PileType.Hand)
        {
            return true;
        }

        if (card is Enthralled)
        {
            return true;
        }

        return isAutoPlay;
    }
}
