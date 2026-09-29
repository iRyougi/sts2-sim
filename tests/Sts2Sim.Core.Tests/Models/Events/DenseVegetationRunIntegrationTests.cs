using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

internal sealed class DenseCombatProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public int CombatStartCount { get; private set; }

    public int EnemyCountAtCombatStart { get; private set; }

    public int RewardGenerationCount { get; private set; }

    public override Task BeforeCombatStart()
    {
        CombatStartCount++;
        EnemyCountAtCombatStart = Owner.Creature.CombatState!.Enemies.Count;
        return Task.CompletedTask;
    }

    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType) =>
        RewardGenerationCount++;
}

file sealed class DenseCombatDecisionSource : IRunDecisionSource
{
    public int EnemyCountAtFirstDecision { get; private set; }

    public int RewardDecisionCount { get; private set; }

    public bool SawNormalCombatRewards { get; private set; }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options[0]);

    public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) =>
        Task.FromResult(options.Single(option => option.Key == "FIGHT"));

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
    {
        if (EnemyCountAtFirstDecision == 0)
        {
            EnemyCountAtFirstDecision = state.Enemies.Count;
        }

        Player player = state.Players[0];
        CardModel? attack = player.PlayerCombatState!.Hand.Cards
            .FirstOrDefault(card => card.Type == CardType.Attack && card.CanPlay(out _));
        return Task.FromResult<CombatDecision>(attack is null
            ? new CombatDecision.EndTurn()
            : new CombatDecision.PlayCard(attack, state.HittableEnemies[0]));
    }

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards)
    {
        RewardDecisionCount++;
        SawNormalCombatRewards |= !rewards.Gold.IsResolved && !rewards.Card.IsResolved
            && rewards.Gold.Amount > 0;
        return Task.FromResult(RewardDecisionClassifier.ChooseDefault(rewards));
    }
}

[Collection("ModelDb")]
public sealed class DenseVegetationRunIntegrationTests : IDisposable
{
    public DenseVegetationRunIntegrationTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(DenseVegetation), typeof(Wriggler), typeof(Infection), typeof(StrengthPower),
            typeof(DenseCombatProbeRelic), typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent),
            typeof(FallingStar), typeof(Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RunEngine_DrivesFourWrigglersAfterRestRewardHooksAndEventResume()
    {
        (RunState runState, Player player, EventRoom room, DenseCombatProbeRelic probe) =
            await EnterRestedEvent("dense-engine-batch");
        var engine = new RunEngine(runState, options => options[0]);
        int goldBefore = player.Gold;

        await engine.DriveEventAsync(room);

        Assert.Equal(1, probe.CombatStartCount);
        Assert.Equal(4, probe.EnemyCountAtCombatStart);
        Assert.Equal(2, probe.RewardGenerationCount);
        Assert.True(player.Gold > goldBefore);
        Assert.True(room.Event.IsFinished);
        Assert.False(room.Event.HasPendingForcedCombat);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
    }

    [Fact]
    public async Task RunDriver_DrivesFourWrigglersAfterRestRewardHooksAndEventResume()
    {
        (RunState runState, Player player, EventRoom room, DenseCombatProbeRelic probe) =
            await EnterRestedEvent("dense-driver-batch");
        var decisions = new DenseCombatDecisionSource();
        var driver = new RunDriver(runState, decisions);
        int rewardDrawsBefore = player.PlayerRng.GetRng(PlayerRngType.Rewards).Counter;

        await driver.DriveEventAsync(room);

        Assert.Equal(4, decisions.EnemyCountAtFirstDecision);
        Assert.True(decisions.RewardDecisionCount > 0);
        Assert.True(decisions.SawNormalCombatRewards);
        Assert.True(player.PlayerRng.GetRng(PlayerRngType.Rewards).Counter > rewardDrawsBefore);
        Assert.Equal(1, probe.CombatStartCount);
        Assert.Equal(4, probe.EnemyCountAtCombatStart);
        Assert.Equal(2, probe.RewardGenerationCount);
        Assert.True(room.Event.IsFinished);
        Assert.False(room.Event.HasPendingForcedCombat);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
    }

    private static async Task<(RunState, Player, EventRoom, DenseCombatProbeRelic)> EnterRestedEvent(
        string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        player.Creature.SetMaxHpInternal(10_000m);
        player.Creature.HealInternal(10_000m);
        runState.AddPlayer(player);
        var probe = (DenseCombatProbeRelic)ModelDb.Relic<DenseCombatProbeRelic>().MutableClone();
        probe.AssignOwner(player);
        player.AddRelicInternal(probe);
        var room = new EventRoom(() =>
            (EventModel)ModelDb.Event<DenseVegetation>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        await room.Event.ChooseOption(room.Event.CurrentOptions.Single(option => option.Key == "REST"));
        return (runState, player, room, probe);
    }
}
