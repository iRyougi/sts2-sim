using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Exceptions;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

file sealed class ForcedCombatContractEvent : EventModel
{
    public void Request(Func<MonsterModel> monsterFactory) => RequestForcedCombat(monsterFactory);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => Array.Empty<EventOption>();
}

file sealed class ForcedCombatLifecycleMonster : MonsterModel
{
    public override int MinInitialHp => 1;
    public override int MaxInitialHp => 1;
    public int BeforeCombatStartCount { get; private set; }
    public AbstractRoom? RoomAtCombatStart { get; private set; }

    public override Task BeforeCombatStart()
    {
        BeforeCombatStartCount++;
        RoomAtCombatStart = Creature.CombatState!.RunState.CurrentRoom;
        return Task.CompletedTask;
    }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var wait = new MoveState("WAIT", _ => Task.CompletedTask, new SingleAttackIntent(1));
        wait.FollowUpState = wait;
        return new MonsterMoveStateMachine(new[] { wait }, wait);
    }
}

file sealed class ForcedCombatEnterFailureMonster : MonsterModel
{
    public const string FailureMessage = "forced combat failed during enter";
    public const string CleanupFailureMessage = "forced combat cleanup must not run";

    public override int MinInitialHp => 1;
    public override int MaxInitialHp => 1;

    public override Task BeforeCombatStart() =>
        throw new InvalidOperationException(FailureMessage);

    public override Task AfterCombatEnd() =>
        throw new InvalidOperationException(CleanupFailureMessage);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var wait = new MoveState("WAIT", _ => Task.CompletedTask, new SingleAttackIntent(1));
        wait.FollowUpState = wait;
        return new MonsterMoveStateMachine(new[] { wait }, wait);
    }
}

file sealed class AfterCombatEndProbeCard : CardModel
{
    public int TriggerCount { get; private set; }

    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };

    public override Task AfterCombatEnd()
    {
        TriggerCount++;
        return Task.CompletedTask;
    }
}

file sealed class ForcesCombatFactoryFailureEvent : EventModel
{
    public const string FailureMessage = "forced combat factory failed before engine creation";

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        new[]
        {
            new EventOption("FIGHT", () =>
            {
                RequestForcedCombat(() => throw new InvalidOperationException(FailureMessage));
                Finish();
                return Task.CompletedTask;
            }),
        };
}

file sealed class ForcesCombatEnterFailureEvent : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        new[]
        {
            new EventOption("FIGHT", () =>
            {
                RequestForcedCombat(() => (MonsterModel)ModelDb
                    .Monster<ForcedCombatEnterFailureMonster>()
                    .MutableClone());
                Finish();
                return Task.CompletedTask;
            }),
        };
}

file sealed class ForcesCombatEvent : EventModel
{
    public int FactoryInvocationCount { get; private set; }
    public ForcedCombatLifecycleMonster? CreatedMonster { get; private set; }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        new[]
        {
            new EventOption("FIGHT", () =>
            {
                RequestForcedCombat(() =>
                {
                    FactoryInvocationCount++;
                    CreatedMonster = (ForcedCombatLifecycleMonster)ModelDb
                        .Monster<ForcedCombatLifecycleMonster>()
                        .MutableClone();
                    return CreatedMonster;
                });
                Finish();
                return Task.CompletedTask;
            }),
        };
}

file sealed class FinishesWithoutCombatEvent : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        new[]
        {
            new EventOption("LEAVE", () =>
            {
                Finish();
                return Task.CompletedTask;
            }),
        };
}

internal sealed class RewardStateProbeRelic : RelicModel
{
    public int VictoryCount { get; private set; }
    public int AfterCombatEndCount { get; private set; }
    public AbstractRoom? RoomAtCombatEnd { get; private set; }
    public override Task AfterCombatVictory()
    {
        VictoryCount++;
        return Task.CompletedTask;
    }
    public override Task AfterCombatEnd()
    {
        AfterCombatEndCount++;
        RoomAtCombatEnd = Owner.RunState.CurrentRoom;
        return Task.CompletedTask;
    }    public override RelicRarity Rarity => RelicRarity.Common;
    public int ExtraRewardTakeCount { get; private set; }
    public int RewardGenerationCount { get; private set; }
    public override bool ShouldForcePotionReward(RoomType roomType) => true;

    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType)
    {
        RewardGenerationCount++;
        rewards.Add(new RewardStateProbe(player, this));
    }

    public void RecordExtraRewardTaken()
    {
        AssertMutable();
        ExtraRewardTakeCount++;
    }
}

file sealed class RewardStateProbe : TakeableReward
{
    private readonly RewardStateProbeRelic _relic;

    public RewardStateProbe(Player player, RewardStateProbeRelic relic) : base(player) => _relic = relic;

    public override void Populate(IRunState runState)
    {
    }

    protected override Task OnTake()
    {
        _relic.RecordExtraRewardTaken();
        var relic = (Circlet)ModelDb.Relic<Circlet>().MutableClone();
        relic.AssignOwner(Player);
        Player.AddRelicInternal(relic);
        return Task.CompletedTask;
    }
}

file sealed class ForcedCombatDecisionSource : IRunDecisionSource
{
    private readonly RunState _runState;

    public ForcedCombatDecisionSource(RunState runState) => _runState = runState;

    public int CombatDecisionCount { get; private set; }
    public int RewardDecisionCount { get; private set; }
    public AbstractRoom? RoomAtFirstCombatDecision { get; private set; }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => Task.FromResult(options[0]);

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
    {
        CombatDecisionCount++;
        RoomAtFirstCombatDecision ??= _runState.CurrentRoom;
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
        if (!rewards.Gold.IsResolved)
        {
            return Task.FromResult<RewardDecision>(new RewardDecision.TakeGold());
        }
        if (rewards.Potion is { IsResolved: false })
        {
            return Task.FromResult<RewardDecision>(new RewardDecision.TakePotion());
        }
        if (rewards.Relic is { IsResolved: false })
        {
            return Task.FromResult<RewardDecision>(new RewardDecision.TakeRelic());
        }
        if (!rewards.Card.IsResolved)
        {
            return Task.FromResult<RewardDecision>(rewards.Card.Options.Count > 0
                ? new RewardDecision.TakeCard(rewards.Card.Options[0])
                : new RewardDecision.SkipCard());
        }
        Reward? extra = rewards.ExtraRewards.FirstOrDefault(reward => !reward.IsResolved);
        return Task.FromResult<RewardDecision>(extra is null
            ? new RewardDecision.Done()
            : new RewardDecision.ResolveExtra(extra));
    }
}

[Collection("ModelDb")]
public sealed class EventForcedCombatTests : IDisposable
{
    public EventForcedCombatTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(ForcedCombatContractEvent), typeof(ForcesCombatEvent), typeof(ForcesCombatFactoryFailureEvent),
            typeof(ForcesCombatEnterFailureEvent), typeof(FinishesWithoutCombatEvent),
            typeof(ForcedCombatLifecycleMonster), typeof(ForcedCombatEnterFailureMonster),
            typeof(WanderingGrunt),
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(AfterCombatEndProbeCard),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight), typeof(RewardStateProbeRelic), typeof(Circlet),
            typeof(Begone), typeof(Alignment), typeof(BeaconOfHope),
            typeof(StrengthPotion), typeof(Duplicator), typeof(BeetleJuice),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void PendingForcedCombat_RejectsNullDuplicateAndEmptyDequeueWithoutLosingFirstFactory()
    {
        var source = (ForcedCombatContractEvent)ModelDb.Event<ForcedCombatContractEvent>().MutableClone();
        Func<MonsterModel> first = () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone();
        Func<MonsterModel> duplicate = () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone();

        Assert.Throws<ArgumentNullException>(() => source.Request(null!));
        source.Request(first);
        Assert.Throws<InvalidOperationException>(() => source.Request(duplicate));

        Assert.True(source.HasPendingForcedCombat);
        Assert.Same(first, source.DequeuePendingForcedCombat());
        Assert.False(source.HasPendingForcedCombat);
        Assert.Throws<InvalidOperationException>(() => source.DequeuePendingForcedCombat());
    }

    [Fact]
    public void PendingForcedCombat_CloneStartsEmptyWithoutConsumingSource()
    {
        var source = (ForcedCombatContractEvent)ModelDb.Event<ForcedCombatContractEvent>().MutableClone();
        Func<MonsterModel> factory = () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone();
        source.Request(factory);

        var clone = (ForcedCombatContractEvent)source.MutableClone();

        Assert.True(source.HasPendingForcedCombat);
        Assert.False(clone.HasPendingForcedCombat);
        Assert.Same(factory, source.DequeuePendingForcedCombat());
        Assert.Throws<InvalidOperationException>(() => clone.DequeuePendingForcedCombat());
    }

    [Fact]
    public void RequestForcedCombat_RejectsCanonicalEventMutation()
    {
        ForcedCombatContractEvent canonical = ModelDb.Event<ForcedCombatContractEvent>();

        Assert.Throws<CanonicalModelException>(
            () => canonical.Request(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone()));
    }

    [Fact]
    public async Task RunEngine_DrivesForcedCombatOnceAboveEventRoomWithoutGrantingRewards()
    {
        (RunState runState, Player player, RewardStateProbeRelic rewardProbe) =
            CreateRunWithPlayer("forced-combat-engine");
        EventRoom room = await EnterRoom(runState, ModelDb.Event<ForcesCombatEvent>());
        var engine = new RunEngine(runState, points => points[0]);
        RewardSnapshot rewardsBefore = RewardSnapshot.Capture(player, rewardProbe);

        await engine.DriveEventAsync(room);
        await engine.DriveEventAsync(room);

        var @event = Assert.IsType<ForcesCombatEvent>(room.Event);
        ForcedCombatLifecycleMonster monster = Assert.IsType<ForcedCombatLifecycleMonster>(@event.CreatedMonster);
        Assert.Equal(1, @event.FactoryInvocationCount);
        Assert.False(@event.HasPendingForcedCombat);
        Assert.Equal(1, monster.BeforeCombatStartCount);
        Assert.Equal(1, rewardProbe.VictoryCount);
        Assert.Equal(1, rewardProbe.AfterCombatEndCount);
        Assert.IsType<CombatRoom>(monster.RoomAtCombatStart);
        Assert.IsType<CombatRoom>(rewardProbe.RoomAtCombatEnd);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
        Assert.Equal(rewardsBefore, RewardSnapshot.Capture(player, rewardProbe));
    }

    [Fact]
    public async Task RunDriver_DrivesForcedCombatOnceAboveEventRoomWithoutGrantingRewards()
    {
        (RunState runState, Player player, RewardStateProbeRelic rewardProbe) =
            CreateRunWithPlayer("forced-combat-driver");
        EventRoom room = await EnterRoom(runState, ModelDb.Event<ForcesCombatEvent>());
        var decisions = new ForcedCombatDecisionSource(runState);
        var driver = new RunDriver(runState, decisions);
        RewardSnapshot rewardsBefore = RewardSnapshot.Capture(player, rewardProbe);

        await driver.DriveEventAsync(room);
        await driver.DriveEventAsync(room);

        var @event = Assert.IsType<ForcesCombatEvent>(room.Event);
        ForcedCombatLifecycleMonster monster = Assert.IsType<ForcedCombatLifecycleMonster>(@event.CreatedMonster);
        Assert.Equal(1, @event.FactoryInvocationCount);
        Assert.False(@event.HasPendingForcedCombat);
        Assert.True(decisions.CombatDecisionCount > 0);
        Assert.Equal(0, decisions.RewardDecisionCount);
        Assert.IsType<CombatRoom>(decisions.RoomAtFirstCombatDecision);
        Assert.Equal(1, monster.BeforeCombatStartCount);
        Assert.Equal(1, rewardProbe.VictoryCount);
        Assert.Equal(1, rewardProbe.AfterCombatEndCount);
        Assert.Same(decisions.RoomAtFirstCombatDecision, rewardProbe.RoomAtCombatEnd);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
        Assert.Equal(rewardsBefore, RewardSnapshot.Capture(player, rewardProbe));
    }

    [Fact]
    public async Task RunEngine_ForcedCombatEnterFailureRestoresEventRoomAndPlayerState()
    {
        (RunState runState, Player player, _) = CreateRunWithPlayer("forced-combat-enter-failure-engine");
        EventRoom room = await EnterRoom(runState, ModelDb.Event<ForcesCombatEnterFailureEvent>());
        var probe = (AfterCombatEndProbeCard)ModelDb.Card<AfterCombatEndProbeCard>().MutableClone();
        probe.AssignOwner(player);
        await CardPileCmd.AddToDeck(probe);
        var engine = new RunEngine(runState, points => points[0]);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.DriveEventAsync(room));

        Assert.Equal(ForcedCombatEnterFailureMonster.FailureMessage, error.Message);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
        Assert.Equal(0, probe.TriggerCount);
    }

    [Fact]
    public async Task RunDriver_ForcedCombatEnterFailureRestoresEventRoomAndPlayerState()
    {
        (RunState runState, Player player, _) = CreateRunWithPlayer("forced-combat-enter-failure-driver");
        EventRoom room = await EnterRoom(runState, ModelDb.Event<ForcesCombatEnterFailureEvent>());
        var probe = (AfterCombatEndProbeCard)ModelDb.Card<AfterCombatEndProbeCard>().MutableClone();
        probe.AssignOwner(player);
        await CardPileCmd.AddToDeck(probe);
        var driver = new RunDriver(runState, new ForcedCombatDecisionSource(runState));

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.DriveEventAsync(room));

        Assert.Equal(ForcedCombatEnterFailureMonster.FailureMessage, error.Message);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
        Assert.Equal(0, probe.TriggerCount);
    }

    [Fact]
    public async Task RunEngine_ForcedCombatFactoryFailureRestoresEventRoomWithoutExitCleanup()
    {
        (RunState runState, Player player, _) = CreateRunWithPlayer("forced-combat-factory-failure-engine");
        EventRoom room = await EnterRoom(runState, ModelDb.Event<ForcesCombatFactoryFailureEvent>());
        var engine = new RunEngine(runState, points => points[0]);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.DriveEventAsync(room));

        Assert.Equal(ForcesCombatFactoryFailureEvent.FailureMessage, error.Message);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
    }

    [Fact]
    public async Task RunDriver_ForcedCombatFactoryFailureRestoresEventRoomWithoutExitCleanup()
    {
        (RunState runState, Player player, _) = CreateRunWithPlayer("forced-combat-factory-failure-driver");
        EventRoom room = await EnterRoom(runState, ModelDb.Event<ForcesCombatFactoryFailureEvent>());
        var driver = new RunDriver(runState, new ForcedCombatDecisionSource(runState));

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.DriveEventAsync(room));

        Assert.Equal(ForcesCombatFactoryFailureEvent.FailureMessage, error.Message);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
    }

    [Fact]
    public async Task ForcedCombat_DoesNotChangeTheNextNormalRewardForTheSameSeed()
    {
        (RunState forcedRun, Player forcedPlayer, RewardStateProbeRelic forcedProbe) =
            CreateRunWithPlayer("forced-combat-next-reward");
        (RunState controlRun, Player controlPlayer, RewardStateProbeRelic controlProbe) =
            CreateRunWithPlayer("forced-combat-next-reward");
        EventRoom room = await EnterRoom(forcedRun, ModelDb.Event<ForcesCombatEvent>());
        var engine = new RunEngine(forcedRun, points => points[0]);

        await engine.DriveEventAsync(room);

        GeneratedRewardSnapshot forcedReward = GenerateRewardSnapshot(forcedPlayer, forcedRun);
        GeneratedRewardSnapshot controlReward = GenerateRewardSnapshot(controlPlayer, controlRun);
        Assert.Equal(controlReward, forcedReward);
        Assert.Equal(controlPlayer.PlayerRng.Rewards.Counter, forcedPlayer.PlayerRng.Rewards.Counter);
        Assert.Equal(controlPlayer.Odds.CardRarity.CurrentValue, forcedPlayer.Odds.CardRarity.CurrentValue);
        Assert.Equal(controlPlayer.Odds.PotionReward.CurrentValue, forcedPlayer.Odds.PotionReward.CurrentValue);
        Assert.Equal(1, forcedProbe.RewardGenerationCount);
        Assert.Equal(1, controlProbe.RewardGenerationCount);
    }

    [Fact]
    public async Task CombatRoom_ResolveOutcomeWithoutRewardGenerationIsOneShotAndLeavesRewardsNull()
    {
        (RunState runState, Player player, RewardStateProbeRelic rewardProbe) =
            CreateRunWithPlayer("forced-combat-outcome-none");
        CombatRoom room = await EnterWonCombat(runState);
        RewardSnapshot before = RewardSnapshot.Capture(player, rewardProbe);

        Task first = room.ResolveOutcomeAsync(generateRewards: false);
        Task concurrent = room.ResolveOutcomeAsync(generateRewards: false);
        Assert.Same(first, concurrent);
        await Task.WhenAll(first, concurrent);
        Task retryWithRewards = room.ResolveOutcomeAsync();
        Assert.Same(first, retryWithRewards);
        await retryWithRewards;

        Assert.True(room.Won);
        Assert.Null(room.Rewards);
        Assert.Equal(before, RewardSnapshot.Capture(player, rewardProbe));
        await room.Exit(runState);
        Assert.Same(room, runState.PopCurrentRoom());
    }

    [Fact]
    public async Task CombatRoom_DefaultResolveOutcomeStillGeneratesRewardsOnce()
    {
        (RunState runState, _, RewardStateProbeRelic rewardProbe) =
            CreateRunWithPlayer("forced-combat-outcome-default");
        CombatRoom room = await EnterWonCombat(runState);

        Task first = room.ResolveOutcomeAsync();
        Task concurrent = room.ResolveOutcomeAsync();
        Assert.Same(first, concurrent);
        await Task.WhenAll(first, concurrent);
        RewardsSet rewards = Assert.IsType<RewardsSet>(room.Rewards);
        Task retryWithoutRewards = room.ResolveOutcomeAsync(generateRewards: false);
        Assert.Same(first, retryWithoutRewards);
        await retryWithoutRewards;

        Assert.True(room.Won);
        Assert.Same(rewards, room.Rewards);
        Assert.Equal(1, rewardProbe.RewardGenerationCount);
        await room.Exit(runState);
        Assert.Same(room, runState.PopCurrentRoom());
    }

    [Fact]
    public async Task RunDriver_FinishedEventWithoutRequestDoesNotEnterCombat()
    {
        (RunState runState, Player player, RewardStateProbeRelic rewardProbe) =
            CreateRunWithPlayer("forced-combat-none");
        EventRoom room = await EnterRoom(runState, ModelDb.Event<FinishesWithoutCombatEvent>());
        var decisions = new ForcedCombatDecisionSource(runState);
        var driver = new RunDriver(runState, decisions);
        RewardSnapshot rewardsBefore = RewardSnapshot.Capture(player, rewardProbe);

        await driver.DriveEventAsync(room);

        Assert.Equal(0, decisions.CombatDecisionCount);
        Assert.Equal(0, decisions.RewardDecisionCount);
        Assert.Same(room, runState.CurrentRoom);
        Assert.Equal(rewardsBefore, RewardSnapshot.Capture(player, rewardProbe));
    }

    private static (RunState RunState, Player Player, RewardStateProbeRelic RewardProbe)
        CreateRunWithPlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var rewardProbe = (RewardStateProbeRelic)ModelDb.Relic<RewardStateProbeRelic>().MutableClone();
        rewardProbe.AssignOwner(player);
        player.AddRelicInternal(rewardProbe);
        return (runState, player, rewardProbe);
    }

    private static async Task<CombatRoom> EnterWonCombat(RunState runState)
    {
        var room = new CombatRoom(() =>
            (MonsterModel)ModelDb.Monster<ForcedCombatLifecycleMonster>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        room.Engine.State.Enemies[0].LoseHpInternal(decimal.MaxValue, default);
        room.Engine.CheckWinCondition();
        Assert.False(room.Engine.IsInProgress);
        Assert.True(room.Engine.Won);
        return room;
    }

    private static GeneratedRewardSnapshot GenerateRewardSnapshot(Player player, RunState runState)
    {
        RewardsSet rewards = RewardsSet.GenerateFor(player, RoomType.Monster, runState);
        return new GeneratedRewardSnapshot(
            rewards.Gold.Amount,
            rewards.Potion?.Potion?.GetType(),
            string.Join(",", rewards.Card.Options.Select(card => card.Id.Entry)));
    }

    private sealed record GeneratedRewardSnapshot(decimal Gold, Type? PotionType, string CardOptionIds);

    private static async Task<EventRoom> EnterRoom(RunState runState, EventModel prototype)
    {
        var room = new EventRoom(() => (EventModel)prototype.MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        return room;
    }

    private sealed record RewardSnapshot(
        int Gold,
        int DeckCards,
        int Potions,
        int Relics,
        int ExtraRewardsTaken,
        int RewardGenerationCount,
        float CardRarityOdds,
        float PotionRewardOdds,
        int RewardRngCounter)
    {
        public static RewardSnapshot Capture(Player player, RewardStateProbeRelic rewardProbe) =>
            new(
                player.Gold,
                player.Deck.Cards.Count,
                player.PotionSlots.Count(potion => potion is not null),
                player.Relics.Count,
                rewardProbe.ExtraRewardTakeCount,
                rewardProbe.RewardGenerationCount,
                player.Odds.CardRarity.CurrentValue,
                player.Odds.PotionReward.CurrentValue,
                player.PlayerRng.Rewards.Counter);
    }
}
