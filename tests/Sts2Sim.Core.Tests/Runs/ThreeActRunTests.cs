namespace Sts2Sim.Core.Tests.Runs;

using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Rewards;

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

    [Fact]
    public async Task RunDriver_ActStartMapDecision_IsAskedOncePerLaterActBeforeTheStartIsVisited_AndDefaultIsUnchanged()
    {
        RunState baseState = CreateHighHpRun("glory-integration-0");
        RunDriver.Result baseResult = await new RunDriver(baseState, new FirstChoiceDecisionSource()).RunAsync(maxFloors: 300);

        RunState runState = CreateHighHpRun("glory-integration-0");
        var source = new ActStartRecordingSource(runState, returnWrongPoint: false);
        RunDriver.Result result = await new RunDriver(runState, source).RunAsync(maxFloors: 300);

        // Native RunManager.EnterAct: acts after the first open the map with the starting point as the only node.
        Assert.Equal(new[] { "act=1 visited=0 room=none", "act=2 visited=0 room=none" }, source.Asks);
        Assert.Equal(Fingerprint(baseState, baseResult), Fingerprint(runState, result));

        RunState wrongState = CreateHighHpRun("glory-integration-0");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RunDriver(wrongState, new ActStartRecordingSource(wrongState, returnWrongPoint: true)).RunAsync(maxFloors: 300));
        Assert.Equal(1, wrongState.CurrentActIndex);
        Assert.Empty(wrongState.VisitedMapCoords);
    }

    private static string Fingerprint(RunState runState, RunDriver.Result result)
    {
        Player player = runState.Players[0];
        return string.Join("|",
            $"act={runState.CurrentActIndex} floors={result.FloorsVisited} hp={result.FinalPlayerHp} gold={player.Gold}",
            string.Join(",", player.Deck.Cards.Select(c => c.Id.ToString() + (c.IsUpgraded ? "+" : ""))),
            string.Join(",", player.Relics.Select(r => r.Id.ToString())),
            string.Join(",", Enum.GetValues<Sts2Sim.Core.Entities.Rngs.RunRngType>().Select(t => runState.Rng.GetRng(t).Counter)),
            string.Join(",", Enum.GetValues<Sts2Sim.Core.Entities.Rngs.PlayerRngType>().Select(t => player.PlayerRng.GetRng(t).Counter)));
    }

    private sealed class ActStartRecordingSource(RunState runState, bool returnWrongPoint) : IRunDecisionSource
    {
        private readonly FirstChoiceDecisionSource _inner = new();

        public List<string> Asks { get; } = new();

        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => _inner.ChooseMapPointAsync(options);

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) => _inner.ChooseCombatActionAsync(state);

        public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) => _inner.ChooseRewardActionAsync(rewards);

        public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) =>
            _inner.ChooseShopActionAsync(inventory, player);

        public Task<MapPoint> ChooseActStartMapPointAsync(MapPoint startingPoint)
        {
            Assert.Same(runState.Map.StartingMapPoint, startingPoint);
            Asks.Add($"act={runState.CurrentActIndex} visited={runState.VisitedMapCoords.Count} " +
                $"room={runState.CurrentRoom?.GetType().Name ?? "none"}");
            return Task.FromResult(returnWrongPoint ? startingPoint.Children.First() : startingPoint);
        }
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
