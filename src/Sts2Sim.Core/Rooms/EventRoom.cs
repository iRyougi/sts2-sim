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
        Event?.EnsureCleanup();
        return Task.CompletedTask;
    }
}
