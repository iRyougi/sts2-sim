using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Normality : CardModel
{
    protected override bool ShouldGlowRedInternal => ShouldPreventCardPlay;

    private bool ShouldPreventCardPlay => Owner.PlayerCombatState!.CardPlaysStartedThisTurn >= 3;

    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };

    public override bool ShouldPlay(CardModel card, bool isAutoPlay)
    {
        if (!ReferenceEquals(card.Owner, Owner) || Pile?.Type != PileType.Hand)
        {
            return true;
        }

        return Owner.PlayerCombatState!.CardPlaysStartedThisTurn < 3;
    }
}
