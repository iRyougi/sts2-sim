using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Tests.Commands;

[Collection("ModelDb")]
public sealed class CardCmdUpgradeTests : IDisposable
{
    public CardCmdUpgradeTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(StrikeRegent) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Upgrade_UpgradableCard_BecomesUpgraded()
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();

        CardCmd.Upgrade(card);

        Assert.True(card.IsUpgraded);
    }
}
