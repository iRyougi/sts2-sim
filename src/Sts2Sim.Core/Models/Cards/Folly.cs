using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Folly : CardModel
{
    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedByModifiers => false;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable, CardKeyword.Eternal, CardKeyword.Innate, CardKeyword.Ethereal };
}
