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
file sealed class LadderCommonRelic : RelicModel { public override RelicRarity Rarity => RelicRarity.Common; }
file sealed class LadderUncommonRelic : RelicModel { public override RelicRarity Rarity => RelicRarity.Uncommon; }
file sealed class LadderRareRelic : RelicModel { public override RelicRarity Rarity => RelicRarity.Rare; }
file sealed class LadderShopRelic : RelicModel { public override RelicRarity Rarity => RelicRarity.Shop; }
file sealed class DisallowedCommonRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;
    protected override bool IsAllowedInternal(IRunState runState) => false;
}
file sealed class DisallowedUncommonRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    protected override bool IsAllowedInternal(IRunState runState) => false;
}
file sealed class DisallowedRareRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    protected override bool IsAllowedInternal(IRunState runState) => false;
}
file sealed class DisallowedShopRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    protected override bool IsAllowedInternal(IRunState runState) => false;
}

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
    public void PullFromBack_ShopFilterRejectsOnlyRelic_ReturnsNullWithoutConsumingRelic()
    {
        var forbidden = new ShopForbiddenCommonRelic();
        var bag = new RelicGrabBag(new Rng(3), new[] { forbidden });
        var runState = new FakeRunState();

        Assert.Null(bag.PullFromBack(RelicRarity.Common, relic => relic.IsAllowedInShops, runState));
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

    /// <summary>#1120：原生 <c>GetAvailableDeque</c>——先永久剔除不允许的遗物；请求档为空且允许 refresh 时
    /// 按原始顺序补回；仍无符合项则按 Shop→Common→Uncommon→Rare 降级，Rare 之后为 null（调用方给 Circlet）。</summary>
    [Theory]
    [InlineData(RelicRarity.Shop, false, RelicRarity.Uncommon)]
    [InlineData(RelicRarity.Common, false, RelicRarity.Uncommon)]
    [InlineData(RelicRarity.Uncommon, false, RelicRarity.Rare)]
    [InlineData(RelicRarity.Rare, false, RelicRarity.None)]
    [InlineData(RelicRarity.Common, true, RelicRarity.Common)]
    public void PullFromFront_EmptiedBucket_RefreshesThenDowngradesInNativeOrder(
        RelicRarity requested, bool refreshAllowed, RelicRarity expected)
    {
        // 商店档：Common 只放一件不可上架的遗物，锁住"降级判定也按 IsAllowedInShops 过滤"（应越过 Common 到 Uncommon）。
        RelicModel common = requested == RelicRarity.Shop ? new ShopForbiddenCommonRelic() : new LadderCommonRelic();
        RelicModel[] ladder = { common, new LadderUncommonRelic(), new LadderRareRelic(), new LadderShopRelic() };
        RelicModel disallowed = requested switch
        {
            RelicRarity.Common => new DisallowedCommonRelic(),
            RelicRarity.Uncommon => new DisallowedUncommonRelic(),
            RelicRarity.Rare => new DisallowedRareRelic(),
            _ => new DisallowedShopRelic(),
        };
        var bag = new RelicGrabBag(new Rng(10), ladder.Append(disallowed).ToArray(), refreshAllowed: refreshAllowed);
        var run = new FakeRunState();
        bool fromBack = requested == RelicRarity.Shop;
        RelicModel? Pull() => fromBack
            ? bag.PullFromBack(requested, relic => relic.IsAllowedInShops, run)
            : bag.PullFromFront(requested, run);

        RelicModel? first = Pull();
        Assert.Equal(requested, first?.Rarity);
        RelicModel? second = Pull();

        string context = $"requested={requested} refresh={refreshAllowed}";
        Assert.DoesNotContain(
            bag.ExportForTransplant().Buckets.Values.SelectMany(ids => ids),
            id => id == disallowed.Id.ToString());
        if (expected == RelicRarity.None)
            Assert.True(second is null, $"{context}: expected null, got {second?.Id}");
        else
            Assert.True(second?.Rarity == expected, $"{context}: expected {expected}, got {second?.Id}");
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
