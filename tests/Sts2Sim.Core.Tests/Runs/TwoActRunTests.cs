namespace Sts2Sim.Core.Tests.Runs;

using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class TwoActRunTests : IDisposable
{
    public TwoActRunTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("7QM3KBSX1L8U")]
    [InlineData("GK3AHB5EJ4MW")]
    [InlineData("QVCMPZ1BVD08")]
    public async Task Silent_RunsActOneThenActTwo_WithoutThrowing(string seed)
    {
        var runState = new RunState(seed, new ActDefinition[] { new Overgrowth(), new Hive() });
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        var engine = new RunEngine(runState, options => options[0]);

        RunEngine.Result result = await engine.RunAsync(maxFloors: 200);

        Assert.True(result.FloorsVisited > 0);
    }

    [Fact]
    public async Task TwoActRun_ReachesActTwo_WhenActOneBossIsCleared()
    {
        var runState = new RunState("relic-gating-boss-28", new ActDefinition[] { new Overgrowth(), new Hive() });
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        player.Creature.SetMaxHpInternal(1_000_000m);
        player.Creature.HealInternal(1_000_000m);
        var engine = new RunEngine(runState, options => options[0]);

        RunEngine.Result result = await engine.RunAsync(maxFloors: 200);

        Assert.True(
            runState.CurrentActIndex == 1,
            $"Expected Act 2 after clearing the Act 1 boss, but stopped at act {runState.CurrentActIndex} " +
            $"after {result.FloorsVisited} floors with {result.FinalPlayerHp} HP.");
    }

    [Fact]
    public void ActTwo_RewardCards_HaveHigherUpgradeRate()
    {
        const int sampleCount = 100;
        int actOneUpgrades = Enumerable.Range(0, sampleCount).Count(index =>
            CreateRewardOption($"hive-upgrade-rate-{index}", advanceToActTwo: false).IsUpgraded);
        int actTwoUpgrades = Enumerable.Range(0, sampleCount).Count(index =>
            CreateRewardOption($"hive-upgrade-rate-{index}", advanceToActTwo: true).IsUpgraded);

        Assert.True(
            actTwoUpgrades > actOneUpgrades,
            $"Expected Act 2 upgrade count ({actTwoUpgrades}) to exceed Act 1 ({actOneUpgrades}).");
    }

    private static CardModel CreateRewardOption(string seed, bool advanceToActTwo)
    {
        var runState = new RunState(seed, new ActDefinition[] { new Overgrowth(), new Hive() });
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        if (advanceToActTwo)
        {
            runState.AdvanceToNextAct();
        }

        return Assert.Single(CardFactory.CreateForReward(
            player,
            optionCount: 1,
            CardRarityOddsType.RegularEncounter));
    }
}
