using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Content;

[Collection("ModelDb")]
public sealed class CrossCharacterContentExclusionsTests : IDisposable
{
    public CrossCharacterContentExclusionsTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void IsExcluded_MatchesKnownIncompleteContentOnly()
    {
        Assert.True(CrossCharacterContentExclusions.IsExcluded(typeof(Splash)));
        Assert.True(CrossCharacterContentExclusions.IsExcluded(typeof(Kaleidoscope)));
        Assert.False(CrossCharacterContentExclusions.IsExcluded(typeof(StrikeRegent)));
        Assert.False(CrossCharacterContentExclusions.IsExcluded(typeof(WingedBoots)));
    }

    [Fact]
    public void IsExcludedKey_MatchesExcludedTypeNamesOnly()
    {
        Assert.True(CrossCharacterContentExclusions.IsExcludedKey(nameof(Kaleidoscope)));
        Assert.False(CrossCharacterContentExclusions.IsExcludedKey(nameof(WingedBoots)));
        Assert.False(CrossCharacterContentExclusions.IsExcludedKey("PROCEED"));
    }

    [Fact]
    public async Task ChooseEventOptionAsync_Default_SkipsExcludedRelicOption()
    {
        IRunDecisionSource source = new MinimalDecisionSource();
        var options = new[]
        {
            new EventOption(nameof(Kaleidoscope), () => Task.CompletedTask),
            new EventOption(nameof(WingedBoots), () => Task.CompletedTask),
        };

        EventOption chosen = await source.ChooseEventOptionAsync(options);

        Assert.Equal(nameof(WingedBoots), chosen.Key);
    }

    [Fact]
    public async Task ChooseEventOptionAsync_Default_FallsBackToFirstOption_WhenEveryOptionIsExcluded()
    {
        IRunDecisionSource source = new MinimalDecisionSource();
        var options = new[] { new EventOption(nameof(Kaleidoscope), () => Task.CompletedTask) };

        EventOption chosen = await source.ChooseEventOptionAsync(options);

        Assert.Equal(nameof(Kaleidoscope), chosen.Key);
    }

    [Fact]
    public async Task ChooseRewardActionAsync_Default_SkipsExcludedCardAndPicksNextOption()
    {
        (RunState runState, Player player) = CreateRun("reward-exclusion");
        var cardReward = new CardReward(player, new CardModel[] { ModelDb.Card<Splash>(), ModelDb.Card<StrikeRegent>() });
        cardReward.Populate(runState);
        RewardsSet rewards = RewardsSet.CreateCustom(player, card: cardReward);
        await rewards.Gold.Take();
        IRunDecisionSource source = new MinimalDecisionSource();

        RewardDecision decision = await source.ChooseRewardActionAsync(rewards);

        RewardDecision.TakeCard takeCard = Assert.IsType<RewardDecision.TakeCard>(decision);
        Assert.IsType<StrikeRegent>(takeCard.Card);
    }

    [Fact]
    public async Task ChooseRewardActionAsync_Default_SkipsCard_WhenOnlyOptionIsExcluded()
    {
        (RunState runState, Player player) = CreateRun("reward-exclusion-only-option");
        var cardReward = new CardReward(player, new CardModel[] { ModelDb.Card<Splash>() });
        cardReward.Populate(runState);
        RewardsSet rewards = RewardsSet.CreateCustom(player, card: cardReward);
        await rewards.Gold.Take();
        IRunDecisionSource source = new MinimalDecisionSource();

        RewardDecision decision = await source.ChooseRewardActionAsync(rewards);

        Assert.IsType<RewardDecision.SkipCard>(decision);
    }

    /// <summary>
    /// 回归测试：种子 7QM3KBSX1L8U 是 Plan07 仿真-真实校验实测过的真实种子——Neow 候选里确实包含
    /// Kaleidoscope（真实游戏和 sts2-ai 当时都选中了它）。修复后，RunEngine 与 RunDriver 的默认策略
    /// 都必须跳过它，且两者对同一 seed 仍要产出一致结果（项目既有不变量）。
    /// </summary>
    [Fact]
    public async Task RunEngineAndRunDriver_DefaultPolicy_NeverObtainKaleidoscopeAtNeow_ForSeedThatOffersIt()
    {
        RunState engineState = CreateRunState("7QM3KBSX1L8U");
        var engine = new RunEngine(engineState, points => points[0]);
        await engine.RunAsync(maxFloors: 0);

        RunState driverState = CreateRunState("7QM3KBSX1L8U");
        var driver = new RunDriver(driverState, new MinimalDecisionSource());
        await driver.RunAsync(maxFloors: 0);

        Assert.DoesNotContain(engineState.Players[0].Relics, relic => relic is Kaleidoscope);
        Assert.DoesNotContain(driverState.Players[0].Relics, relic => relic is Kaleidoscope);
        Assert.Equal(
            engineState.Players[0].Relics.Select(relic => relic.GetType()),
            driverState.Players[0].Relics.Select(relic => relic.GetType()));
    }

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = CreateRunState(seed);
        return (runState, runState.Players[0]);
    }

    private static RunState CreateRunState(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return runState;
    }

    private sealed class MinimalDecisionSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(options[0]);

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
    }
}
