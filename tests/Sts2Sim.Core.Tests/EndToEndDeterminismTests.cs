namespace Sts2Sim.Core.Tests;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

public class EndToEndDeterminismTests
{
    /// <summary>模拟一段"半局游戏"的 RNG/Odds 消耗，返回全部可观察状态的指纹。</summary>
    private static string RunScript(string seed)
    {
        var runRng = new RunRngSet(seed);
        var playerRng = new PlayerRngSet(runRng.Seed);
        var ascension = new AscensionManager(AscensionLevel.TightBelt); // A4
        var playerOdds = new PlayerOddsSet(playerRng, ascension, NullOddsHooks.Instance);
        var runOdds = new RunOddsSet(runRng.UnknownMapPoint, NullOddsHooks.Instance);

        var trace = new System.Text.StringBuilder();
        for (int floor = 0; floor < 30; floor++)
        {
            trace.Append(runOdds.UnknownMapPoint.Roll(Array.Empty<RoomType>()));
            trace.Append(playerOdds.CardRarity.Roll(CardRarityOddsType.RegularEncounter));
            trace.Append(playerOdds.PotionReward.Roll(floor % 5 == 0 ? RoomType.Elite : RoomType.Monster));
            var deck = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
            runRng.Shuffle.Shuffle(deck);
            trace.Append(string.Join('-', deck));
            trace.Append(runRng.MonsterAi.NextInt(4));
            trace.Append(runRng.CombatTargets.NextBool());
            trace.Append(playerRng.Rewards.NextInt(100));
        }
        trace.Append('|').Append(runRng.ToSerializable().Rngs.Count);
        trace.Append('|').Append(playerOdds.CardRarity.CurrentValue.ToString("R"));
        trace.Append('|').Append(playerOdds.PotionReward.CurrentValue.ToString("R"));
        trace.Append('|').Append(runOdds.UnknownMapPoint.MonsterOdds.ToString("R"));
        trace.Append('|').Append(runOdds.UnknownMapPoint.EliteOdds.ToString("R"));
        trace.Append('|').Append(runOdds.UnknownMapPoint.TreasureOdds.ToString("R"));
        trace.Append('|').Append(runOdds.UnknownMapPoint.ShopOdds.ToString("R"));
        return trace.ToString();
    }

    [Fact]
    public void SameSeed_ProducesIdenticalTrace()
    {
        Assert.Equal(RunScript("sts2"), RunScript("sts2"));
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentTraces()
    {
        Assert.NotEqual(RunScript("sts2"), RunScript("sts2!"));
    }

    [Fact]
    public void ClonedRngSet_DivergesIndependently()
    {
        var original = new RunRngSet("fork");
        for (int i = 0; i < 10; i++) original.MonsterAi.NextInt(100);

        var clone = original.CloneExact();
        // 原集继续推进 50 步后，克隆从分叉点继续的序列必须与"另一个推进到相同位置的参照集"一致。
        // 参照集用 FromSave 构造：v0.109 语义下 SerializableRng 直接序列化底层生成器状态
        // （counter + 4×ulong），FromSave 精确重建分叉点状态，与旧版"按 Counter 重放"无关。
        var reference = RunRngSet.FromSave(original.ToSerializable());
        for (int i = 0; i < 50; i++) original.MonsterAi.NextInt(100);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(reference.MonsterAi.NextInt(100), clone.MonsterAi.NextInt(100));
        }
    }
}
