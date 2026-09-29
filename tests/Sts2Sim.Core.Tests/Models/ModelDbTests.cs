namespace Sts2Sim.Core.Tests.Models;

using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Exceptions;

[CollectionDefinition("ModelDb")]
public class ModelDbCollection
{
}

[Collection("ModelDb")]
public class ModelDbTests
{
    private sealed class TestTrinket : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => false;
    }

    private abstract class WidgetModel : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => false;
    }

    private sealed class BlueWidget : WidgetModel
    {
    }

    [Fact]
    public void GetId_UsesCategoryRootForSubclassHierarchies()
    {
        // BlueWidget → WidgetModel → AbstractModel:category 取 AbstractModel 的直接子类 WidgetModel
        var id = ModelDb.GetId<BlueWidget>();
        Assert.Equal("WIDGET", id.Category);
        Assert.Equal("BLUE_WIDGET", id.Entry);
    }

    [Fact]
    public void Init_RegistersCanonicalInstances_AndIsIdempotent()
    {
        try
        {
            ModelDb.Init(new[] { typeof(TestTrinket), typeof(BlueWidget) });
            ModelDb.Init(new[] { typeof(TestTrinket) }); // 重复 Init 不抛、不重建

            var trinket = ModelDb.GetById<TestTrinket>(ModelDb.GetId<TestTrinket>());
            Assert.True(trinket.IsCanonical);
            Assert.Same(trinket, ModelDb.GetById<TestTrinket>(ModelDb.GetId<TestTrinket>()));
        }
        finally
        {
            ModelDb.ResetForTests();
        }
    }

    [Fact]
    public void GetById_UnknownId_Throws_GetByIdOrNull_ReturnsNull()
    {
        var unknown = new ModelId("WIDGET", "MISSING");
        Assert.Null(ModelDb.GetByIdOrNull<WidgetModel>(unknown));
        Assert.Throws<ModelNotFoundException>(() => ModelDb.GetById<WidgetModel>(unknown));
    }

    [Fact]
    public void Contains_ReflectsRegistration()
    {
        try
        {
            Assert.False(ModelDb.Contains(typeof(TestTrinket)));
            ModelDb.Inject(typeof(TestTrinket));
            Assert.True(ModelDb.Contains(typeof(TestTrinket)));
        }
        finally
        {
            ModelDb.ResetForTests();
        }
    }

    [Fact]
    public void Inject_NonModelType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ModelDb.Inject(typeof(string)));
        Assert.Throws<ArgumentException>(() => ModelDb.Inject(typeof(WidgetModel)));
    }

    private static class ContainerA
    {
        public sealed class CollisionVictim : AbstractModel
        {
            public override bool ShouldReceiveCombatHooks => false;
        }
    }

    private static class ContainerB
    {
        public sealed class CollisionVictim : AbstractModel
        {
            public override bool ShouldReceiveCombatHooks => false;
        }
    }

    [Fact]
    public void Inject_ModelIdCollision_Throws()
    {
        try
        {
            ModelDb.Inject(typeof(ContainerA.CollisionVictim));
            Assert.Throws<InvalidOperationException>(() => ModelDb.Inject(typeof(ContainerB.CollisionVictim)));
        }
        finally
        {
            ModelDb.ResetForTests();
        }
    }
}
