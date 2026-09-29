using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Entities.Creatures;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace Sts2Sim.Core.Tests.Runs;

public sealed class CombinedRunDecisionSourceTests
{
    [Theory]
    [InlineData("map", false)]
    [InlineData("combat", false)]
    [InlineData("cards", false)]
    [InlineData("cards", true)]
    [InlineData("reward", false)]
    [InlineData("shop", false)]
    [InlineData("custom-event", false)]
    [InlineData("rest-site", false)]
    [InlineData("event-option", false)]
    public async Task Routes_each_decision_to_the_expected_source(string decision, bool projectedCombat)
    {
        var combat = new RecordingDecisionSource("combat");
        var strategy = new RecordingDecisionSource("strategy");
        var combined = new CombinedRunDecisionSource(combat, strategy);
        CardSelectionRequest cardRequest = new(
            CreatePlayer(projectedCombat),
            Array.Empty<CardModel>(),
            0,
            0,
            null);

        await Invoke(combined, decision, cardRequest);

        string expected = decision == "combat" || decision == "cards" && !projectedCombat
            ? "combat"
            : "strategy";
        Assert.Equal(expected, Assert.Single(combat.Calls.Concat(strategy.Calls)));
        Assert.Equal(expected == "combat", combat.Calls.Count == 1);
        Assert.Equal(expected == "strategy", strategy.Calls.Count == 1);
    }

    private static async Task Invoke(
        IRunDecisionSource source,
        string decision,
        CardSelectionRequest cardRequest)
    {
        switch (decision)
        {
            case "map": await source.ChooseMapPointAsync(null!); break;
            case "combat": await source.ChooseCombatActionAsync(null!); break;
            case "cards": await source.ChooseCardsAsync(cardRequest); break;
            case "reward": await source.ChooseRewardActionAsync(null!); break;
            case "shop": await source.ChooseShopActionAsync(null!, null!); break;
            case "custom-event": await source.ChooseCustomEventActionAsync(null!); break;
            case "rest-site": await source.ChooseRestSiteActionAsync(null!, null!); break;
            case "event-option": await source.ChooseEventOptionAsync(null!); break;
            default: throw new ArgumentOutOfRangeException(nameof(decision));
        }
    }

    private static Player CreatePlayer(bool projectedCombat)
    {
        var runState = new RunState("combined-decision-source", new global::Sts2Sim.Core.Content.Acts.Overgrowth());
        Player player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var creature = Creature.CreateStandaloneForTests(10, 10);
        typeof(Player).GetField("<Creature>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, creature);
        creature.CombatState = projectedCombat
            ? new CombatState(runState).Clone()
            : new CombatState(runState);
        return player;
    }
    private sealed class RecordingDecisionSource(string name) : IRunDecisionSource
    {
        public List<string> Calls { get; } = new();

        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => Record<MapPoint>("map");
        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) => Record<CombatDecision>("combat");
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) => Record<IReadOnlyList<CardModel>>("cards");
        public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) => Record<RewardDecision>("reward");
        public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) => Record<ShopDecision>("shop");
        public Task<CustomEventDecision> ChooseCustomEventActionAsync(EventModel @event) => Record<CustomEventDecision>("custom-event");
        public Task<RestSiteDecision> ChooseRestSiteActionAsync(Player player, IReadOnlyList<RestSiteDecision> candidates) => Record<RestSiteDecision>("rest-site");
        public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) => Record<EventOption>("event-option");

        private Task<T> Record<T>(string call)
        {
            Calls.Add(name == "combat" ? "combat" : "strategy");
            return Task.FromResult<T>(default!);
        }
    }
}




