using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Deviation #159: the source-specific modifier exclusion is restored; the simulator also keeps
/// its broader existing <see cref="CanBeGeneratedInCombat"/> exclusion.
/// </summary>
public sealed class PoorSleep : CardModel
{
    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedByModifiers => false;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable, CardKeyword.Retain };
}
