namespace Sts2Sim.Core.Tests.Runs;

using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class SilentFullActOneTests : IDisposable
{
    public SilentFullActOneTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("7QM3KBSX1L8U")]
    [InlineData("GK3AHB5EJ4MW")]
    [InlineData("QVCMPZ1BVD08")]
    public async Task Silent_RunsFullActOne_WithoutThrowing(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        var engine = new RunEngine(runState, options => options[0]);

        RunEngine.Result result = await engine.RunAsync(maxFloors: 100);

        Assert.True(result.FloorsVisited > 0);
    }
}
