namespace Sts2Sim.Core.Tests.Random;

using System.Linq;
using Sts2Sim.Core.Random;

public class RngCloneSemanticsTests
{
    private static int[] Draw(Rng rng, int count) =>
        Enumerable.Range(0, count).Select(_ => rng.NextInt(1_000_000)).ToArray();

    [Fact]
    public void CloneExact_ReproducesIdenticalSequenceAndCounter()
    {
        var source = new Rng(12345uL);
        for (int i = 0; i < 7; i++)
        {
            source.NextInt(100);
        }

        Rng clone = source.CloneExact();

        Assert.Equal(source.Counter, clone.Counter);
        Assert.Equal(Draw(source, 20), Draw(clone, 20));
    }

    [Fact]
    public void CloneReseeded_ResetsCounterToZero()
    {
        var source = new Rng(12345uL);
        for (int i = 0; i < 7; i++)
        {
            source.NextInt(100);
        }

        Rng branch = source.CloneReseeded(999uL);

        Assert.Equal(7, source.Counter);
        Assert.Equal(0, branch.Counter);
    }

    /// <summary>
    /// 这是 Plan08b 设计简报 A.3 描述的那个 bug 的回归守卫：战略层 playout 若照搬精确克隆语义，
    /// 100 次 playout 会得到 100 条几乎相同的轨迹，方差估计完全失效，且不会报错。
    /// </summary>
    [Fact]
    public void CloneReseeded_DifferentSeeds_ProduceDifferentSequences()
    {
        var source = new Rng(12345uL);

        Rng first = source.CloneReseeded(1uL);
        Rng second = source.CloneReseeded(2uL);

        Assert.NotEqual(Draw(first, 32), Draw(second, 32));
    }

    [Fact]
    public void CloneReseeded_SameSeed_IsDeterministic()
    {
        var source = new Rng(12345uL);

        Rng first = source.CloneReseeded(777uL);
        Rng second = source.CloneReseeded(777uL);

        Assert.Equal(Draw(first, 20), Draw(second, 20));
    }

    [Fact]
    public void CloneReseeded_WithStreamName_DerivesDistinctStreamsFromOneBranchSeed()
    {
        var source = new Rng(12345uL);

        Rng shuffle = source.CloneReseeded(4242uL, "shuffle");
        Rng monsterAi = source.CloneReseeded(4242uL, "monster_ai");

        Assert.NotEqual(Draw(shuffle, 32), Draw(monsterAi, 32));
    }

    [Fact]
    public void CloneReseeded_DoesNotAdvanceSource()
    {
        var source = new Rng(12345uL);
        var reference = new Rng(12345uL);

        source.CloneReseeded(555uL);

        Assert.Equal(source.Counter, reference.Counter);
        Assert.Equal(Draw(reference, 10), Draw(source, 10));
    }
}
