namespace Sts2Sim.Core.Tests.Runs;

using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Saves;

public class RngSetTests
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
    public void RunRngSet_DerivesSeedFromXxHash()
    {
        var set = new RunRngSet("abc");
        Assert.Equal(StringHelper.GetDeterministicHashCode("abc"), set.Seed);
    }

    [Fact]
    public void RunRngSet_OldPrefix_UsesLegacyHash()
    {
        var set = new RunRngSet("oldabc");
        Assert.Equal((uint)StringHelper.GetDeterministicHashCodeOld("abc"), set.Seed);
    }

    [Fact]
    public void RunRngSet_StreamEqualsManuallyDerivedRng()
    {
        var set = new RunRngSet("abc");
        var manual = new Rng(set.Seed, "combat_targets");
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(manual.NextInt(1000), set.CombatTargets.NextInt(1000));
        }
    }

    [Fact]
    public void RunRngSet_StreamsAreIndependent()
    {
        var set = new RunRngSet("test_seed");
        int before = set.Shuffle.Counter;
        set.MonsterAi.NextInt(100);
        set.MonsterAi.NextInt(100);
        Assert.Equal(before, set.Shuffle.Counter); // 消耗 MonsterAi 不影响 Shuffle
        Assert.Equal(2, set.MonsterAi.Counter);
    }

    [Fact]
    public void RunRngSet_SerializeAndRestore_RoundTrips()
    {
        var set = new RunRngSet("roundtrip");
        for (int i = 0; i < 7; i++) set.Shuffle.NextInt(100);
        for (int i = 0; i < 3; i++) set.MonsterAi.NextBool();

        var save = set.ToSerializable();
        Assert.Equal("roundtrip", save.Seed);
        Assert.Equal(7, save.Rngs[RunRngType.Shuffle].counter);
        Assert.Equal(3, save.Rngs[RunRngType.MonsterAi].counter);

        var restored = RunRngSet.FromSave(save);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(set.Shuffle.NextInt(100), restored.Shuffle.NextInt(100));
            Assert.Equal(set.MonsterAi.NextInt(100), restored.MonsterAi.NextInt(100));
        }
    }

    [Fact]
    public void RunRngSet_LoadFromSerializable_RewindsToSavedState()
    {
        var set = new RunRngSet("rewind");
        for (int i = 0; i < 10; i++) set.UpFront.NextInt(100);
        var save = set.ToSerializable();

        // 参照流：与保存点状态完全一致的独立 Rng（用同一份 SerializableRng 重建）。
        var reference = new Rng(save.Rngs[RunRngType.UpFront]);

        // 继续推进后加载旧存档 → 应精确回退到保存点状态。
        for (int i = 0; i < 5; i++) set.UpFront.NextInt(100);
        set.LoadFromSerializable(save);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(reference.NextInt(100), set.UpFront.NextInt(100));
        }
    }

    [Fact]
    public void RunRngSet_LoadWithDifferentSeed_Throws()
    {
        var set = new RunRngSet("a");
        var save = new RunRngSet("b").ToSerializable();
        Assert.Throws<NotImplementedException>(() => set.LoadFromSerializable(save));
    }

    [Fact]
    public void RunRngSet_Clone_IsExactAndIndependent()
    {
        var set = new RunRngSet("clone_me");
        set.Shuffle.NextInt(100);
        var clone = set.CloneExact();
        AssertSameState(set.Shuffle.ToSerializable(), clone.Shuffle.ToSerializable());

        // 克隆后推进原集，克隆不受影响：两者从分叉点起的序列必须一致。
        int fromClone = clone.Shuffle.NextInt(100);
        int fromOriginal = set.Shuffle.NextInt(100);
        Assert.Equal(fromOriginal, fromClone);
    }

    [Fact]
    public void RunRngSet_MockRng_ReplacesStream()
    {
        var set = new RunRngSet("mocked");
        set.MockRng(RunRngType.MonsterAi, 999uL);
        var expected = new Rng(999uL);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(expected.NextInt(1000), set.MonsterAi.NextInt(1000));
        }
    }

    [Fact]
    public void RunRngSet_SeedUsesFull64Bits()
    {
        // "sts2" 的 XxHash64 派生种子超出 uint 范围——锁定"内部种子确实用满 64 位"。
        var set = new RunRngSet("sts2");
        Assert.True(set.Seed > uint.MaxValue, $"expected Seed > uint.MaxValue, got {set.Seed}");
    }

    [Fact]
    public void PlayerRngSet_DerivesStreamFromSnakeCaseName()
    {
        var set = new PlayerRngSet(42uL);
        var manual = new Rng(42uL, "rewards");
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(manual.NextInt(1000), set.Rewards.NextInt(1000));
        }
    }

    [Fact]
    public void PlayerRngSet_SerializeAndRestore_RoundTrips()
    {
        var set = new PlayerRngSet(7uL);
        for (int i = 0; i < 4; i++) set.Shops.NextInt(50);

        var save = set.ToSerializable();
        Assert.Equal(7uL, save.Seed);
        Assert.Equal(4, save.Rngs[PlayerRngType.Shops].counter);

        var restored = PlayerRngSet.FromSerializable(save);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(set.Shops.NextInt(50), restored.Shops.NextInt(50));
        }
    }

    [Fact]
    public void PlayerRngSet_LoadFromSerializable_RewindsToSavedState()
    {
        var set = new PlayerRngSet(13uL);
        for (int i = 0; i < 10; i++) set.Shops.NextInt(100);
        var save = set.ToSerializable();
        var reference = new Rng(save.Rngs[PlayerRngType.Shops]);

        for (int i = 0; i < 5; i++) set.Shops.NextInt(100);
        set.LoadFromSerializable(save);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(reference.NextInt(100), set.Shops.NextInt(100));
        }
    }

    [Fact]
    public void PlayerRngSet_LoadWithDifferentSeed_Throws()
    {
        var set = new PlayerRngSet(1uL);
        var save = new PlayerRngSet(2uL).ToSerializable();
        Assert.Throws<NotImplementedException>(() => set.LoadFromSerializable(save));
    }

    [Fact]
    public void PlayerRngSet_Clone_IsExactAndIndependent()
    {
        var set = new PlayerRngSet(21uL);
        set.Rewards.NextInt(100);
        var clone = set.CloneExact();
        AssertSameState(set.Rewards.ToSerializable(), clone.Rewards.ToSerializable());

        int fromClone = clone.Rewards.NextInt(100);
        int fromOriginal = set.Rewards.NextInt(100);
        Assert.Equal(fromOriginal, fromClone);
    }
}
