namespace Sts2Sim.Core.Tests.Demo;

using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Models;

[Collection("ModelDb")]
public class CombatReplayDemoTests
{
    public CombatReplayDemoTests()
    {
        ModelDb.ResetForTests();
    }

    [Fact]
    public async Task RunAsync_SameSeed_ProducesIdenticalLogAndResult()
    {
        var log1 = new List<string>();
        var log2 = new List<string>();

        CombatReplayDemo.Result result1 = await CombatReplayDemo.RunAsync("deterministic-seed", log1.Add);
        ModelDb.ResetForTests();
        CombatReplayDemo.Result result2 = await CombatReplayDemo.RunAsync("deterministic-seed", log2.Add);

        Assert.Equal(log1, log2);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public async Task RunAsync_PlayerEventuallyWinsAgainstTrainingDummy()
    {
        var log = new List<string>();

        CombatReplayDemo.Result result = await CombatReplayDemo.RunAsync("win-check-seed", log.Add);

        Assert.True(result.Won);
        Assert.Equal(0, result.MonsterFinalHp);
        Assert.True(result.Rounds <= 50);
        Assert.Contains(log, line => line.Contains("胜利"));
    }

    [Fact]
    public async Task RunAsync_LogsMonsterIntentBeforeCardPlaysEachRound()
    {
        var log = new List<string>();

        await CombatReplayDemo.RunAsync("intent-log-seed", log.Add);

        int firstIntentIndex = log.FindIndex(l => l.Contains("怪物意图"));
        int firstPlayIndex = log.FindIndex(l => l.Contains("出牌："));
        Assert.True(firstIntentIndex >= 0);
        Assert.True(firstPlayIndex > firstIntentIndex);
    }

    [Fact]
    public async Task RunAsync_DifferentSeeds_CanProduceDifferentHandOrder()
    {
        var logA = new List<string>();
        var logB = new List<string>();

        await CombatReplayDemo.RunAsync("seed-a", logA.Add);
        ModelDb.ResetForTests();
        await CombatReplayDemo.RunAsync("seed-b", logB.Add);

        Assert.NotEqual(logA, logB);
    }

    [Fact]
    public async Task RunAsync_LogsVenerateAndFallingStarResourceAndEffectDeltas()
    {
        var log = new List<string>();

        await CombatReplayDemo.RunAsync("task22-demo-1", log.Add);

        int venerateIndex = log.FindIndex(line => line.Contains("Venerate -> Energy 3->2, Stars 0->2, damage 0, block 0->0, Weak 0->0, Vulnerable 0->0"));
        int fallingStarIndex = log.FindIndex(line => line.Contains("FallingStar -> Energy 1->1, Stars 2->0, damage 8, block 5->5, Weak 0->1, Vulnerable 0->1"));
        Assert.True(venerateIndex >= 0);
        Assert.True(fallingStarIndex > venerateIndex);
    }
}
