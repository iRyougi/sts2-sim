using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Tests.Helpers;

public class ListExtensionsTests
{
    [Fact]
    public void UnstableShuffle_MatchesRngShuffle_ForSameSeed()
    {
        var listA = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
        var listB = new List<int> { 1, 2, 3, 4, 5, 6, 7 };

        new Rng(42UL).Shuffle(listA);
        listB.UnstableShuffle(new Rng(42UL));

        Assert.Equal(listA, listB);
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
