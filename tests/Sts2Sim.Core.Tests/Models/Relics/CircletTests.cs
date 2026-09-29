using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public class CircletTests : IDisposable
{
    public CircletTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight),
            typeof(Circlet), typeof(Vajra),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Circlet_HasNoneRarity_AndIsExcludedFromGrabBagBuckets()
    {
        Assert.Equal(RelicRarity.None, ModelDb.Relic<Circlet>().Rarity);

        var bag = new RelicGrabBag(new Rng(1), ModelDb.All<RelicModel>());
        // The Common bucket should contain only Vajra; Circlet must not enter any bucket.
        RelicModel? first = bag.PullFromFront(RelicRarity.Common);
        RelicModel? second = bag.PullFromFront(RelicRarity.Common);
        Assert.IsType<Vajra>(first);
        Assert.Null(second);
    }
}
