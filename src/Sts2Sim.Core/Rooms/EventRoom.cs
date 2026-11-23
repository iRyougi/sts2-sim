using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rooms;

/// <summary>Hosts a mutable event state machine for the current run player.</summary>
public sealed class EventRoom : AbstractRoom
{
    private readonly Func<EventModel> _eventFactory;
    private CombatRoom? _preparedCombatRoom;

    public override RoomType RoomType => RoomType.Event;

    public override ModelId? ModelId => null;

    public EventModel Event { get; private set; } = null!;

    public EventRoom(Func<EventModel> eventFactory)
    {
        _eventFactory = eventFactory ?? throw new ArgumentNullException(nameof(eventFactory));
    }

    internal EventRoom CloneForRun(RunState targetRun, bool reseeded,
        Func<EventModel, EventModel> mapEvent,
        Func<CombatRoom, CombatRoom> mapCombatRoom,
        Func<Delegate, Delegate> mapCallback,
        Action<object, object> registerRoot) =>
        new(this, targetRun, reseeded, mapEvent, mapCombatRoom, mapCallback, registerRoot);

    private EventRoom(EventRoom source, RunState targetRun, bool reseeded,
        Func<EventModel, EventModel> mapEvent,
        Func<CombatRoom, CombatRoom> mapCombatRoom,
        Func<Delegate, Delegate> mapCallback,
        Action<object, object> registerRoot)
    {
        source.Event?.AssertRunCloneBoundary();
        registerRoot(source, this);
        CopyEntryStateFrom(source);
        Event = source.Event is null ? null! : mapEvent(source.Event);
        _eventFactory = (Func<EventModel>)mapCallback(source._eventFactory);
        if (source._preparedCombatRoom is null) return;
        if (!reseeded)
        {
            _preparedCombatRoom = mapCombatRoom(source._preparedCombatRoom);
            return;
        }

        source._preparedCombatRoom.AssertRunCloneBoundary();
        if (source._preparedCombatRoom.Engine is not null)
            throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                "An event's concealed prepared room must not have entered combat.");
        EncounterDefinition encounter = Event.CanonicalEncounter
            ?? throw new InvalidOperationException("A prepared event has no public canonical encounter.");
        var fresh = new CombatRoom(
            () => (encounter, encounter.CreateMonsters(new Rng(
                RoomFactory.EncounterMonsterSeed(targetRun.Rng.Seed, targetRun.TotalFloor, encounter)))),
            RoomType.Monster)
        {
            FixedGoldAmount = Event.ForcedCombatGold,
        };
        fresh.CopyEntryStateFrom(source._preparedCombatRoom);
        fresh.Prepare(targetRun);
        _preparedCombatRoom = fresh;
        registerRoot(source._preparedCombatRoom, fresh);
        // Only state-root identity is paired. Old monsters, their catalogue and prerolls
        // never enter the clone queue or drive the native new-seed generation.
        using var oldStates = source._preparedCombatRoom.EnumerateRunCloneStates().GetEnumerator();
        using var newStates = fresh.EnumerateRunCloneStates().GetEnumerator();
        while (oldStates.MoveNext())
        {
            if (!newStates.MoveNext())
                throw new InvalidOperationException("Prepared combat state roots do not match.");
            registerRoot(oldStates.Current, newStates.Current);
        }
        if (newStates.MoveNext())
            throw new InvalidOperationException("Prepared combat state roots do not match.");
    }

    public override Task EnterInternal(RunState? runState)
    {
        ArgumentNullException.ThrowIfNull(runState);

        Event = _eventFactory();
        ArgumentNullException.ThrowIfNull(Event);
        Player player = runState.Players[0];
        Event.AssignOwner(player);
        Event.BeginEvent(runState);

        if (Event.LayoutType == EventLayoutType.Combat)
        {
            EncounterDefinition encounter = Event.CanonicalEncounter
                ?? throw new InvalidOperationException(
                    $"Combat-layout event {Event.GetType().Name} has no canonical encounter.");
            // 原版 EventCombatSynchronizer 用 GenerateMonstersWithSlots 生成布局怪物，走遭遇自己的派生 Rng，
            // 与地图上的普通遭遇同一公式；PunchOffEventEncounter 在这里抽 StartingHpReduction。
            _preparedCombatRoom = new CombatRoom(
                () => (encounter, encounter.CreateMonsters(new Rng(
                    RoomFactory.EncounterMonsterSeed(runState.Rng.Seed, runState.TotalFloor, encounter)))),
                RoomType.Monster)
            {
                FixedGoldAmount = Event.ForcedCombatGold,
            };
            _preparedCombatRoom.Prepare(runState);
        }
        return Task.CompletedTask;
    }

    internal CombatRoom CreatePendingForcedCombatRoom()
    {
        if (Event.TryDequeuePendingForcedCombatSlottedBatch(out var slottedBatchFactory))
        {
            if (_preparedCombatRoom is not null)
            {
                throw new InvalidOperationException(
                    "A combat-layout event cannot also request a slotted forced combat.");
            }

            return new CombatRoom(
                slottedBatchFactory,
                RoomType.Monster,
                CombatRoom.ForcedEncounterName)
            {
                FixedGoldAmount = Event.ForcedCombatGold,
            };
        }

        Func<IReadOnlyList<MonsterModel>> monsterBatchFactory = Event.DequeuePendingForcedCombatBatch();
        if (_preparedCombatRoom is not null)
        {
            CombatRoom prepared = _preparedCombatRoom;
            _preparedCombatRoom = null;
            return prepared;
        }

        return new CombatRoom(
            monsterBatchFactory,
            RoomType.Monster,
            CombatRoom.ForcedEncounterName)
        {
            FixedGoldAmount = Event.ForcedCombatGold,
        };
    }

    public override Task Exit(RunState? runState)
    {
        // Native EventSynchronizer.BeforeExitingRoom resets the combat layout even when
        // the event chose its non-combat option. A consumed forced room cleans itself up.
        if (_preparedCombatRoom is { } prepared)
        {
            foreach (var state in prepared.EnumerateRunCloneStates())
                foreach (var creature in state.Creatures.ToArray())
                    state.RemoveCreature(creature);
            _preparedCombatRoom = null;
        }
        Event?.EnsureCleanup();
        return Task.CompletedTask;
    }
}
