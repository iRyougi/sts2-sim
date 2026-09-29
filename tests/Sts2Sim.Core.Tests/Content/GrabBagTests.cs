using Sts2Sim.Core.Content;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Tests.Content;

public class GrabBagTests
{
    [Fact]
    public void GrabAndRemove_UsesWeightsAndRemovesPickedEntry()
    {
        var bag = new GrabBag<string>();
        bag.Add("zero-weight", 0.0);
        bag.Add("selected", 1.0);

        string? picked = bag.GrabAndRemove(new Rng(seed: 1));

        Assert.Equal("selected", picked);
        Assert.Null(bag.GrabAndRemove(new Rng(seed: 2)));
        Assert.True(bag.Any());
    }

    [Fact]
    public void GrabAndRemove_WhenEmpty_ReturnsDefaultWithoutRefilling()
    {
        var bag = new GrabBag<string>();
        bag.Add("only-entry", 1.0);
        var rng = new Rng(seed: 1);

        Assert.Equal("only-entry", bag.GrabAndRemove(rng));
        Assert.False(bag.Any());
        Assert.Null(bag.GrabAndRemove(rng));
    }

    [Fact]
    public void GrabAndRemove_WithPredicate_RejectsNonMatchingDraws()
    {
        var bag = new GrabBag<string>();
        bag.Add("excluded", 1.0);
        bag.Add("matching-zero-weight", 0.0);
        bag.Add("matching-selected", 1.0);

        string? picked = bag.GrabAndRemove(new Rng(seed: 1), item => item.StartsWith("matching"));

        Assert.Equal("matching-selected", picked);
        Assert.Equal("excluded", bag.GrabAndRemove(new Rng(seed: 2)));
    }

    [Fact]
    public void GrabAndRemove_WithNoPredicateMatch_ReturnsDefaultAndPreservesPool()
    {
        var bag = new GrabBag<string>();
        bag.Add("entry", 1.0);
        var rng = new Rng(seed: 1);

        Assert.Null(bag.GrabAndRemove(rng, _ => false));
        Assert.True(bag.Any());
        Assert.Equal("entry", bag.GrabAndRemove(rng));
    }

    [Fact]
    public void GrabAndRemove_WithEqualEntries_RemovesTheSelectedInstance()
    {
        var bag = new GrabBag<EqualItem>();
        var nonMatching = new EqualItem("non-matching");
        var matching = new EqualItem("matching");
        bag.Add(nonMatching, 1.0);
        bag.Add(matching, 1.0);
        var rng = new Rng(seed: 1);

        Assert.Same(matching, bag.GrabAndRemove(rng, item => ReferenceEquals(item, matching)));
        Assert.Null(bag.GrabAndRemove(rng, item => ReferenceEquals(item, matching)));
        Assert.Same(nonMatching, bag.GrabAndRemove(rng));
    }

    [Fact]
    public void GrabAndRemove_WhenWeightsAreNaN_ReturnsDefaultWithoutRemovingEntries()
    {
        var bag = new GrabBag<string>();
        const string excluded = "excluded";
        const string matching = "matching";
        bag.Add(excluded, 1.0);
        bag.Add(matching, double.NaN);
        var rng = new Rng(seed: 1);

        Assert.Null(bag.GrabAndRemove(rng, item => item == matching));
        Assert.Null(bag.GrabAndRemove(rng));
        Assert.True(bag.Any());
    }

    [Fact]
    public void EncounterTag_ContainsOnlyTheDefinedEncounterTags()
    {
        Assert.Equal(
            new[]
            {
                EncounterTag.None,
                EncounterTag.Mushroom,
                EncounterTag.Slimes,
                EncounterTag.Crawler,
                EncounterTag.Shrinker,
                EncounterTag.Nibbit,
                EncounterTag.Jaxfruit,
                EncounterTag.Workers,
                EncounterTag.Exoskeletons,
                EncounterTag.Chomper,
                EncounterTag.Thieves,
                EncounterTag.Burrower,
                EncounterTag.Knights,
                EncounterTag.Scrolls,
                EncounterTag.Seapunk,
                EncounterTag.Slugs,
            },
            Enum.GetValues<EncounterTag>());
    }

    [Fact]
    public void GrabAndRemove_RejectedDrawsMatchUpstreamSeedOneTrace()
    {
        var bag = new GrabBag<string>();
        bag.Add("A", 1.0);
        bag.Add("B", 1.0);
        bag.Add("C", 1.0);
        var rng = new Rng(1);

        // Upstream trace for seed 1: C, B, B, B, C are rejected, then A is accepted.
        Assert.Equal("A", bag.GrabAndRemove(rng, item => item == "A"));
        Assert.Equal(6, rng.Counter);
        Assert.True(bag.Any());
    }

    private sealed class EqualItem(string name)
    {
        public override bool Equals(object? obj) => obj is EqualItem;

        public override int GetHashCode() => 0;

        public override string ToString() => name;
    }
}
