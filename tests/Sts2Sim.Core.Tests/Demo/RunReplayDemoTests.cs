using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Models;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Sts2Sim.Core.Tests.Demo;

public class RunReplayDemoTests : IDisposable
{
    public RunReplayDemoTests()
    {
        ModelDb.ResetForTests();
    }

    public void Dispose()
    {
        ModelDb.ResetForTests();
    }

    [Fact]
    public async Task RunAsync_ProducesNonEmptyLog()
    {
        var log = new List<string>();
        RunReplayDemo.Result result = await RunReplayDemo.RunAsync("demo-seed-1", log.Add);

        Assert.NotEmpty(log);
        Assert.True(result.FloorsVisited > 0);
    }

    [Fact]
    public async Task RunAsync_Plan06cDemo_ReportsNewRelicPickupByName()
    {
        var log = new List<string>();

        await RunReplayDemo.RunAsync("plan06c-demo", log.Add);

        Assert.Contains(
            log,
            line => line.StartsWith("[floor ", StringComparison.Ordinal) &&
                    line.Contains(" | new relic: ", StringComparison.Ordinal) &&
                    !line.Contains(
                        nameof(Sts2Sim.Core.Models.Relics.DivineRight),
                        StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_IsDeterministicForSameSeed()
    {
        var logA = new List<string>();
        var logB = new List<string>();

        RunReplayDemo.Result resultA = await RunReplayDemo.RunAsync("demo-seed-2", logA.Add);
        ModelDb.ResetForTests();
        RunReplayDemo.Result resultB = await RunReplayDemo.RunAsync("demo-seed-2", logB.Add);

        Assert.Equal(logA, logB);
        Assert.Equal(resultA, resultB);
    }

    [Fact]
    public async Task RunAsync_DifferentSeedsProduceDifferentLogs()
    {
        var logA = new List<string>();
        var logB = new List<string>();

        await RunReplayDemo.RunAsync("demo-seed-3", logA.Add);
        ModelDb.ResetForTests();
        await RunReplayDemo.RunAsync("demo-seed-4", logB.Add);

        Assert.NotEqual(logA, logB);
    }


    [Fact]
    public async Task RunAsync_Plan06gBossRoute_ReachesAndResolvesRealBossWithoutCrashing()
    {
        var log = new List<string>();

        // Source-correct encounter slot order changes this automatic policy route.
        // A bounded compiled-demo probe selected this seed; retain real Boss reach and resolution assertions.
        RunReplayDemo.Result result = await RunReplayDemo.RunAsync("shared-demo-boss-117", log.Add);

        Assert.True(
            result.FloorsVisited >= 15,
            $"Expected the seeded route to reach the boss, but it stopped after {result.FloorsVisited} floors.\n{string.Join(Environment.NewLine, log)}");
        Assert.Contains(log, line => line.Contains("Boss -> Boss:", StringComparison.Ordinal));
    }

    [Fact]
    public void EnsureModelsRegistered_IncludesRepresentativeEconomyContent()
    {
        RunReplayDemo.EnsureModelsRegistered();

        Assert.Contains(ModelDb.All<RelicModel>(), relic => relic is Sts2Sim.Core.Models.Relics.Vajra);
        Assert.Contains(ModelDb.All<PotionModel>(), potion => potion is Sts2Sim.Core.Models.Potions.StrengthPotion);
        Assert.True(ModelDb.Contains(typeof(Sts2Sim.Core.Models.Powers.StrengthPower)));
    }
}
