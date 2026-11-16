using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Tests.Helpers;

public class ListExtensionsTests
{
    /// <summary>
    /// v0.113.1（#946）：父流只取一次 NextUnsignedLong 派生子 Rng，Fisher-Yates 全部用子 Rng。
    /// 期望值按原版 ListExtensions.UnstableShuffle 逐行手写。
    /// </summary>
    [Fact]
    public void UnstableShuffle_DerivesChildRngFromOneParentDraw()
    {
        var expected = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
        var oracleParent = new Rng(42UL);
        var child = new Rng(oracleParent.NextUnsignedLong());
        for (int num = expected.Count - 1; num > 0; num--)
        {
            int num2 = child.NextInt(num + 1);
            (expected[num], expected[num2]) = (expected[num2], expected[num]);
        }

        var parent = new Rng(42UL);
        var actual = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
        actual.UnstableShuffle(parent);

        Assert.Equal(expected, actual);
        Assert.Equal(1, parent.Counter);
        Assert.Equal(oracleParent.NextInt(1_000_000), parent.NextInt(1_000_000));
    }

    [Fact]
    public void StableShuffle_IsIndependentOfInitialOrder()
    {
        var listA = new List<int> { 1, 2, 3, 4, 5 };
        var listB = new List<int> { 5, 4, 3, 2, 1 };

        listA.StableShuffle(new Rng(7UL));
        listB.StableShuffle(new Rng(7UL));

        Assert.Equal(listA, listB);
    }

    [Fact]
    public void StableShuffle_IsDeterministic()
    {
        var listA = new List<int> { 1, 2, 3, 4, 5 };
        var listB = new List<int> { 1, 2, 3, 4, 5 };

        listA.StableShuffle(new Rng(99UL));
        listB.StableShuffle(new Rng(99UL));

        Assert.Equal(listA, listB);
    }
}
