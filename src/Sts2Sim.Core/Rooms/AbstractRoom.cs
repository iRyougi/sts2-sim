using Sts2Sim.Core.Models;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rooms;

/// <summary>
/// Base room abstraction, structurally ported from <c>MegaCrit.Sts2.Core.Rooms.AbstractRoom</c>.
/// Deviation #43: omits <c>IsPreFinished</c>, <c>IsVictoryRoom</c>, <c>Resume</c>, and save
/// serialization because the save system plus Ancient/Event content are not ported yet. <c>Enter</c>
/// assigns the room id and dispatches room-specific entry hook ordering.
/// </summary>
public abstract class AbstractRoom
{
    private bool _hasEntered;

    public abstract RoomType RoomType { get; }

    public abstract ModelId? ModelId { get; }

    public int? Id { get; private set; }

    // The fork has already entered this room. Copy only entry identity/state;
    // re-entering would consume a room id, dispatch hooks, and recount an Ancient.
    internal void CopyEntryStateFrom(AbstractRoom source)
    {
        _hasEntered = source._hasEntered;
        Id = source.Id;
    }

    protected virtual bool EntryHooksBeforeRewards => false;

    public async Task Enter(RunState? runState)
    {
        Id = runState?.GetAndIncrementNextRoomId();
        if (runState != null)
        {
            await Hook.BeforeRoomEntered(runState, this);
        }
        await EnterWithEntryHooksAsync(runState);
        // Record successful entries before the room is later popped; nested shops count as well.
        if (runState is not null && RoomType == RoomType.Shop &&
            ReferenceEquals(runState.CurrentRoom, this))
        {
            runState.RecordShopRoomEntry();
        }
        if (!_hasEntered)
        {
            _hasEntered = true;
            // RunManager marks a fresh top-level Ancient as an Event visit after entry hooks.
            // Ordinary event selection already advances its cursor in RunState.AcceptEvent.
            if (runState is not null && runState.CurrentRoomCount == 1 &&
                ReferenceEquals(runState.CurrentRoom, this) &&
                this is EventRoom { Event: AncientEventModel ancient })
            {
                runState.RecordAncientRoomVisit(ancient.GetType());
            }
        }
    }

    // Combat rooms dispatch entry hooks after setup but before starting their first turn.
    protected virtual async Task EnterWithEntryHooksAsync(RunState? runState)
    {
        if (runState != null && EntryHooksBeforeRewards)
            await Hook.AfterRoomEntered(runState, this);
        await EnterInternal(runState);
        if (runState != null && !EntryHooksBeforeRewards)
            await Hook.AfterRoomEntered(runState, this);
    }

    public abstract Task EnterInternal(RunState? runState);

    public abstract Task Exit(RunState? runState);
}
