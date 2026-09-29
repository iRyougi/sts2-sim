using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Models;

public sealed class CardModelTask11SupportTests
{
    [Fact]
    public void NegativeCanonicalCost_RemainsNegative()
    {
        var card = (NegativeCostCard)new NegativeCostCard().MutableClone();

        Assert.Equal(-1, card.EnergyCost);
    }

}
