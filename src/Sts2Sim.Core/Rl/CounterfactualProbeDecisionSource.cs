using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rl;

/// <summary>
/// Deterministic measurement-only decision source. It takes candidate zero everywhere and,
/// for a variant run, candidate one at exactly one tracked strategic decision. This is an
/// experimental control for CRN probes, not a gameplay policy or training heuristic.
/// </summary>
public sealed class CounterfactualProbeDecisionSource(
    string trackedKind,
    int trackedOrdinal,
    bool takeVariant) : IRunDecisionSource
{
    private readonly Dictionary<string, int> _seen = new();

    public bool OverrideApplied { get; private set; }

    public string ChosenSignature { get; private set; } = string.Empty;

    public event Action? OnOverrideApplied;

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        throw new InvalidOperationException(
            "Combat reached the probe strategy; CombinedRunDecisionSource routing is broken.");

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options[Pick("map", options.Count)]);

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards)
    {
        if (RewardDecisionClassifier.Classify(rewards) is not RewardDecisionClassification.Choice choice)
        {
            return Task.FromResult(RewardDecisionClassifier.ChooseDefault(rewards));
        }

        return Task.FromResult(choice.Candidates[Pick("reward", choice.Candidates.Count)]);
    }

    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player)
    {
        IReadOnlyList<ShopDecisionCandidates.Candidate> choices =
            ShopDecisionCandidates.Build(inventory, player);
        return Task.FromResult(choices[Pick("shop", choices.Count)].Decision);
    }

    public Task<RestSiteDecision> ChooseRestSiteActionAsync(
        Player player,
        IReadOnlyList<RestSiteDecision> candidates)
    {
        IReadOnlyList<RestSiteDecisionCandidates.Candidate> choices =
            RestSiteDecisionCandidates.Build(player, candidates);
        return Task.FromResult(choices[Pick("restSite", choices.Count)].Decision);
    }

    public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options)
    {
        IReadOnlyList<EventOption> enabled = options.Where(option => option.IsEnabled).ToList();
        return Task.FromResult(enabled[Pick("event", enabled.Count)]);
    }

    public Task<CustomEventDecision> ChooseCustomEventActionAsync(EventModel @event)
    {
        IReadOnlyList<(CustomEventDecision Decision, string Label)> choices =
            ObservationEncoder.BuildCustomEventCandidates(@event);
        return Task.FromResult(choices[Pick("customEvent", choices.Count)].Decision);
    }

    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
    {
        IReadOnlyList<CardModel> candidates = request.Candidates;
        int count = Math.Min(request.MinCount, candidates.Count);
        int offset = Pick("cardSelection", Math.Max(candidates.Count - count + 1, 1));
        return Task.FromResult<IReadOnlyList<CardModel>>(
            candidates.Skip(offset).Take(count).ToList());
    }

    private int Pick(string kind, int candidateCount)
    {
        int ordinal = _seen.GetValueOrDefault(kind);
        _seen[kind] = ordinal + 1;

        bool isTracked = string.Equals(kind, trackedKind, StringComparison.Ordinal) &&
            ordinal == trackedOrdinal;
        bool canSwap = isTracked && candidateCount > 1;
        int chosen = canSwap && takeVariant ? 1 : 0;
        if (canSwap && takeVariant)
        {
            OverrideApplied = true;
            OnOverrideApplied?.Invoke();
        }

        if (isTracked)
        {
            ChosenSignature = $"{kind}#{ordinal}:{chosen}/{candidateCount}";
        }

        return chosen;
    }
}
