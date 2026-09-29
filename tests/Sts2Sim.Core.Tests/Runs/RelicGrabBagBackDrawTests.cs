using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

file sealed class BackDrawCommonRelicA : RelicModel { public override RelicRarity Rarity => RelicRarity.Common; }
file sealed class BackDrawCommonRelicB : RelicModel { public override RelicRarity Rarity => RelicRarity.Common; }
file sealed class BackDrawCommonRelicC : RelicModel { public override RelicRarity Rarity => RelicRarity.Common; }

[Collection("ModelDb")]
public class RelicGrabBagBackDrawTests : IDisposable
{
    public RelicGrabBagBackDrawTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(BackDrawCommonRelicA), typeof(BackDrawCommonRelicB), typeof(BackDrawCommonRelicC) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void PullFromBack_ReturnsLastItemOfIndependentlyShuffledBucket()
    {
        const ulong shuffleSeed = 7u;
        List<RelicModel> pool = ModelDb.All<RelicModel>().ToList();
        List<RelicModel> expectedOrder = pool.Where(relic => relic.Rarity == RelicRarity.Common).ToList();
        new Rng(shuffleSeed).Shuffle(expectedOrder);
        var bag = new RelicGrabBag(new Rng(shuffleSeed), pool);

        RelicModel? fromFront = bag.PullFromFront(RelicRarity.Common);
        RelicModel? fromBack = bag.PullFromBack(RelicRarity.Common);
        RelicModel? remaining = bag.PullFromFront(RelicRarity.Common);

        Assert.Same(expectedOrder[0], fromFront);
        Assert.Same(expectedOrder[^1], fromBack);
        Assert.Same(expectedOrder[1], remaining);
        Assert.Null(bag.PullFromFront(RelicRarity.Common));
    }
}
