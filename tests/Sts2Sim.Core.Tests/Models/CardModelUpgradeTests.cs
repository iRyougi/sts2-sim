using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public class CardModelUpgradeTests : IDisposable
{
    public CardModelUpgradeTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(StrikeRegent) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void NewCard_IsNotUpgraded_AndIsUpgradable()
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();

        Assert.False(card.IsUpgraded);
        Assert.True(card.IsUpgradable);
        Assert.Equal(0, card.CurrentUpgradeLevel);
    }

    [Fact]
    public void Upgrade_SetsIsUpgraded_AndBecomesNotUpgradable()
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();

        card.Upgrade();

        Assert.True(card.IsUpgraded);
        Assert.False(card.IsUpgradable);
        Assert.Equal(1, card.CurrentUpgradeLevel);
    }

    [Fact]
    public void Upgrade_WhenAlreadyUpgraded_Throws()
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        card.Upgrade();

        Assert.Throws<InvalidOperationException>(() => card.Upgrade());
    }
}
