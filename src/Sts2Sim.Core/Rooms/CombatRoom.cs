using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rooms;

/// <summary>
/// Combat room that drives the Plan 03 <see cref="CombatEngine"/>.
/// 偏离 #47：现支持显式怪物批次和槽位，但仍不移植真实 <c>EncounterModel</c> 的场景与奖励表面。
/// </summary>
public sealed class CombatRoom : AbstractRoom
{
    private readonly Func<MonsterModel>? _monsterFactory;
    private readonly Func<IReadOnlyList<MonsterModel>>? _monsterBatchFactory;
    private readonly Func<(EncounterDefinition Encounter, IReadOnlyList<(MonsterModel Monster, string? SlotName)> Monsters)>? _encounterFactory;
    private readonly Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>>? _slottedMonsterBatchFactory;
    private readonly object _lifecycleLock = new();
    private CombatState? _preparedCombatState;
    private readonly AsyncLocal<bool> _insideLifecycle = new();
    private readonly Dictionary<Player, List<Reward>> _pendingExtraRewards =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Reward> _unpopulatedExtraRewards = new(ReferenceEqualityComparer.Instance);
    private Task? _outcomeTask;
    private Task? _exitTask;
    private Task? _completionTask;
    private bool _outcomeCompleted;
    private bool _exitCompleted;
    private bool _completionCompleted;
    internal int? FixedGoldAmount { get; init; }
    public const string CustomEncounterName = "custom-combat";

    public const string ForcedEncounterName = "forced-combat";

    public EncounterDefinition? Encounter { get; private set; }

    public string EncounterName { get; private set; }


    private Action<CombatRoom, CombatState>? _beforeSetupDiagnostic;

    internal void ConfigureBeforeSetupDiagnostic(Action<CombatRoom, CombatState> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (_beforeSetupDiagnostic is not null || Engine is not null)
            throw new InvalidOperationException("Combat setup diagnostic is already configured.");
        _beforeSetupDiagnostic = callback;
    }

    private ICombatObserver? _observer;
    private ICardSelectionDecisionSource _cardSelectionSource =
        RejectingCardSelectionDecisionSource.Instance;
    public override RoomType RoomType { get; }

    public override ModelId? ModelId { get; }

    public CombatEngine Engine { get; private set; } = null!;

    public bool Won { get; private set; }

    public float GoldProportion { get; private set; } = 1f;

    public IReadOnlyList<RewardsSet> GeneratedRewards { get; private set; } = Array.Empty<RewardsSet>();

    public RewardsSet? Rewards => GeneratedRewards.FirstOrDefault();

    public CombatRoom(Func<MonsterModel> monsterFactory, RoomType roomType = RoomType.Monster)
        : this(monsterFactory, roomType, CustomEncounterName)
    {
    }

    public CombatRoom(
        Func<MonsterModel> monsterFactory,
        RoomType roomType,
        string encounterName)
    {
        _monsterFactory = monsterFactory ?? throw new ArgumentNullException(nameof(monsterFactory));
        RoomType = roomType;
        EncounterName = NormalizeEncounterName(encounterName);
        ModelId = null;
    }

    public CombatRoom(
        Func<IReadOnlyList<MonsterModel>> monsterBatchFactory,
        RoomType roomType = RoomType.Monster)
        : this(monsterBatchFactory, roomType, CustomEncounterName)
    {
    }

    public CombatRoom(
        Func<IReadOnlyList<MonsterModel>> monsterBatchFactory,
        RoomType roomType,
        string encounterName)
    {
        _monsterBatchFactory = monsterBatchFactory
            ?? throw new ArgumentNullException(nameof(monsterBatchFactory));
        RoomType = roomType;
        EncounterName = NormalizeEncounterName(encounterName);
        ModelId = null;
    }

    public CombatRoom(
        Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>> slottedMonsterBatchFactory,
        RoomType roomType = RoomType.Monster)
        : this(slottedMonsterBatchFactory, roomType, CustomEncounterName)
    {
    }

    public CombatRoom(
        Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>> slottedMonsterBatchFactory,
        RoomType roomType,
        string encounterName)
    {
        _slottedMonsterBatchFactory = slottedMonsterBatchFactory
            ?? throw new ArgumentNullException(nameof(slottedMonsterBatchFactory));
        RoomType = roomType;
        EncounterName = NormalizeEncounterName(encounterName);
        ModelId = null;
    }

    internal CombatRoom(
        Func<(EncounterDefinition Encounter, IReadOnlyList<(MonsterModel Monster, string? SlotName)> Monsters)>
            encounterFactory,
        RoomType roomType)
    {
        _encounterFactory = encounterFactory ?? throw new ArgumentNullException(nameof(encounterFactory));
        RoomType = roomType;
        EncounterName = EncounterDefinition.DefaultName;
        ModelId = null;
    }

    internal IEnumerable<CombatState> EnumerateRunCloneStates()
    {
        if (_preparedCombatState is not null) yield return _preparedCombatState;
        if (Engine is not null && !ReferenceEquals(Engine.State, _preparedCombatState))
            yield return Engine.State;
    }

    internal void AssertRunCloneBoundary()
    {
        if (Engine?.IsInProgress == true)
            throw new RunCloneNotSupportedException(RunCloneRejectionReason.ActiveCombat,
                "The combat room is still active.");
        if (_insideLifecycle.Value || Engine?.IsStarting == true
            || _outcomeTask is { IsCompletedSuccessfully: false }
            || _exitTask is { IsCompletedSuccessfully: false }
            || _completionTask is { IsCompletedSuccessfully: false })
            throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                "The combat room has no successful stable lifecycle boundary.");
    }

    internal CombatRoom CloneForRun(RunState targetRun,
        Func<Player, Player> mapPlayer,
        Func<Reward, Reward> mapReward,
        Func<RewardsSet, RewardsSet> mapOffer,
        Func<CombatState, CombatState> mapState,
        Func<Delegate, Delegate> mapCallback,
        Action<CombatRoom, CombatRoom> registerRoom)
    {
        AssertRunCloneBoundary();
        return new CombatRoom(this, targetRun, mapPlayer, mapReward,
            mapOffer, mapState, mapCallback, registerRoom);
    }

    private CombatRoom(CombatRoom source, RunState targetRun,
        Func<Player, Player> mapPlayer,
        Func<Reward, Reward> mapReward,
        Func<RewardsSet, RewardsSet> mapOffer,
        Func<CombatState, CombatState> mapState,
        Func<Delegate, Delegate> mapCallback,
        Action<CombatRoom, CombatRoom> registerRoom)
    {
        // Publish the shell before mapping a closure that refers back to this room.
        registerRoom(source, this);
        RoomType = source.RoomType;
        ModelId = source.ModelId;
        Encounter = source.Encounter;
        EncounterName = source.EncounterName;
        FixedGoldAmount = source.FixedGoldAmount;
        Won = source.Won;
        GoldProportion = source.GoldProportion;
        _outcomeCompleted = source._outcomeCompleted;
        _exitCompleted = source._exitCompleted;
        _completionCompleted = source._completionCompleted;
        CopyEntryStateFrom(source);
        _preparedCombatState = source._preparedCombatState is null ? null : mapState(source._preparedCombatState);
        Engine = source.Engine is null ? null! : mapState(source.Engine.State).Engine!;
        _cardSelectionSource = targetRun.CardSelectionSource;
        _monsterFactory = source._monsterFactory is null ? null : (Func<MonsterModel>)mapCallback(source._monsterFactory);
        _monsterBatchFactory = source._monsterBatchFactory is null ? null : (Func<IReadOnlyList<MonsterModel>>)mapCallback(source._monsterBatchFactory);
        _encounterFactory = source._encounterFactory is null ? null : (Func<(EncounterDefinition Encounter, IReadOnlyList<(MonsterModel Monster, string? SlotName)> Monsters)>)mapCallback(source._encounterFactory);
        _slottedMonsterBatchFactory = source._slottedMonsterBatchFactory is null ? null : (Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>>)mapCallback(source._slottedMonsterBatchFactory);
        _beforeSetupDiagnostic = source._beforeSetupDiagnostic is null ? null : (Action<CombatRoom, CombatState>)mapCallback(source._beforeSetupDiagnostic);
        foreach ((Player player, List<Reward> rewards) in source._pendingExtraRewards)
            _pendingExtraRewards.Add(mapPlayer(player), rewards.Select(mapReward).ToList());
        foreach (Reward reward in source._unpopulatedExtraRewards)
            _unpopulatedExtraRewards.Add(mapReward(reward));
        GeneratedRewards = Array.AsReadOnly(source.GeneratedRewards.Select(mapOffer).ToArray());
        // Tasks, the lifecycle lock, AsyncLocal and observer are not copied.
    }


    internal void ConfigureObserver(ICombatObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (Engine is not null)
        {
            throw new InvalidOperationException("A combat observer must be configured before room entry.");
        }

        if (_observer is not null)
        {
            throw new InvalidOperationException("A combat observer has already been configured.");
        }

        _observer = observer;
    }

    internal void ConfigureCardSelectionSource(ICardSelectionDecisionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (Engine is not null)
        {
            throw new InvalidOperationException(
                "A card selection source must be configured before room entry.");
        }

        if (!ReferenceEquals(_cardSelectionSource, RejectingCardSelectionDecisionSource.Instance))
        {
            throw new InvalidOperationException("A card selection source has already been configured.");
        }

        _cardSelectionSource = source;
    }

    public override Task EnterInternal(RunState? runState) => EnterCombatAsync(runState, afterSetup: null);

    protected override Task EnterWithEntryHooksAsync(RunState? runState) =>
        EnterCombatAsync(runState, () => Hook.AfterRoomEntered(runState!, this));

    internal void Prepare(RunState runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        if (_preparedCombatState is not null)
        {
            if (!ReferenceEquals(_preparedCombatState.RunState, runState))
            {
                throw new InvalidOperationException("A prepared combat cannot be used with another run.");
            }

            return;
        }

        IReadOnlyList<(MonsterModel Monster, string? SlotName)>? generated;
        if (_encounterFactory is not null)
        {
            (EncounterDefinition encounter, IReadOnlyList<(MonsterModel Monster, string? SlotName)> encounterMonsters) =
                _encounterFactory();
            Encounter = encounter
                ?? throw new InvalidOperationException("Encounter factory returned no definition.");
            EncounterName = encounter.Name;
            generated = encounterMonsters;
        }
        else if (_slottedMonsterBatchFactory is not null)
        {
            generated = _slottedMonsterBatchFactory();
        }
        else
        {
            IReadOnlyList<MonsterModel>? unslotted = _monsterBatchFactory is null
                ? new[] { _monsterFactory!() }
                : _monsterBatchFactory();
            generated = unslotted?.Select(monster => (monster, (string?)null)).ToArray();
        }

        if (generated is null)
        {
            throw new InvalidOperationException("Monster batch factory returned null.");
        }

        var combatState = new CombatState(runState, Encounter?.Slots ?? Array.Empty<string>());
        List<(MonsterModel Monster, string? SlotName)> monsters = generated.ToList();
        if (monsters.Count == 0)
        {
            throw new InvalidOperationException("Combat requires at least one monster.");
        }

        if (monsters.Any(entry => entry.Monster is null))
        {
            throw new InvalidOperationException("Combat monster batches cannot contain null.");
        }
        if (monsters.Any(entry => !entry.Monster.IsMutable))
        {
            throw new InvalidOperationException("Combat monster batches must contain mutable models.");
        }
        if (monsters.Select(entry => entry.Monster).Distinct(ReferenceEqualityComparer.Instance).Count() != monsters.Count)
        {
            throw new InvalidOperationException("Combat monster batches cannot reuse the same model instance.");
        }

        // Native CombatRoom.EnterInternal registers players before StartCombat creates monsters.
        // Their CombatIds therefore occupy the first slots of this combat's creature ID sequence.
        foreach (Player player in runState.Players)
        {
            combatState.AddPlayerCreature(player.Creature);
        }

        foreach ((MonsterModel monster, string? slotName) in monsters)
        {
            combatState.AddMonster(monster, CombatSide.Enemy, slotName);
        }

        combatState.SortEnemiesBySlotName();
        _preparedCombatState = combatState;
    }

    private async Task EnterCombatAsync(RunState? runState, Func<Task>? afterSetup)
    {
        ArgumentNullException.ThrowIfNull(runState);
        Prepare(runState);
        CombatState combatState = _preparedCombatState!;
        _beforeSetupDiagnostic?.Invoke(this, combatState);
        if (runState is not null
            && _cardSelectionSource is ICombatOpeningPlanner { RequiresOpeningSnapshot: true } planner)
            await planner.PrepareOpeningAsync(new CombatOpeningContext(runState, this,
                afterRoomEntered: afterSetup is not null));
        combatState.CardSelectionSource = _cardSelectionSource;
        Engine = new CombatEngine(combatState, _observer);
        await Engine.StartCombatAsync(afterSetup);
    }

    // Only CombatOpeningContext invokes this on an independent, already prepared room.
    // Rebuild the hook against the cloned graph; never copy the live afterSetup closure.
    internal CombatState PreparedOpeningState => _preparedCombatState
        ?? throw new InvalidOperationException("Combat has not been prepared.");

    internal async Task ReplayPreparedOpeningAsync(RunState runState,
        ICardSelectionDecisionSource selection, bool afterRoomEntered,
        ICombatOpeningObserver? observer = null)
    {
        if (_preparedCombatState is null || Engine is not null
            || !ReferenceEquals(_preparedCombatState.RunState, runState))
            throw new InvalidOperationException("Opening replay requires an unstarted prepared combat.");
        _preparedCombatState.CardSelectionSource = selection;
        Engine = new CombatEngine(_preparedCombatState, observer is null ? null
            : new CombatOpeningObserverAdapter(_preparedCombatState, observer));
        observer?.OpeningStarted(_preparedCombatState);
        await Engine.StartCombatAsync(afterRoomEntered
            ? () => Hook.AfterRoomEntered(runState, this) : null);
    }

    private static string NormalizeEncounterName(string encounterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterName);
        return encounterName;
    }

    internal async Task RollbackFailedEnterAsync()
    {
        if (Engine is null)
        {
            return;
        }

        foreach (Player player in Engine.State.Players)
        {
            await player.RollbackCombatStartAsync();
        }
    }

    public void AddExtraReward(Player player, Reward reward) => AddExtraRewardCore(player, reward);

    internal void QueueExtraReward(Player player, Reward reward) => AddExtraRewardCore(player, reward);

    private void AddExtraRewardCore(Player player, Reward reward)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(reward);
        if (!Engine.State.Players.Contains(player))
        {
            throw new ArgumentException("Extra rewards can only be added for a player in this combat.", nameof(player));
        }

        if (!_pendingExtraRewards.TryGetValue(player, out List<Reward>? rewards))
        {
            rewards = [];
            _pendingExtraRewards.Add(player, rewards);
        }

        // Native CombatRoom only appends here; the initial reward-set pass populates
        // every pre-existing reward after the ordinary gold, potion, and card rewards.
        _unpopulatedExtraRewards.Add(reward);
        rewards.Add(reward);
    }

    // GenerateFor enumerates extras after its ordinary rewards are populated and before modifiers.
    // No-reward victories never enumerate this sequence or spend reward RNG.
    private IEnumerable<Reward> PendingExtraRewards(Player player)
    {
        if (!_pendingExtraRewards.TryGetValue(player, out List<Reward>? rewards)) yield break;
        foreach (Reward reward in rewards)
        {
            if (_unpopulatedExtraRewards.Remove(reward)) reward.Populate(Engine.State.RunState);
            yield return reward;
        }
    }
    public Task ResolveOutcomeAsync(bool generateRewards = true)
    {
        RejectLifecycleReentry();
        lock (_lifecycleLock)
        {
            if (_outcomeCompleted) return _outcomeTask ??= Task.CompletedTask;
            if (_outcomeTask is not null) return _outcomeTask;
            if (_exitTask is not null)
                throw new InvalidOperationException("Cannot resolve an outcome after room exit has started.");
            if (Engine.IsInProgress || !Engine.Won) return Task.CompletedTask;
            return PublishLifecycleTask(ref _outcomeTask, () => ResolveVictoryOnceAsync(generateRewards));
        }
    }

    private void RejectLifecycleReentry()
    {
        if (_insideLifecycle.Value)
            throw new InvalidOperationException("Combat completion callbacks cannot recursively resolve or exit their room.");
    }

    // The caller holds the one lifecycle lock. Publish before invoking any user/model callback.
    private Task PublishLifecycleTask(ref Task? slot, Func<Task> operation)
    {
        if (slot is not null) return slot;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        slot = completion.Task;
        _ = RunPublishedLifecycleAsync(completion, operation);
        return slot;
    }

    private async Task RunPublishedLifecycleAsync(TaskCompletionSource completion, Func<Task> operation)
    {
        bool previous = _insideLifecycle.Value;
        _insideLifecycle.Value = true;
        try
        {
            await operation();
            if (ReferenceEquals(_outcomeTask, completion.Task)) _outcomeCompleted = true;
            if (ReferenceEquals(_exitTask, completion.Task)) _exitCompleted = true;
            if (ReferenceEquals(_completionTask, completion.Task)) _completionCompleted = true;
            completion.TrySetResult();
        }
        catch (OperationCanceledException exception)
        {
            completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            _insideLifecycle.Value = previous;
        }
    }
    /// <summary>偏离 #311（2026-09-08）：权威 <c>RewardsSet.WithRewardsFromRoom</c> 的第一件事就是
    /// 在最终幕 Boss 房会直接返回原奖励集合，不生成任何奖励。此前本仓库整条门缺失。
    ///
    /// 对 A10 是直接影响：DoubleBoss 的两场都在最终幕，按权威两场都应零奖励，
    /// 缺门会让终局白得一次完整奖励（含一张卡进牌组），并多消耗金币/药水 roll/卡牌抽取三处 RNG。
    ///
    /// 用模式匹配而非给 <c>IRunState</c> 加成员：幕信息只在 <see cref="RunState"/> 上，
    /// 而 <c>IRunState</c> 有大量测试替身实现。</summary>
    private bool IsFinalActBoss() =>
        RoomType == RoomType.Boss &&
        Engine.State.RunState is RunState runState &&
        runState.CurrentActIndex >= runState.Acts.Count - 1;

    private async Task ResolveVictoryOnceAsync(bool generateRewards)
    {
        Won = true;
        await CompleteCombatAsync();
        await Sts2Sim.Core.Hooks.Hook.AfterCombatVictory(Engine.State);
        if (generateRewards && !IsFinalActBoss())
        {
            GoldProportion = Encounter?.CalculateGoldProportion(Engine.State)
                ?? EncounterDefinition.CalculateDefaultGoldProportion(Engine.State);
            GeneratedRewards = Array.AsReadOnly(Engine.State.Players
                .Select(player => RewardsSet.GenerateFor(
                    player,
                    RoomType,
                    Engine.State.RunState,
                    PendingExtraRewards(player),
                    FixedGoldAmount,
                    Encounter,
                    GoldProportion))
                .ToArray());
            // Generate every player's options before any pre-offer listener advances its state.
            // The memoized outcome task exposes these sets to the headless resolution boundary only after callbacks complete.
            foreach (RewardsSet rewards in GeneratedRewards)
                await Sts2Sim.Core.Hooks.Hook.BeforeCombatRewardOffered(rewards, Engine.State.RunState, this);
        }
    }

    public override Task Exit(RunState? runState)
    {
        RejectLifecycleReentry();
        lock (_lifecycleLock)
        {
            if (_exitCompleted) return _exitTask ??= Task.CompletedTask;
            return PublishLifecycleTask(ref _exitTask, ExitOnceAsync);
        }
    }

    private Task CompleteCombatAsync()
    {
        lock (_lifecycleLock)
        {
            if (_completionCompleted) return _completionTask ??= Task.CompletedTask;
            return PublishLifecycleTask(ref _completionTask, CompleteCombatOnceAsync);
        }
    }

    private async Task CompleteCombatOnceAsync()
    {
        try
        {
            using IDisposable rngScope = Engine.State.BeginPhaseRngScope("combat_end");
            if (Engine.Won)
                await Sts2Sim.Core.Hooks.Hook.AfterCombatEnd(Engine.State);
        }
        finally
        {
            // Pending loss skips the source's victory teardown, preserving game-over block.
            // Keep existing cleanup for victory and for Exit during active combat.
            if (Engine.Won || Engine.IsInProgress)
            {
                foreach (Player player in Engine.State.Players)
                    player.ClearCombatForVictory();
            }
        }
    }

    private async Task ExitOnceAsync()
    {
        try
        {
            // A concurrently requested exit must wait through victory and reward callbacks.
            await (_outcomeTask ?? CompleteCombatAsync());
        }
        finally
        {
            foreach (Player player in Engine.State.Players)
                player.DetachCombatState();
        }
    }
}
