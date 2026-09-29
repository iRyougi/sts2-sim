using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

file sealed class RestDecisionAppendingRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.None;

    public RestSiteDecision Decision { get; set; } = null!;

    public List<string>? InvocationOrder { get; set; }

    public List<RestSiteDecision>? CapturedDecisions { get; private set; }

    public string Label { get; set; } = string.Empty;

    public override void ModifyAvailableRestSiteDecisions(
        IRunState runState,
        Player player,
        List<RestSiteDecision> decisions)
    {
        CapturedDecisions = decisions;
        InvocationOrder?.Add(Label);
        decisions.Add(Decision);
    }
}

[Collection("ModelDb")]
public sealed class RestSiteExtensibilityTests : IDisposable
{
    public RestSiteExtensibilityTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight), typeof(RestDecisionAppendingRelic),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void GetAvailableDecisions_WithoutListeners_ReturnsHealAndEveryUpgradableSmith()
    {
        (RunState runState, Player player) = CreatePlayer("rest-baseline");
        CardModel[] upgradable = player.Deck.Cards.Where(card => card.IsUpgradable).ToArray();

        IReadOnlyList<RestSiteDecision> decisions = new RestSiteRoom().GetAvailableDecisions(runState, player);

        Assert.IsType<RestSiteDecision.Heal>(decisions[0]);
        Assert.Equal(
            upgradable,
            decisions.Skip(1).Select(decision => Assert.IsType<RestSiteDecision.Smith>(decision).Card));
    }

    [Fact]
    public void GetAvailableDecisions_WithoutUpgradableCards_OmitsSmith()
    {
        (RunState runState, Player player) = CreatePlayer("rest-no-smith");
        foreach (CardModel card in player.Deck.Cards.Where(card => card.IsUpgradable))
        {
            card.Upgrade();
        }

        IReadOnlyList<RestSiteDecision> decisions = new RestSiteRoom().GetAvailableDecisions(runState, player);

        Assert.Collection(decisions, decision => Assert.IsType<RestSiteDecision.Heal>(decision));
    }

    [Fact]
    public void GetAvailableDecisions_ListenerAppendsDecisionAfterBaseline()
    {
        (RunState runState, Player player) = CreatePlayer("rest-single-listener");
        var expected = new RestSiteDecision.Hatch();
        AddListener(player, "single", expected, invocationOrder: null);

        IReadOnlyList<RestSiteDecision> decisions = new RestSiteRoom().GetAvailableDecisions(runState, player);

        Assert.Same(expected, decisions[^1]);
        Assert.IsType<RestSiteDecision.Heal>(decisions[0]);
        Assert.IsType<RestSiteDecision.Smith>(decisions[1]);
    }

    [Fact]
    public void GetAvailableDecisions_MultipleListenersRunAndAppendInInsertionOrder()
    {
        (RunState runState, Player player) = CreatePlayer("rest-multiple-listeners");
        var invocationOrder = new List<string>();
        var firstDecision = new RestSiteDecision.Hatch();
        var secondDecision = new RestSiteDecision.Hatch();
        AddListener(player, "first", firstDecision, invocationOrder);
        AddListener(player, "second", secondDecision, invocationOrder);

        IReadOnlyList<RestSiteDecision> decisions = new RestSiteRoom().GetAvailableDecisions(runState, player);

        Assert.Equal(new[] { "first", "second" }, invocationOrder);
        Assert.Same(firstDecision, decisions[^2]);
        Assert.Same(secondDecision, decisions[^1]);
    }

    [Fact]
    public void GetAvailableDecisions_ReturnsFreshExternallyReadOnlySnapshots()
    {
        (RunState runState, Player player) = CreatePlayer("rest-read-only");
        var room = new RestSiteRoom();

        IReadOnlyList<RestSiteDecision> first = room.GetAvailableDecisions(runState, player);
        IReadOnlyList<RestSiteDecision> second = room.GetAvailableDecisions(runState, player);

        Assert.NotSame(first, second);
        IList<RestSiteDecision> exposed = Assert.IsAssignableFrom<IList<RestSiteDecision>>(first);
        Assert.Throws<NotSupportedException>(() => exposed.Add(new RestSiteDecision.Hatch()));
        Assert.Equal(1 + player.Deck.Cards.Count(card => card.IsUpgradable), second.Count);
    }

    [Fact]
    public void GetAvailableDecisions_ReturnedSnapshotCannotBeChangedThroughListenerRetainedList()
    {
        (RunState runState, Player player) = CreatePlayer("rest-listener-retained-list");
        var listener = (RestDecisionAppendingRelic)ModelDb.Relic<RestDecisionAppendingRelic>().MutableClone();
        listener.Label = "retains-list";
        listener.Decision = new RestSiteDecision.Hatch();
        listener.AssignOwner(player);
        player.AddRelicInternal(listener);

        IReadOnlyList<RestSiteDecision> decisions = new RestSiteRoom().GetAvailableDecisions(runState, player);
        listener.CapturedDecisions!.Clear();

        Assert.IsType<RestSiteDecision.Heal>(decisions[0]);
        Assert.All(
            decisions.Skip(1).Take(decisions.Count - 2),
            decision => Assert.IsType<RestSiteDecision.Smith>(decision));
        Assert.IsType<RestSiteDecision.Hatch>(decisions[^1]);
    }

    [Fact]
    public void Hatch_IsConstructibleAndUsesStableRecordEquality()
    {
        var first = new RestSiteDecision.Hatch();
        var second = new RestSiteDecision.Hatch();

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    private static (RunState RunState, Player Player) CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static void AddListener(
        Player player,
        string label,
        RestSiteDecision decision,
        List<string>? invocationOrder)
    {
        var relic = (RestDecisionAppendingRelic)ModelDb.Relic<RestDecisionAppendingRelic>().MutableClone();
        relic.Label = label;
        relic.Decision = decision;
        relic.InvocationOrder = invocationOrder;
        relic.AssignOwner(player);
        player.AddRelicInternal(relic);
    }
}
