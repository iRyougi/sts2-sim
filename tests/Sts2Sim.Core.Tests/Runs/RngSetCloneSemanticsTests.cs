namespace Sts2Sim.Core.Tests.Runs;

using System.Linq;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

public class RngSetCloneSemanticsTests
{
    private static int[] Draw(Rng rng, int count) =>
        Enumerable.Range(0, count).Select(_ => rng.NextInt(1_000_000)).ToArray();

    [Theory]
    [InlineData(0)]
    public void KeyedRng_AcceptanceCoversGuardAlignmentCloneAndTrace(int _)
    {
        RunRngSet guard = RunRngSet.CreateKeyed("keyed-guard");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => guard.Shuffle.NextInt(100));

        Assert.Contains("semantic key", exception.Message, StringComparison.OrdinalIgnoreCase);

        {
            RunRngSet baseline = RunRngSet.CreateKeyed("keyed-alignment");
            RunRngSet variant = RunRngSet.CreateKeyed("keyed-alignment");
            int baselineFirst = baseline.ForSemanticKey(
                RunRngType.Shuffle, "floor=7/combat=3/shuffle_ordinal=1").NextInt(1_000_000);
            int variantFirst = variant.ForSemanticKey(
                RunRngType.Shuffle, "floor=7/combat=3/shuffle_ordinal=1").NextInt(1_000_000);
            variant.ForSemanticKey(
                RunRngType.Shuffle, "floor=7/combat=3/variant_only_shuffle=1").NextInt(1_000_000);
            int baselineSecond = baseline.ForSemanticKey(
                RunRngType.Shuffle, "floor=7/combat=3/shuffle_ordinal=2").NextInt(1_000_000);
            int variantSecond = variant.ForSemanticKey(
                RunRngType.Shuffle, "floor=7/combat=3/shuffle_ordinal=2").NextInt(1_000_000);
            Assert.Equal(baselineFirst, variantFirst);
            Assert.Equal(baselineSecond, variantSecond);
        }

        {
            RunRngSet source = RunRngSet.CreateKeyed("keyed-clone");
            RunRngSet clone = source.CloneExact();
            source.ForSemanticKey(
                RunRngType.MonsterAi, "floor=4/combat=2/enemy_uid=3/turn=1").NextInt(1_000_000);
            int sourceNext = source.ForSemanticKey(
                RunRngType.MonsterAi, "floor=4/combat=2/enemy_uid=3/turn=2").NextInt(1_000_000);
            int cloneNext = clone.ForSemanticKey(
                RunRngType.MonsterAi, "floor=4/combat=2/enemy_uid=3/turn=2").NextInt(1_000_000);
            Assert.Equal(sourceNext, cloneNext);
            Assert.Throws<InvalidOperationException>(() => clone.MonsterAi.NextInt(100));
        }

        {
            PlayerRngSet baseline = PlayerRngSet.CreateKeyed(12345uL);
            PlayerRngSet variant = PlayerRngSet.CreateKeyed(12345uL);
            variant.ForSemanticKey(
                PlayerRngType.Rewards, "floor=5/room_kind=monster/slot=variant_only").NextInt(1_000_000);
            int baselineReward = baseline.ForSemanticKey(
                PlayerRngType.Rewards, "floor=5/room_kind=monster/slot=card_0").NextInt(1_000_000);
            int variantReward = variant.ForSemanticKey(
                PlayerRngType.Rewards, "floor=5/room_kind=monster/slot=card_0").NextInt(1_000_000);
            Assert.Equal(baselineReward, variantReward);
            Assert.Throws<InvalidOperationException>(() => baseline.Rewards.NextInt(100));
        }

        {
            RunRngSet keyed = RunRngSet.CreateKeyed("keyed-trace");
            const string semanticKey = "floor=7/combat=3/shuffle_ordinal=2";
            Rng purpose = keyed.ForSemanticKey(RunRngType.Shuffle, semanticKey);
            purpose.NextInt(10);
            purpose.NextBool();
            Assert.Collection(
                keyed.GetKeyedDraws(),
                first =>
                {
                    Assert.Equal("shuffle", first.StreamName);
                    Assert.Equal(semanticKey, first.SemanticKey);
                    Assert.Equal(1, first.DrawOrdinal);
                    Assert.Equal("NextInt(maxExclusive=10)", first.Operation);
                },
                second =>
                {
                    Assert.Equal("shuffle", second.StreamName);
                    Assert.Equal(semanticKey, second.SemanticKey);
                    Assert.Equal(2, second.DrawOrdinal);
                    Assert.Equal("NextBool", second.Operation);
                });
        }

        {
            RunRngSet source = RunRngSet.CreateKeyed("keyed-trace-clone");
            source.ForSemanticKey(RunRngType.MonsterAi, "shared").NextInt(10);
            RunRngSet clone = source.CloneExact();
            source.ForSemanticKey(RunRngType.MonsterAi, "source-only").NextInt(10);
            clone.ForSemanticKey(RunRngType.MonsterAi, "clone-only").NextInt(10);
            Assert.Equal(new[] { "shared", "source-only" },
                source.GetKeyedDraws().Select(draw => draw.SemanticKey));
            Assert.Equal(new[] { "shared", "clone-only" },
                clone.GetKeyedDraws().Select(draw => draw.SemanticKey));
        }
    }

    [Fact]
    public void PlayerRngSet_CloneExact_AllStreamsReproduceSource()
    {
        var source = new PlayerRngSet(9001uL);
        source.Rewards.NextInt(50);

        PlayerRngSet clone = source.CloneExact();

        foreach (PlayerRngType type in Enum.GetValues<PlayerRngType>())
        {
            Assert.Equal(source.GetRng(type).Counter, clone.GetRng(type).Counter);
            Assert.Equal(Draw(source.GetRng(type), 8), Draw(clone.GetRng(type), 8));
        }
    }

    [Fact]
    public void PlayerRngSet_CloneReseeded_OnlyNamedStreamsAreReseeded()
    {
        var source = new PlayerRngSet(9001uL);
        source.Rewards.NextInt(50);
        source.Shops.NextInt(50);

        PlayerRngSet branch = source.CloneReseeded(4242uL, [PlayerRngType.Rewards]);

        Assert.Equal(0, branch.Rewards.Counter);
        Assert.Equal(source.Shops.Counter, branch.Shops.Counter);
        Assert.Equal(Draw(source.Shops, 8), Draw(branch.Shops, 8));
    }

    [Fact]
    public void PlayerRngSet_CloneReseeded_DifferentBranchSeedsDiverge()
    {
        var source = new PlayerRngSet(9001uL);

        PlayerRngSet first = source.CloneReseeded(1uL, [PlayerRngType.Rewards]);
        PlayerRngSet second = source.CloneReseeded(2uL, [PlayerRngType.Rewards]);

        Assert.NotEqual(Draw(first.Rewards, 32), Draw(second.Rewards, 32));
    }

    [Fact]
    public void PlayerRngSet_CloneReseeded_EmptyStreamListThrows()
    {
        var source = new PlayerRngSet(9001uL);

        Assert.Throws<ArgumentException>(() => source.CloneReseeded(1uL, []));
    }

    [Fact]
    public void PlayerRngSet_CloneReseeded_InvalidOnlyStreamListThrows()
    {
        var source = new PlayerRngSet(9001uL);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => source.CloneReseeded(1uL, [(PlayerRngType)999]));
    }

    [Fact]
    public void PlayerRngSet_CloneReseeded_MixedValidAndInvalidStreamListThrows()
    {
        var source = new PlayerRngSet(9001uL);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => source.CloneReseeded(1uL, [PlayerRngType.Rewards, (PlayerRngType)999]));
    }

    [Fact]
    public void PlayerRngSet_CloneReseeded_PreservesSeedIdentity()
    {
        var source = new PlayerRngSet(9001uL);

        PlayerRngSet branch = source.CloneReseeded(1uL, [PlayerRngType.Rewards]);

        Assert.Equal(source.Seed, branch.Seed);
    }

    [Fact]
    public void RunRngSet_CloneExact_AllStreamsReproduceSource()
    {
        var source = new RunRngSet("gate-seed");
        source.Shuffle.NextInt(50);
        source.MonsterAi.NextInt(50);

        RunRngSet clone = source.CloneExact();

        foreach (RunRngType type in Enum.GetValues<RunRngType>())
        {
            Assert.Equal(source.GetRng(type).Counter, clone.GetRng(type).Counter);
            Assert.Equal(Draw(source.GetRng(type), 8), Draw(clone.GetRng(type), 8));
        }
    }

    [Fact]
    public void RunRngSet_CloneReseeded_OnlyNamedStreamsAreReseeded()
    {
        var source = new RunRngSet("gate-seed");
        source.Shuffle.NextInt(50);
        source.MonsterAi.NextInt(50);

        RunRngSet branch = source.CloneReseeded(4242uL, [RunRngType.Shuffle]);

        Assert.Equal(0, branch.Shuffle.Counter);
        Assert.Equal(source.MonsterAi.Counter, branch.MonsterAi.Counter);
        Assert.Equal(Draw(source.MonsterAi, 8), Draw(branch.MonsterAi, 8));
    }

    [Fact]
    public void RunRngSet_CloneReseeded_DifferentBranchSeedsDiverge()
    {
        var source = new RunRngSet("gate-seed");

        RunRngSet first = source.CloneReseeded(1uL, [RunRngType.Shuffle]);
        RunRngSet second = source.CloneReseeded(2uL, [RunRngType.Shuffle]);

        Assert.NotEqual(Draw(first.Shuffle, 32), Draw(second.Shuffle, 32));
    }

    [Fact]
    public void RunRngSet_CloneReseeded_MultipleStreamsGetDistinctSequences()
    {
        var source = new RunRngSet("gate-seed");

        RunRngSet branch = source.CloneReseeded(
            4242uL,
            [RunRngType.Shuffle, RunRngType.MonsterAi]);

        Assert.NotEqual(Draw(branch.Shuffle, 32), Draw(branch.MonsterAi, 32));
    }

    [Fact]
    public void RunRngSet_CloneReseeded_EmptyStreamListThrows()
    {
        var source = new RunRngSet("gate-seed");

        Assert.Throws<ArgumentException>(() => source.CloneReseeded(1uL, []));
    }

    [Fact]
    public void RunRngSet_CloneReseeded_InvalidOnlyStreamListThrows()
    {
        var source = new RunRngSet("gate-seed");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => source.CloneReseeded(1uL, [(RunRngType)999]));
    }

    [Fact]
    public void RunRngSet_CloneReseeded_MixedValidAndInvalidStreamListThrows()
    {
        var source = new RunRngSet("gate-seed");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => source.CloneReseeded(1uL, [RunRngType.Shuffle, (RunRngType)999]));
    }

    [Fact]
    public void RunRngSet_CloneReseeded_PreservesSeedIdentity()
    {
        var source = new RunRngSet("gate-seed");

        RunRngSet branch = source.CloneReseeded(1uL, [RunRngType.Shuffle]);

        Assert.Equal(source.StringSeed, branch.StringSeed);
        Assert.Equal(source.Seed, branch.Seed);
    }
}
