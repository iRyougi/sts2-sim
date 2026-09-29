namespace Sts2Sim.Core.Tests.Runs;

using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class ThreeActRunTests : IDisposable
{
    public ThreeActRunTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("7QM3KBSX1L8U")]
    [InlineData("GK3AHB5EJ4MW")]
    [InlineData("QVCMPZ1BVD08")]
    public async Task Silent_RunsAllThreeActs_WithoutThrowing(string seed)
    {
        RunState runState = CreateRun(seed);
        var engine = new RunEngine(runState, options => options[0]);

        RunEngine.Result result = await engine.RunAsync(maxFloors: 300);

        Assert.True(result.FloorsVisited > 0);
    }

    [Fact]
    public async Task RunEngine_HighHpProductionRun_ReachesGlory()
    {
        RunState runState = CreateHighHpRun("glory-integration-0");
        var engine = new RunEngine(runState, options => options[0]);

        RunEngine.Result result = await engine.RunAsync(maxFloors: 300);

        Assert.True(
            runState.CurrentActIndex == 2 && runState.Act is Glory,
            $"Expected Glory after clearing the first two bosses, but stopped at act " +
            $"{runState.CurrentActIndex} after {result.FloorsVisited} floors with " +
            $"{result.FinalPlayerHp} HP.");
    }

    [Fact]
    public async Task RunDriver_HighHpProductionRun_ReachesGlory()
    {
        RunState runState = CreateHighHpRun("glory-integration-0");
        var driver = new RunDriver(runState, new FirstChoiceDecisionSource());

        RunDriver.Result result = await driver.RunAsync(maxFloors: 300);

        Assert.True(
            runState.CurrentActIndex == 2 && runState.Act is Glory,
            $"Expected Glory after clearing the first two bosses, but stopped at act " +
            $"{runState.CurrentActIndex} after {result.FloorsVisited} floors with " +
            $"{result.FinalPlayerHp} HP.");
    }

    private static RunState CreateRun(string seed)
    {
        var runState = new RunState(
            seed,
            new ActDefinition[] { new Overgrowth(), new Hive(), new Glory() });
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        return runState;
    }

    private static RunState CreateHighHpRun(string seed)
    {
        RunState runState = CreateRun(seed);
        Player player = runState.Players[0];
        player.Creature.SetMaxHpInternal(999_999_999m);
        player.Creature.HealInternal(999_999_999m);
        return runState;
    }
}
