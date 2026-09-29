using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rewards;

file sealed class LateModificationCommonCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;
}

file sealed class LateModificationReplacementCard : CardModel
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;
}

file sealed class LateReplacementRelic : RelicModel
{
    public int OptionCallbackCount { get; private set; }

    public int AfterCallbackCount { get; private set; }

    public IReadOnlyList<CardModel>? OptionsSeenAfterModification { get; private set; }

    public override RelicRarity Rarity => RelicRarity.Common;

    public override CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option)
    {
        OptionCallbackCount++;
        return ModelDb.Card<LateModificationReplacementCard>();
    }

    public override void AfterModifyingCardRewardOptions(
        IRunState runState,
        Player player,
        IReadOnlyList<CardModel> options)
    {
        AfterCallbackCount++;
        OptionsSeenAfterModification = options;
    }
}

public sealed class LateSequenceRelic : RelicModel
{
    public CardModel? Replacement { get; set; }

    public List<string>? Trace { get; set; }

    public string Label { get; set; } = string.Empty;

    public int AfterCallbackCount { get; private set; }

    public IReadOnlyList<CardModel>? OptionsSeenAfterModification { get; private set; }

    public override RelicRarity Rarity => RelicRarity.Common;

    public override CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option)
    {
        Trace?.Add($"{Label}:{option.Id}");
        return Replacement;
    }

    public override void AfterModifyingCardRewardOptions(
        IRunState runState,
        Player player,
        IReadOnlyList<CardModel> options)
    {
        AfterCallbackCount++;
        OptionsSeenAfterModification = options;

        Trace?.Add($"{Label}:after");
    }
}
[Collection("ModelDb")]
public sealed class CardRewardLateModificationTests : IDisposable
{
    public CardRewardLateModificationTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(LateModificationCommonCard), typeof(LateModificationReplacementCard),
            typeof(LateReplacementRelic), typeof(LateSequenceRelic),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Populate_AppliesLateReplacementFromOwnedRelic()
    {
        (RunState runState, Player player) = CreatePlayer("late-card-replacement");
        var relic = (LateReplacementRelic)ModelDb.Relic<LateReplacementRelic>().MutableClone();
        relic.AssignOwner(player);
        player.AddRelicInternal(relic);
        var reward = new CardReward(player, CardRarityOddsType.RegularEncounter);

        reward.Populate(runState);

        CardModel replacement = ModelDb.Card<LateModificationReplacementCard>();
        Assert.NotEmpty(reward.Options);
        Assert.Equal(reward.Options.Count, relic.OptionCallbackCount);
        Assert.Equal(1, relic.AfterCallbackCount);
        Assert.Same(reward.Options, relic.OptionsSeenAfterModification);
        Assert.All(reward.Options, option => Assert.Same(replacement, option));
    }

    [Fact]
    public void Populate_ComposesOwnedRelicsInOrder_AndUsesLastNonNullReplacement()
    {
        (RunState runState, Player player) = CreatePlayer("late-card-order");
        var trace = new List<string>();
        CardModel firstReplacement = ModelDb.Card<LateModificationCommonCard>();
        CardModel lastReplacement = ModelDb.Card<LateModificationReplacementCard>();
        LateSequenceRelic first = AttachSequenceRelic(player, "first", firstReplacement, trace);
        LateSequenceRelic none = AttachSequenceRelic(player, "none", null, trace);
        LateSequenceRelic last = AttachSequenceRelic(player, "last", lastReplacement, trace);
        first.ExecutionFinished += _ => trace.Add("first:finished");
        none.ExecutionFinished += _ => trace.Add("none:finished");
        last.ExecutionFinished += _ => trace.Add("last:finished");
        var reward = new CardReward(player, CardRarityOddsType.RegularEncounter);
        reward.Populate(runState);

        Assert.NotEmpty(reward.Options);
        string[] optionCallbacks = trace.Take(reward.Options.Count * 3).ToArray();
        for (int i = 0; i < reward.Options.Count; i++)
        {
            string originalOptionId = optionCallbacks[i * 3].Split(':')[1];
            Assert.Equal(
                new[]
                {
                    $"first:{originalOptionId}",
                    $"none:{firstReplacement.Id}",
                    $"last:{firstReplacement.Id}",
                },
                optionCallbacks.Skip(i * 3).Take(3));
        }

        Assert.Equal(new[] { "first:after", "first:finished", "last:after", "last:finished" }, trace.TakeLast(4));
        Assert.DoesNotContain("none:finished", trace);
        Assert.Equal(1, first.AfterCallbackCount);
        Assert.Equal(0, none.AfterCallbackCount);
        Assert.Equal(1, last.AfterCallbackCount);
        Assert.Same(reward.Options, first.OptionsSeenAfterModification);
        Assert.Same(reward.Options, last.OptionsSeenAfterModification);
        Assert.All(reward.Options, option => Assert.Same(lastReplacement, option));
    }

    [Fact]
    public void Populate_WithNoLateReplacement_DoesNotNotifyListeners()
    {
        (RunState runState, Player player) = CreatePlayer("late-card-no-replacement");
        var trace = new List<string>();
        LateSequenceRelic none = AttachSequenceRelic(player, "none", null, trace);
        var reward = new CardReward(player, CardRarityOddsType.RegularEncounter);

        reward.Populate(runState);

        Assert.Equal(0, none.AfterCallbackCount);
        Assert.DoesNotContain(trace, entry => entry.EndsWith(":after", StringComparison.Ordinal));
    }

    [Fact]
    public void Populate_RebuildsImmutableOptions_AndNotifiesOncePerCall()
    {
        (RunState runState, Player player) = CreatePlayer("late-card-repeat");
        var passiveRelic = (DivineRight)ModelDb.Relic<DivineRight>().MutableClone();
        passiveRelic.AssignOwner(player);
        player.AddRelicInternal(passiveRelic);
        var relic = (LateReplacementRelic)ModelDb.Relic<LateReplacementRelic>().MutableClone();
        relic.AssignOwner(player);
        player.AddRelicInternal(relic);
        var reward = new CardReward(player, CardRarityOddsType.RegularEncounter);
        reward.Populate(runState);
        IReadOnlyList<CardModel> firstOptions = reward.Options;
        IList<CardModel> exposedList = Assert.IsAssignableFrom<IList<CardModel>>(reward.Options);
        Assert.Throws<NotSupportedException>(() => exposedList[0] = ModelDb.Card<LateModificationCommonCard>());

        reward.Populate(runState);

        Assert.NotSame(firstOptions, reward.Options);
        Assert.Equal(2, relic.AfterCallbackCount);
        Assert.All(reward.Options, option => Assert.Same(ModelDb.Card<LateModificationReplacementCard>(), option));
    }

    private static LateSequenceRelic AttachSequenceRelic(Player player, string label, CardModel? replacement, List<string> trace)
    {
        var relic = (LateSequenceRelic)ModelDb.Relic<LateSequenceRelic>().MutableClone();
        relic.Label = label;
        relic.Replacement = replacement;
        relic.Trace = trace;
        relic.AssignOwner(player);
        player.AddRelicInternal(relic);
        return relic;
    }

    private static (RunState RunState, Player Player) CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
