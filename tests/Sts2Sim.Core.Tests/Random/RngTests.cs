namespace Sts2Sim.Core.Tests.Random;

using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Saves;

public class RngTests
{
    private static void AssertSameState(SerializableRng a, SerializableRng b)
    {
        Assert.Equal(a.counter, b.counter);
        Assert.Equal(a.state0, b.state0);
        Assert.Equal(a.state1, b.state1);
        Assert.Equal(a.state2, b.state2);
        Assert.Equal(a.state3, b.state3);
    }

    [Fact]
    public void NamedConstructor_DerivesSeedFromXxHash()
    {
        var named = new Rng(0uL, "act_1_map");
        var manual = new Rng(StringHelper.GetDeterministicHashCode("act_1_map"));
        AssertSameState(named.ToSerializable(), manual.ToSerializable());
    }

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new Rng(12345uL);
        var b = new Rng(12345uL);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(a.NextInt(1000), b.NextInt(1000));
        }
    }

    [Fact]
    public void Counter_IncrementsPerDraw()
    {
        var rng = new Rng(1uL);
        Assert.Equal(0, rng.Counter);
        rng.NextBool();
        rng.NextInt(10);
        rng.NextInt(1, 10);
        rng.NextFloat();
        rng.NextDouble();
        rng.NextUnsignedInt(10u);
        rng.NextUnsignedLong(10uL);
        Assert.Equal(7, rng.Counter);
    }

    [Fact]
    public void Gaussian_IncrementsCounterPerUnderlyingDraw()
    {
        // v0.109: NextGaussianDouble samples via the public NextDouble() (2 calls per
        // rejection-sampling iteration), so Counter advances by an even number >= 2 —
        // no longer the old "+1 regardless of iterations" behavior.
        var rng = new Rng(31337uL);
        int before = rng.Counter;
        rng.NextGaussianDouble(0.5, 0.2, 0.0, 1.0);
        int delta = rng.Counter - before;
        Assert.True(delta >= 2, $"expected delta >= 2, got {delta}");
        Assert.Equal(0, delta % 2);
    }

    [Fact]
    public void NextGaussianDouble_MeanOutOfRange_Throws()
    {
        var rng = new Rng(1uL);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextGaussianDouble(mean: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextGaussianDouble(mean: 1.1));
    }

    [Fact]
    public void NextGaussianDouble_MinGreaterThanMax_Throws()
    {
        var rng = new Rng(1uL);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextGaussianDouble(min: 1.0, max: 0.0));
    }

    [Fact]
    public void NextGaussianInt_InvalidArgs_Throws()
    {
        var rng = new Rng(1uL);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextGaussianInt(mean: 50, stdDev: 10, min: 100, max: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextGaussianInt(mean: -5, stdDev: 10, min: 0, max: 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextGaussianInt(mean: 200, stdDev: 10, min: 0, max: 100));
    }

    [Fact]
    public void SerializeRestore_RoundTrips()
    {
        var original = new Rng(777uL);
        for (int i = 0; i < 25; i++)
        {
            original.NextInt(1000);
        }
        var saved = original.ToSerializable();

        var restored = new Rng(saved);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(original.NextInt(1000), restored.NextInt(1000));
        }

        // LoadFromSerializable can roll an advanced stream back to the saved point.
        var reference = new Rng(saved);
        var live = new Rng(saved);
        for (int i = 0; i < 10; i++)
        {
            live.NextInt(1000); // advance past the save point
        }
        live.LoadFromSerializable(saved);
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(reference.NextInt(1000), live.NextInt(1000));
        }
    }

    [Fact]
    public void Clone_IsExactAndIndependent()
    {
        var original = new Rng(31337uL);
        original.NextGaussianDouble(0.5, 0.2, 0.0, 1.0);
        var saved = original.ToSerializable();

        var clone = original.CloneExact();
        AssertSameState(saved, clone.ToSerializable());

        // Advancing the original after cloning must not affect the clone.
        original.NextInt(1_000_000);
        var reference = new Rng(saved);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(reference.NextInt(1_000_000), clone.NextInt(1_000_000));
        }
    }

    [Fact]
    public void Shuffle_IsDeterministic()
    {
        var a = new Rng(2024uL);
        var b = new Rng(2024uL);
        var listA = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var listB = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        a.Shuffle(listA);
        b.Shuffle(listB);
        Assert.Equal(listA, listB);
        Assert.Equal(9, a.Counter); // Fisher-Yates: n-1 次交换
    }

    [Fact]
    public void NextInt_InvalidRange_Throws()
    {
        var rng = new Rng(1uL);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(6, 5));
    }

    [Fact]
    public void NextItem_EmptyCollection_ReturnsDefault()
    {
        var rng = new Rng(1uL);
        Assert.Null(rng.NextItem(Array.Empty<string>()));
        Assert.Equal(0, rng.NextItem(Array.Empty<int>()));
    }

    [Fact]
    public void WeightedNextItem_StaticOverload_IsPureAndDeterministic()
    {
        string[] items = { "a", "b", "c" };
        // 权重 a=1, b=2, c=7；randInput=0.05 → 0.5 落在 a；0.25 → 2.5 落在 b；0.5 → 5.0 落在 c
        Assert.Equal("a", Rng.WeightedNextItem(0.05f, items, _ => _ == "a" ? 1f : _ == "b" ? 2f : 7f, "x"));
        Assert.Equal("b", Rng.WeightedNextItem(0.25f, items, _ => _ == "a" ? 1f : _ == "b" ? 2f : 7f, "x"));
        Assert.Equal("c", Rng.WeightedNextItem(0.5f, items, _ => _ == "a" ? 1f : _ == "b" ? 2f : 7f, "x"));
    }
}
