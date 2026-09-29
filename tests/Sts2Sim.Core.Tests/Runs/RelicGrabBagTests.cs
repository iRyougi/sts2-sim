using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

file sealed class FakeCommonRelicA : RelicModel { public override RelicRarity Rarity => RelicRarity.Common; }
file sealed class FakeCommonRelicB : RelicModel { public override RelicRarity Rarity => RelicRarity.Common; }
file sealed class FakeUncommonRelic : RelicModel { public override RelicRarity Rarity => RelicRarity.Uncommon; }
file sealed class ShopForbiddenCommonRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override bool IsAllowedInShops => false;
}
file sealed class FakeStarterRelic : RelicModel { public override RelicRarity Rarity => RelicRarity.Starter; }

public class RelicGrabBagTests : IDisposable
{
    public RelicGrabBagTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(FakeCommonRelicA), typeof(FakeCommonRelicB), typeof(FakeUncommonRelic), typeof(FakeStarterRelic) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void PullFromFront_ExcludesStarterRarity_AndNeverRepeatsWithinARun()
    {
        var bag = new RelicGrabBag(new Rng(1), ModelDb.All<RelicModel>());

        RelicModel? first = bag.PullFromFront(RelicRarity.Common);
        RelicModel? second = bag.PullFromFront(RelicRarity.Common);
        RelicModel? third = bag.PullFromFront(RelicRarity.Common);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first!.GetType(), second!.GetType());
        Assert.Null(third);
    }

    [Fact]
    public void PullFromFront_UnknownRarityBucket_ReturnsNull()
    {
        var bag = new RelicGrabBag(new Rng(2), ModelDb.All<RelicModel>());

        Assert.Null(bag.PullFromFront(RelicRarity.Rare));
    }

    [Fact]
    public void PullForShop_ForbiddenOnlyBucket_ReturnsNullWithoutConsumingRelic()
    {
        var forbidden = new ShopForbiddenCommonRelic();
        var bag = new RelicGrabBag(new Rng(3), new[] { forbidden });
        var runState = new FakeRunState();

        Assert.Null(bag.PullForShop(RelicRarity.Common, runState));
        Assert.Same(forbidden, bag.PullFromFront(RelicRarity.Common));
    }
    [Fact]
    public void HasAvailableRelics_EmptyRequestedBucket_ReturnsFalse()
    {
        var bag = new RelicGrabBag(new Rng(4), Array.Empty<RelicModel>());

        Assert.False(bag.HasAvailableRelics(RelicRarity.Common));
    }

    [Fact]
    public void HasAvailableRelics_PopulatedRequestedBucket_ReturnsTrue()
    {
        var bag = new RelicGrabBag(new Rng(5), new[] { (RelicModel)ModelDb.Relic<FakeCommonRelicA>().MutableClone() });

        Assert.True(bag.HasAvailableRelics(RelicRarity.Common));
    }

    [Fact]
    public void HasAvailableRelics_DoesNotConsumeRelic_AndBecomesFalseAfterPull()
    {
        var bag = new RelicGrabBag(new Rng(6), new[] { (RelicModel)ModelDb.Relic<FakeCommonRelicA>().MutableClone() });

        Assert.True(bag.HasAvailableRelics(RelicRarity.Common));
        Assert.NotNull(bag.PullFromFront(RelicRarity.Common));
        Assert.False(bag.HasAvailableRelics(RelicRarity.Common));
    }

    [Fact]
    public void HasAvailableRelics_UnsupportedRarity_ReturnsFalse()
    {
        var bag = new RelicGrabBag(new Rng(7), new[] { (RelicModel)ModelDb.Relic<FakeStarterRelic>().MutableClone() });

        Assert.False(bag.HasAvailableRelics(RelicRarity.Starter));
    }

    [Fact]
    public void HasAvailableRelics_DifferentPopulatedRarity_DoesNotMakeRequestedBucketAvailable()
    {
        var bag = new RelicGrabBag(new Rng(8), new[] { (RelicModel)ModelDb.Relic<FakeUncommonRelic>().MutableClone() });

        Assert.False(bag.HasAvailableRelics(RelicRarity.Common));
    }
    [Fact]
    public void HasAvailableRelics_DoesNotChangeCommonPullOrder()
    {
        var subject = new RelicGrabBag(new Rng(9), CreateCommonRelics());
        var control = new RelicGrabBag(new Rng(9), CreateCommonRelics());

        Assert.True(subject.HasAvailableRelics(RelicRarity.Common));
        Assert.Equal(
            control.PullFromFront(RelicRarity.Common)?.GetType(),
            subject.PullFromFront(RelicRarity.Common)?.GetType());

        Assert.True(subject.HasAvailableRelics(RelicRarity.Common));
        Assert.Equal(
            control.PullFromFront(RelicRarity.Common)?.GetType(),
            subject.PullFromFront(RelicRarity.Common)?.GetType());

        Assert.False(subject.HasAvailableRelics(RelicRarity.Common));
        Assert.Null(subject.PullFromFront(RelicRarity.Common));
        Assert.Null(control.PullFromFront(RelicRarity.Common));
    }

    private static IEnumerable<RelicModel> CreateCommonRelics() =>
        new RelicModel[]
        {
            (RelicModel)ModelDb.Relic<FakeCommonRelicA>().MutableClone(),
            (RelicModel)ModelDb.Relic<FakeCommonRelicB>().MutableClone(),
        };

    private sealed class FakeRunState : IRunState
    {
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            Array.Empty<AbstractModel>();

        public RunRngSet Rng { get; } = new("relic-grab-bag-tests");
        public AscensionManager Ascension { get; } = new(0);
        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
        public AbstractRoom? CurrentRoom => null;
    }
}
