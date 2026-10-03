using System.Diagnostics.CodeAnalysis;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models;

/// <summary>Base class for mutable, multi-page event state machines.</summary>
public abstract class EventModel : AbstractModel
{
    private object _stateLock = new();
    private Player? _owner;
    private IRunState? _runState;
    private Rng? _rng;
    private Task? _activeChoiceTask;
    private EventOption? _activeChoiceOption;
    private int _activeChoicePageVersion;
    private int _pageVersion;
    private bool _hasBegun;
    private bool _isAwaitingForcedCombat;
    private Queue<RewardsSet> _pendingRewardOffers = new();
    private Stack<Queue<RewardsSet>> _suspendedRewardOffers = new();
    private Func<MonsterModel>? _pendingForcedCombatFactory;
    private Func<IReadOnlyList<MonsterModel>>? _pendingForcedCombatBatchFactory;
    private Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>>? _pendingForcedCombatSlottedBatchFactory;

    public Player Owner => _owner ?? throw new InvalidOperationException("Event owner has not been assigned.");

    public IRunState RunState => _runState ?? throw new InvalidOperationException("Event has not begun.");

    public Rng Rng => _rng ?? throw new InvalidOperationException("Event has not begun.");

    public IReadOnlyList<EventOption> CurrentOptions { get; private set; } = EmptyOptions();

    public bool IsFinished { get; private set; }

    public bool IsAwaitingForcedCombat { get { lock (_stateLock) { return _isAwaitingForcedCombat; } } }

    public bool HasPendingRewardOffers
    {
        get
        {
            lock (_stateLock)
            {
                return _pendingRewardOffers.Count > 0;
            }
        }
    }

    public bool HasPendingForcedCombat
    {
        get
        {
            lock (_stateLock)
            {
                return HasPendingForcedCombatUnlocked();
            }
        }
    }

    public override bool ShouldReceiveCombatHooks => false;


    /// <summary>Whether this event can be selected for the current run. Events override this for live run-state gates.</summary>
    public virtual bool IsAllowed(IRunState runState) => true;
    /// <summary>Permanent natural-selection eligibility, independent of temporary IsAllowed gates.</summary>
    /// <remarks>偏离 #288：真实游戏的 <c>IsShared</c> 未移植。它的用途全在多人——<c>EventSynchronizer</c>
    /// 的共享推进，以及 <c>NEventLayout</c>/<c>NEventRoom</c> 的标签与选项锁定，全部由
    /// 这些用途都由多人模式判断控制。本模拟器只跑单人，所以不保留没有读取方的元数据。
    /// 事件池归属与它无关：那来自显式的 18 项 <c>ModelDb.AllSharedEvents</c>，本地镜像在
    /// <see cref="Rooms.SharedEventPool"/>。</remarks>
    public virtual bool CanAppearNaturally => true;
    public virtual EventLayoutType LayoutType => EventLayoutType.Default;
    public virtual EncounterDefinition? CanonicalEncounter => null;
    protected virtual bool LocksPotions => false;
    protected virtual bool UsesCustomInteraction => false;
    public virtual bool GenerateForcedCombatRewards => false;
    public virtual int? ForcedCombatGold => null;
    public virtual IReadOnlyList<Reward> ForcedCombatExtraRewards => [];
    public void AssignOwner(Player owner)
    {
        AssertMutable();
        ArgumentNullException.ThrowIfNull(owner);

        lock (_stateLock)
        {
            if (_hasBegun)
            {
                throw new InvalidOperationException("Event ownership cannot change after the event has begun.");
            }

            _owner = owner;
        }
    }

    public void BeginEvent(IRunState runState)
    {
        AssertMutable();
        ArgumentNullException.ThrowIfNull(runState);

        lock (_stateLock)
        {
            if (_activeChoiceTask is not null && !_activeChoiceTask.IsCompleted)
            {
                throw new InvalidOperationException("Cannot begin an event while an option choice is in progress.");
            }

            if (_owner is not null && !ReferenceEquals(_owner.RunState, runState))
            {
                throw new InvalidOperationException("Event owner belongs to a different run.");
            }

            // 偏离 #83：playerSlotIndex 恒为 0——本计划及可预见的后续计划都只做单人。
            const int playerSlotIndex = 0;
            _runState = runState;
            _rng = new Rng(runState.Rng.Seed + (ulong)playerSlotIndex + StringHelper.GetDeterministicHashCode(Id.Entry));
            _hasBegun = true;
            CurrentOptions = EmptyOptions();
            IsFinished = false;
            _activeChoiceTask = null;
            _activeChoiceOption = null;
            _pageVersion++;

            string setupKey = _owner is not null
                ? $"{_owner.CurrentSemanticLocationKey}/event={Id.Entry}/phase=setup"
                : $"floor={runState.TotalFloor}/event={Id.Entry}/phase=setup";
            using (runState.Rng.BeginSemanticScope(setupKey))
            using (_owner?.PlayerRng.BeginSemanticScope(setupKey))
            {
                CalculateVars();
                SetOptions(GenerateInitialOptionsWrapper());
            }
            if (LocksPotions && !IsFinished) Owner.CanUseOrRemovePotions = false;
        }
    }

    public Task ChooseOption(EventOption option)
    {
        AssertMutable();
        ArgumentNullException.ThrowIfNull(option);

        lock (_stateLock)
        {
            if (_isAwaitingForcedCombat)
            {
                throw new InvalidOperationException("Event cannot take an option while awaiting a forced combat.");
            }

            if (!_hasBegun || IsFinished || !CurrentOptions.Any(current => ReferenceEquals(current, option)))
            {
                throw new InvalidOperationException("Chosen option is not currently offered by this event.");
            }

            if (option.IsLocked)
            {
                throw new InvalidOperationException("Locked event options cannot be chosen.");
            }

            if (_activeChoiceTask is not null)
            {
                if (ReferenceEquals(_activeChoiceOption, option) && _activeChoicePageVersion == _pageVersion)
                {
                    return _activeChoiceTask;
                }

                throw new InvalidOperationException("An event option choice is already in progress.");
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task choiceTask = completion.Task;
            _activeChoiceTask = choiceTask;
            _activeChoiceOption = option;
            _activeChoicePageVersion = _pageVersion;
            _ = InvokeChoiceAsync(option, completion);
            return choiceTask;
        }
    }

    /// <summary>Calculates event-local values before the initial page is generated.</summary>
    protected virtual void CalculateVars()
    {
    }

    protected abstract IReadOnlyList<EventOption> GenerateInitialOptions();

    protected virtual IReadOnlyList<EventOption> GenerateInitialOptionsWrapper() => GenerateInitialOptions().ToList();

    protected void SetOptions(IReadOnlyList<EventOption> options)
    {
        AssertMutable();
        ArgumentNullException.ThrowIfNull(options);

        lock (_stateLock)
        {
            if (!_hasBegun)
            {
                throw new InvalidOperationException("Event options cannot be set before the event has begun.");
            }

            CurrentOptions = Array.AsReadOnly(options.ToArray());
            IsFinished = CurrentOptions.Count == 0 && !UsesCustomInteraction;
            _pageVersion++;
        }
    }

    protected void Finish()
    {
        SetOptions(EmptyOptions());
        IsFinished = true;
        EnsureCleanup();
    }

    internal void EnsureCleanup()
    {
        if (LocksPotions && _owner is not null) _owner.CanUseOrRemovePotions = true;
    }

    protected void SuspendForForcedCombat()
    {
        AssertMutable();
        lock (_stateLock)
        {
            if (!HasPendingForcedCombatUnlocked())
                throw new InvalidOperationException("SuspendForForcedCombat requires a queued forced combat.");
            CurrentOptions = EmptyOptions();
            IsFinished = false;
            _isAwaitingForcedCombat = true;
            _pageVersion++;
        }
    }

    public void ResumeAfterForcedCombat(ForcedCombatOutcome outcome)
    {
        AssertMutable();
        lock (_stateLock)
        {
            if (!_isAwaitingForcedCombat) throw new InvalidOperationException("Event is not awaiting a forced combat.");
            _isAwaitingForcedCombat = false;
        }
        string continuationKey = _owner is not null
            ? $"{_owner.CurrentSemanticLocationKey}/event={Id.Entry}/page={_pageVersion}/after-forced-combat"
            : $"floor={RunState.TotalFloor}/event={Id.Entry}/page={_pageVersion}/after-forced-combat";
        using IDisposable runRngScope = RunState.Rng.BeginSemanticScope(continuationKey);
        using IDisposable? playerRngScope = _owner?.PlayerRng.BeginSemanticScope(continuationKey);
        AfterForcedCombat(outcome);
    }

    protected virtual void AfterForcedCombat(ForcedCombatOutcome outcome) => Finish();

    protected void OfferRewards(RewardsSet rewards)
    {
        AssertMutable();
        ArgumentNullException.ThrowIfNull(rewards);

        lock (_stateLock)
        {
            _pendingRewardOffers.Enqueue(rewards);
        }
    }

    protected void RequestForcedCombat(Func<MonsterModel> monsterFactory)
    {
        AssertMutable();
        ArgumentNullException.ThrowIfNull(monsterFactory);

        lock (_stateLock)
        {
            if (HasPendingForcedCombatUnlocked())
            {
                throw new InvalidOperationException("A forced combat is already pending.");
            }

            _pendingForcedCombatFactory = monsterFactory;
        }
    }

    protected void RequestForcedCombatBatch(Func<IReadOnlyList<MonsterModel>> monsterBatchFactory)
    {
        AssertMutable();
        ArgumentNullException.ThrowIfNull(monsterBatchFactory);

        lock (_stateLock)
        {
            if (HasPendingForcedCombatUnlocked())
            {
                throw new InvalidOperationException("A forced combat is already pending.");
            }

            _pendingForcedCombatBatchFactory = monsterBatchFactory;
        }
    }

    /// <summary>Requests an event combat whose monsters occupy named encounter slots, as native
    /// event encounters do through <c>EncounterModel.GenerateMonsters</c>.</summary>
    protected void RequestForcedCombatSlottedBatch(
        Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>> slottedMonsterBatchFactory)
    {
        AssertMutable();
        ArgumentNullException.ThrowIfNull(slottedMonsterBatchFactory);

        lock (_stateLock)
        {
            if (HasPendingForcedCombatUnlocked())
            {
                throw new InvalidOperationException("A forced combat is already pending.");
            }

            _pendingForcedCombatSlottedBatchFactory = slottedMonsterBatchFactory;
        }
    }

    internal bool TryDequeuePendingForcedCombatSlottedBatch(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>>? factory)
    {
        AssertMutable();
        lock (_stateLock)
        {
            factory = _pendingForcedCombatSlottedBatchFactory;
            _pendingForcedCombatSlottedBatchFactory = null;
            return factory is not null;
        }
    }

    public Func<MonsterModel> DequeuePendingForcedCombat()
    {
        AssertMutable();
        lock (_stateLock)
        {
            Func<MonsterModel> factory = _pendingForcedCombatFactory
                ?? throw new InvalidOperationException("No forced combat is pending.");
            _pendingForcedCombatFactory = null;
            return factory;
        }
    }

    public Func<IReadOnlyList<MonsterModel>> DequeuePendingForcedCombatBatch()
    {
        AssertMutable();
        lock (_stateLock)
        {
            if (_pendingForcedCombatBatchFactory is not null)
            {
                Func<IReadOnlyList<MonsterModel>> batchFactory = _pendingForcedCombatBatchFactory;
                _pendingForcedCombatBatchFactory = null;
        _pendingForcedCombatSlottedBatchFactory = null;
                return batchFactory;
            }

            Func<MonsterModel> singleFactory = _pendingForcedCombatFactory
                ?? throw new InvalidOperationException("No forced combat is pending.");
            _pendingForcedCombatFactory = null;
            return () => new[] { singleFactory() };
        }
    }

    internal bool TryOfferRewardsFromCurrentEvent(IRunState runState, RewardsSet rewards)
    {
        AssertMutable();
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(rewards);

        if (runState.CurrentRoom is not EventRoom room || !ReferenceEquals(room.Event, this))
        {
            return false;
        }

        OfferRewards(rewards);
        return true;
    }

    // Keep already queued parent continuations separate from offers produced by the
    // reward currently being resolved. Drivers drain the child FIFO before resuming
    // the parent, matching native nested RewardsSet.Offer awaits.
    internal IDisposable BeginNestedRewardOffers()
    {
        AssertMutable();
        lock (_stateLock)
        {
            _suspendedRewardOffers.Push(_pendingRewardOffers);
            _pendingRewardOffers = new Queue<RewardsSet>();
            return new NestedRewardOfferScope(this);
        }
    }

    // A fork resumes a cloned data frame, not the source using/async state machine.
    internal IDisposable ResumeNestedRewardOffers() => new NestedRewardOfferScope(this);

    private void EndNestedRewardOffers()
    {
        lock (_stateLock)
        {
            Queue<RewardsSet> parent = _suspendedRewardOffers.Pop();
            // Preserve any undrained child offers if reward resolution throws.
            if (_pendingRewardOffers.Count == 0)
                _pendingRewardOffers = parent;
            else
                foreach (RewardsSet offer in parent)
                    _pendingRewardOffers.Enqueue(offer);
        }
    }

    private sealed class NestedRewardOfferScope(EventModel owner) : IDisposable
    {
        public void Dispose() => owner.EndNestedRewardOffers();
    }

    internal bool TryDequeuePendingRewardOffer([NotNullWhen(true)] out RewardsSet? rewards)
    {
        AssertMutable();
        lock (_stateLock)
        {
            return _pendingRewardOffers.TryDequeue(out rewards);
        }
    }

    internal void AssertRunCloneBoundary()
    {
        lock (_stateLock)
        {
            if (_activeChoiceTask is { IsCompleted: false })
                throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                    $"Event {Id} still has an executing option callback.");
        }
    }

    internal void RestoreRunCloneStateFrom(
        EventModel source, Player owner, IRunState runState, Rng rng,
        Func<RewardsSet, RewardsSet> mapOffer,
        Func<Delegate, Delegate> mapCallback)
    {
        source.AssertRunCloneBoundary();
        // Preserve logical progress; never run BeginEvent/CalculateVars/options again.
        // Callback descriptors are rebound only after all mutable roots are mapped.
        _stateLock = new object();
        _owner = owner;
        _runState = runState;
        _rng = rng;
        _activeChoiceTask = null;
        _activeChoiceOption = null;
        _activeChoicePageVersion = 0;
        _pageVersion = source._pageVersion;
        _hasBegun = source._hasBegun;
        _isAwaitingForcedCombat = source._isAwaitingForcedCombat;
        IsFinished = source.IsFinished;
        CurrentOptions = Array.AsReadOnly(source.CurrentOptions
            .Select(option => option.RebindForRun(mapCallback)).ToArray());
        _pendingRewardOffers = new Queue<RewardsSet>(source._pendingRewardOffers.Select(mapOffer));
        // Stack enumeration is top-first. Reverse before constructing the clone stack.
        _suspendedRewardOffers = new Stack<Queue<RewardsSet>>(source._suspendedRewardOffers
            .Reverse().Select(queue => new Queue<RewardsSet>(queue.Select(mapOffer))));
        _pendingForcedCombatFactory = source._pendingForcedCombatFactory is { } factory
            ? (Func<MonsterModel>)mapCallback(factory) : null;
        _pendingForcedCombatBatchFactory = source._pendingForcedCombatBatchFactory is { } batch
            ? (Func<IReadOnlyList<MonsterModel>>)mapCallback(batch) : null;
        _pendingForcedCombatSlottedBatchFactory = source._pendingForcedCombatSlottedBatchFactory is { } slotted
            ? (Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>>)mapCallback(slotted) : null;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _stateLock = new object();
        _owner = null;
        _runState = null;
        _rng = null;
        _activeChoiceTask = null;
        _activeChoiceOption = null;
        _activeChoicePageVersion = 0;
        _pageVersion = 0;
        _hasBegun = false;
        _isAwaitingForcedCombat = false;
        _pendingRewardOffers = new Queue<RewardsSet>();
        _suspendedRewardOffers = new Stack<Queue<RewardsSet>>();
        _pendingForcedCombatFactory = null;
        _pendingForcedCombatBatchFactory = null;
        CurrentOptions = EmptyOptions();
        IsFinished = false;
    }

    private bool HasPendingForcedCombatUnlocked() =>
        _pendingForcedCombatFactory is not null || _pendingForcedCombatBatchFactory is not null ||
        _pendingForcedCombatSlottedBatchFactory is not null;

    private async Task InvokeChoiceAsync(EventOption option, TaskCompletionSource completion)
    {
        Exception? failure = null;
        OperationCanceledException? cancellation = null;
        try
        {
            string choiceKey = _owner is not null
                ? $"{_owner.CurrentSemanticLocationKey}/event={Id.Entry}" +
                  $"/page={_activeChoicePageVersion}/option={option.Key}"
                : $"floor={RunState.TotalFloor}/event={Id.Entry}" +
                  $"/page={_activeChoicePageVersion}/option={option.Key}";
            using IDisposable runRngScope = RunState.Rng.BeginSemanticScope(choiceKey);
            using IDisposable? playerRngScope = _owner?.PlayerRng.BeginSemanticScope(choiceKey);
            await option.Invoke();
        }
        catch (OperationCanceledException exception)
        {
            cancellation = exception;
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        lock (_stateLock)
        {
            if (ReferenceEquals(_activeChoiceTask, completion.Task) &&
                (failure is not null || cancellation is not null || IsFinished || _activeChoicePageVersion != _pageVersion))
            {
                _activeChoiceTask = null;
                _activeChoiceOption = null;
            }
        }

        if (cancellation is not null)
        {
            completion.TrySetCanceled(cancellation.CancellationToken);
        }
        else if (failure is not null)
        {
            completion.TrySetException(failure);
        }
        else
        {
            completion.TrySetResult();
        }
    }

    private static IReadOnlyList<EventOption> EmptyOptions() => new List<EventOption>().AsReadOnly();
}
