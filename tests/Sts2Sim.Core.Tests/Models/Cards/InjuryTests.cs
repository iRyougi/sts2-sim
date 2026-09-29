using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Cards;

public sealed class InjuryTests
{
    [Fact]
    public void Metadata_MatchesSource()
    {
        var card = (Injury)new Injury().MutableClone();

        Assert.Equal(-1, card.EnergyCost);
        Assert.Equal(CardType.Curse, card.Type);
        Assert.Equal(CardRarity.Curse, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(new[] { CardKeyword.Unplayable }, card.Keywords);
        Assert.Equal(0, card.MaxUpgradeLevel);
    }
}
