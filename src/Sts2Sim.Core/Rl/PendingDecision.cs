using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rl;

/// <summary>
/// A decision currently waiting for an external action. It carries the state required for
/// observation encoding and action decoding.
/// </summary>
public abstract record PendingDecision
{
    public sealed record MapChoice(RunState RunState, IReadOnlyList<MapPoint> Options) : PendingDecision;

    public sealed record Combat(RunState RunState, CombatState State) : PendingDecision;

    public sealed record Reward(RunState RunState, RewardsSet Rewards) : PendingDecision;

    public sealed record Shop(RunState RunState, MerchantInventory Inventory, Player Player) : PendingDecision;

    public sealed record RestSite(
        RunState RunState,
        Player Player,
        IReadOnlyList<RestSiteDecision> Options) : PendingDecision;

    public sealed record Event(RunState RunState, IReadOnlyList<EventOption> Options) : PendingDecision;

    public sealed record CustomEvent(RunState RunState, Models.EventModel EventModel) : PendingDecision;

    public sealed record CardSelection(
        RunState RunState,
        CardSelectionRequest Request,
        IReadOnlyList<Models.CardModel> Candidates,
        int SelectedCount) : PendingDecision;
}
