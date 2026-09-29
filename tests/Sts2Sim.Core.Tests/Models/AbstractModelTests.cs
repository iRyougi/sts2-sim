namespace Sts2Sim.Core.Tests.Models;

using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Exceptions;

[Collection("ModelDb")]
public class AbstractModelTests
{
    // 直接继承 AbstractModel 的类型,category 就是它自己
    private sealed class GoldenAmulet : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => false;

        public List<int> Counters { get; private set; } = new();

        protected override void DeepCloneFields()
        {
            base.DeepCloneFields();
            Counters = new List<int>(Counters);
        }
    }

    private sealed class SilverAmulet : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => false;
    }

    [Fact]
    public void Id_DerivesCategoryAndEntryFromTypeName()
    {
        var model = new GoldenAmulet();
        Assert.Equal("GOLDEN_AMULET", model.Id.Category);
        Assert.Equal("GOLDEN_AMULET", model.Id.Entry);
    }

    [Fact]
    public void FreshModel_IsCanonical()
    {
        var model = new GoldenAmulet();
        Assert.True(model.IsCanonical);
        Assert.False(model.IsMutable);
        Assert.Throws<CanonicalModelException>(() => model.AssertMutable());
        model.AssertCanonical(); // 不抛
    }

    [Fact]
    public void MutableClone_IsMutableAndDeepCopiesFields()
    {
        var canonical = new GoldenAmulet();
        canonical.Counters.Add(1);

        var clone = (GoldenAmulet)canonical.MutableClone();
        Assert.True(clone.IsMutable);
        Assert.Throws<MutableModelException>(() => clone.AssertCanonical());
        clone.AssertMutable(); // 不抛

        clone.Counters.Add(2);
        Assert.Equal(new[] { 1 }, canonical.Counters);      // 原件不受影响
        Assert.Equal(new[] { 1, 2 }, clone.Counters);
        Assert.Equal(canonical.Id, clone.Id);
    }

    [Fact]
    public void ClonePreservingMutability_ReturnsSameInstanceForCanonical()
    {
        var canonical = new GoldenAmulet();
        Assert.Same(canonical, canonical.ClonePreservingMutability());
    }

    [Fact]
    public void ClonePreservingMutability_ClonesMutable()
    {
        var mutable = new GoldenAmulet().MutableClone();
        var second = mutable.ClonePreservingMutability();
        Assert.NotSame(mutable, second);
        Assert.True(second.IsMutable);
    }

    [Fact]
    public void ExecutionFinished_FiresOnInvoke_AndIsClearedOnClone()
    {
        var model = new GoldenAmulet();
        int fired = 0;
        model.ExecutionFinished += _ => fired++;
        model.InvokeExecutionFinished();
        Assert.Equal(1, fired);

        var clone = model.MutableClone();
        clone.InvokeExecutionFinished(); // 克隆体的订阅被 AfterCloned 清空
        Assert.Equal(1, fired);

        int cloneFired = 0;
        clone.ExecutionFinished += _ => cloneFired++;
        clone.InvokeExecutionFinished();
        Assert.Equal(1, cloneFired);
        Assert.Equal(1, fired); // 原件订阅不受克隆体触发影响
    }

    [Fact]
    public void CompareTo_OrdersById()
    {
        var golden = new GoldenAmulet();
        var silver = new SilverAmulet();
        // GOLDEN_AMULET < SILVER_AMULET(ordinal)
        Assert.True(golden.CompareTo(silver) < 0);
        Assert.Equal(0, golden.CompareTo(golden));
        Assert.True(golden.CompareTo(null) > 0);
    }

    [Fact]
    public void Constructor_ThrowsWhenTypeAlreadyRegistered()
    {
        try
        {
            ModelDb.Inject(typeof(GoldenAmulet));
            Assert.Throws<DuplicateModelException>(() => new GoldenAmulet());
        }
        finally
        {
            ModelDb.ResetForTests();
        }
    }
}
