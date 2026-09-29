using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Models.Cards;

internal abstract class KeywordTestCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;
}

internal sealed class NormalKeywordTestCard : KeywordTestCard;

internal sealed class RetainKeywordTestCard : KeywordTestCard
{
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };
}

internal sealed class EtherealKeywordTestCard : KeywordTestCard
{
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Ethereal };
}

internal sealed class InnateKeywordTestCard : KeywordTestCard
{
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Innate };
}
