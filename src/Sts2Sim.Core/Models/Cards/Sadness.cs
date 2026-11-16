using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>衍生状态牌 Sadness（原版 <c>MegaCrit.Sts2.Core.Models.Cards.Sadness</c>）。</summary>
public sealed class Sadness : CardModel, ICardChoiceBaseValueProvider
{
    public override bool CanBeGeneratedInCombat => false;
    public override int MaxUpgradeLevel => 0;
    public override CardType Type => CardType.Status;
    public override CardRarity Rarity => CardRarity.Status;
    public override TargetType TargetType => TargetType.None;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Unplayable];
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
}
