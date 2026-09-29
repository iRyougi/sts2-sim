using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// The source forbids both modifier and in-combat generation.
/// </summary>
public sealed class AscendersBane : CardModel
{
    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedByModifiers => false;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Eternal, CardKeyword.Unplayable, CardKeyword.Ethereal };
}
