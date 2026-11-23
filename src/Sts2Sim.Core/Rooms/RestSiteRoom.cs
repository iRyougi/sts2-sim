using System.Diagnostics.CodeAnalysis;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rooms;

/// <summary>
/// 休息点。可用选项由 <see cref="GetAvailableDecisions"/> 生成一次稳定快照；
/// RunEngine、RunDriver 和 RL 都必须消费该快照，不能自行重建选项。
/// </summary>
public sealed class RestSiteRoom : AbstractRoom
{
    private readonly object _rewardLock = new();
    private readonly Queue<RewardsSet> _pendingRewardOffers = new();
    private readonly Dictionary<Player, List<RestSiteDecision>> _remainingDecisions = new();
    // Metadata-only counterpart of native RestSiteOption.Options; never used to choose an action.
    private readonly Dictionary<Player, List<string>> _displayedOptionIds = new();

    public override RoomType RoomType => RoomType.RestSite;

    public override ModelId? ModelId => null;

    public override Task EnterInternal(RunState? runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        return Task.CompletedTask;
    }

    public bool IsSmithEnabled(Player player) => player.Deck.Cards.Any(card => card.IsUpgradable);

    public IReadOnlyList<CardModel> GetUpgradableCards(Player player) =>
        player.Deck.Cards.Where(card => card.IsUpgradable).ToList();

    public IReadOnlyList<RestSiteDecision> GetAvailableDecisions(IRunState runState, Player player)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(player);
        if (_remainingDecisions.TryGetValue(player, out List<RestSiteDecision>? existing)) return existing.ToArray();
        var decisions = new List<RestSiteDecision> { new RestSiteDecision.Heal() };
        foreach (CardModel card in GetUpgradableCards(player)) decisions.Add(new RestSiteDecision.Smith(card));
        Hooks.Hook.ModifyAvailableRestSiteDecisions(runState, player, decisions);
        _remainingDecisions[player] = decisions;
        // Native RestSiteOption.Generate always adds Smith, even with no eligible target.
        // Native MeatCleaver adds Cook regardless of its current removable-card threshold.
        // Capture these display-only families here, beside the one existing generation step.
        List<string> displayed = decisions.Select(decision => decision.OptionId).Distinct().ToList();
        if (!displayed.Contains("smith")) displayed.Insert(Math.Min(1, displayed.Count), "smith");
        if (player.Relics.Any(relic => relic is Models.Relics.MeatCleaver) && !displayed.Contains("cook"))
            displayed.Add("cook");
        _displayedOptionIds[player] = displayed;
        return decisions.ToArray();
    }

    /// <summary>Returns the already-captured display lifecycle, without generating decisions or executing hooks.</summary>
    public IReadOnlyList<string> GetDisplayedOptionIds(Player player) =>
        _displayedOptionIds.TryGetValue(player, out List<string>? displayed) ? displayed.ToArray() : [];

    public async Task ResolveAsync(Player player, RestSiteDecision decision)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(decision);

        if (!decision.IsEnabled)
        {
            throw new InvalidOperationException("Rest-site option is disabled.");
        }

        await decision.ExecuteAsync(player);
        if (_remainingDecisions.TryGetValue(player, out List<RestSiteDecision>? remaining))
        {
            if (Hooks.Hook.ShouldDisableRemainingRestSiteOptions(player.RunState, player))
            {
                remaining.Clear();
                if (_displayedOptionIds.TryGetValue(player, out List<string>? displayed)) displayed.Clear();
            }
            else
            {
                if (_displayedOptionIds.TryGetValue(player, out List<string>? displayed))
                    displayed.Remove(decision.OptionId);
                if (decision is RestSiteDecision.Smith)
                {
                    // Smith is flattened to one record per equivalent card target for the
                    // static RL action space, but upstream exposes it as one option per visit.
                    remaining.RemoveAll(candidate => candidate is RestSiteDecision.Smith);
                }
                else
                {
                    remaining.Remove(decision);
                }

                // Other successful options can mutate the persistent deck (notably Cook).
                // Never leave a stable snapshot pointing at a removed or exhausted target.
                remaining.RemoveAll(candidate =>
                    candidate is RestSiteDecision.Smith smith &&
                    (!smith.Card.IsUpgradable ||
                     !player.Deck.Cards.Any(card => ReferenceEquals(card, smith.Card))));
            }
        }
    }

    public bool HasRemainingDecisions(Player player) =>
        _remainingDecisions.TryGetValue(player, out List<RestSiteDecision>? remaining) && remaining.Count > 0;

    internal bool TryOfferRewardsFromCurrentRestSite(IRunState runState, RewardsSet rewards)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(rewards);
        if (!ReferenceEquals(runState.CurrentRoom, this)) return false;
        lock (_rewardLock)
        {
            _pendingRewardOffers.Enqueue(rewards);
        }
        return true;
    }

    internal bool TryDequeuePendingRewardOffer([NotNullWhen(true)] out RewardsSet? rewards)
    {
        lock (_rewardLock)
        {
            return _pendingRewardOffers.TryDequeue(out rewards);
        }
    }

    internal RestSiteRoom CloneForRun(
        Func<Player, Player> mapPlayer,
        Func<RestSiteDecision, RestSiteDecision> mapDecision,
        Func<RewardsSet, RewardsSet> mapOffer,
        Action<AbstractRoom, AbstractRoom> registerRoom)
    {
        var clone = new RestSiteRoom();
        registerRoom(this, clone);
        clone.CopyEntryStateFrom(this);
        foreach ((Player player, List<string> displayed) in _displayedOptionIds)
            clone._displayedOptionIds.Add(mapPlayer(player), displayed.ToList());
        foreach ((Player player, List<RestSiteDecision> remaining) in _remainingDecisions)
            clone._remainingDecisions.Add(mapPlayer(player), remaining.Select(mapDecision).ToList());
        lock (_rewardLock)
            foreach (RewardsSet offer in _pendingRewardOffers)
                clone._pendingRewardOffers.Enqueue(mapOffer(offer));
        return clone;
    }

    public override Task Exit(RunState? runState)
    {
        _remainingDecisions.Clear();
        _displayedOptionIds.Clear();
        return Task.CompletedTask;
    }
}
