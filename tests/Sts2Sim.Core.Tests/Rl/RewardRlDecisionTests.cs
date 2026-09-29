using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rl;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rl;

public class RewardRlDecisionTests : IDisposable
{
    public RewardRlDecisionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Classify_ForcedRewardsAndDone_AreAutomatic()
    {
        (RunState _, Player player) = NewRun("reward-classify-forced");
        RewardsSet rewards = RewardsSet.CreateCustom(
            player,
            gold: new GoldReward(17, player));

        var gold = Assert.IsType<RewardDecisionClassification.Automatic>(
            RewardDecisionClassifier.Classify(rewards));
        Assert.IsType<RewardDecision.TakeGold>(gold.Decision);

        await rewards.Gold.Take();

        var done = Assert.IsType<RewardDecisionClassification.Automatic>(
            RewardDecisionClassifier.Classify(rewards));
        Assert.IsType<RewardDecision.Done>(done.Decision);
    }

    [Fact]
    public async Task Classify_CardReward_OffersCardsInOrderThenSkip()
    {
        (RunState runState, Player player) = NewRun("reward-classify-card");
        CardReward cardReward = NewCardReward(
            runState,
            player,
            ModelDb.Card<StrikeRegent>(),
            ModelDb.Card<DefendRegent>());
        RewardsSet rewards = RewardsSet.CreateCustom(player, card: cardReward);
        await rewards.Gold.Take();

        var choice = Assert.IsType<RewardDecisionClassification.Choice>(
            RewardDecisionClassifier.Classify(rewards));

        Assert.Equal(3, choice.Candidates.Count);
        Assert.Same(cardReward.Options[0], Assert.IsType<RewardDecision.TakeCard>(choice.Candidates[0]).Card);
        Assert.Same(cardReward.Options[1], Assert.IsType<RewardDecision.TakeCard>(choice.Candidates[1]).Card);
        Assert.IsType<RewardDecision.SkipCard>(choice.Candidates[2]);
    }

    [Fact]
    public async Task Classify_CardRewardWithoutCards_AutomaticallySkips()
    {
        (RunState runState, Player player) = NewRun("reward-classify-empty-card");
        CardReward cardReward = NewCardReward(runState, player);
        RewardsSet rewards = RewardsSet.CreateCustom(player, card: cardReward);
        await rewards.Gold.Take();

        var automatic = Assert.IsType<RewardDecisionClassification.Automatic>(
            RewardDecisionClassifier.Classify(rewards));

        Assert.IsType<RewardDecision.SkipCard>(automatic.Decision);
    }

    [Fact]
    public async Task Classify_ExtraCardReward_OffersResolveCandidatesAndSkip()
    {
        (RunState runState, Player player) = NewRun("reward-classify-extra-card");
        CardReward extra = NewCardReward(runState, player, ModelDb.Card<StrikeRegent>());
        RewardsSet rewards = RewardsSet.CreateCustom(player, extraRewards: [extra]);
        await rewards.Gold.Take();

        var choice = Assert.IsType<RewardDecisionClassification.Choice>(
            RewardDecisionClassifier.Classify(rewards));

        Assert.Equal(2, choice.Candidates.Count);
        var take = Assert.IsType<RewardDecision.ResolveExtra>(choice.Candidates[0]);
        Assert.Same(extra, take.Reward);
        Assert.Same(extra.Options[0], take.SelectedCard);
        var skip = Assert.IsType<RewardDecision.ResolveExtra>(choice.Candidates[1]);
        Assert.Same(extra, skip.Reward);
        Assert.Null(skip.SelectedCard);
    }

    [Fact]
    public async Task EncodeRewardDecision_UsesOrderedRewardSlotsLabelsAndMask()
    {
        (RunState runState, Player player) = NewRun("reward-encode");
        CardReward cardReward = NewCardReward(
            runState,
            player,
            ModelDb.Card<StrikeRegent>(),
            ModelDb.Card<DefendRegent>());
        RewardsSet rewards = RewardsSet.CreateCustom(player, card: cardReward);
        await rewards.Gold.Take();

        ObservationSnapshot snapshot = ObservationEncoder.EncodeRewardDecision(runState, rewards);

        Assert.Equal(DecisionType.Reward, snapshot.DecisionType);
        Assert.Equal(
            [ActionSpaceLayout.RewardIndex(0), ActionSpaceLayout.RewardIndex(1), ActionSpaceLayout.RewardIndex(2)],
            snapshot.Candidates.Select(candidate => candidate.SlotIndex));
        Assert.Equal(
            [$"take_card:{cardReward.Options[0].Id}", $"take_card:{cardReward.Options[1].Id}", "skip_card"],
            snapshot.Candidates.Select(candidate => candidate.Label));
        Assert.Equal(3, snapshot.LegalActionMask.Count(isLegal => isLegal));
        Assert.True(snapshot.LegalActionMask[ActionSpaceLayout.RewardIndex(0)]);
        Assert.True(snapshot.LegalActionMask[ActionSpaceLayout.RewardIndex(2)]);
        Assert.False(snapshot.LegalActionMask[ActionSpaceLayout.RewardIndex(3)]);
    }

    [Fact]
    public async Task EncodeRewardDecision_ThrowsForAutomaticOrOverflowState()
    {
        (RunState runState, Player player) = NewRun("reward-encode-invalid");
        RewardsSet forced = RewardsSet.CreateCustom(player, gold: new GoldReward(1, player));
        CardReward overflowCard = NewCardReward(
            runState,
            player,
            ModelDb.Card<StrikeRegent>(),
            ModelDb.Card<DefendRegent>(),
            ModelDb.Card<StrikeRegent>(),
            ModelDb.Card<DefendRegent>(),
            ModelDb.Card<StrikeRegent>());
        RewardsSet overflow = RewardsSet.CreateCustom(player, card: overflowCard);
        await overflow.Gold.Take();

        Assert.Throws<InvalidOperationException>(
            () => ObservationEncoder.EncodeRewardDecision(runState, forced));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ObservationEncoder.EncodeRewardDecision(runState, overflow));
    }

    [Fact]
    public async Task DecodeRewardChoice_ReturnsCardOrSkipAtMatchingPosition()
    {
        (RunState runState, Player player) = NewRun("reward-decode");
        CardReward cardReward = NewCardReward(
            runState,
            player,
            ModelDb.Card<StrikeRegent>(),
            ModelDb.Card<DefendRegent>());
        RewardsSet rewards = RewardsSet.CreateCustom(player, card: cardReward);
        await rewards.Gold.Take();

        RewardDecision selected = ActionDecoder.DecodeRewardChoice(
            ActionSpaceLayout.RewardIndex(1),
            rewards);
        RewardDecision skipped = ActionDecoder.DecodeRewardChoice(
            ActionSpaceLayout.RewardIndex(2),
            rewards);

        Assert.Same(cardReward.Options[1], Assert.IsType<RewardDecision.TakeCard>(selected).Card);
        Assert.IsType<RewardDecision.SkipCard>(skipped);
    }

    [Fact]
    public async Task DecodeRewardChoice_RejectsWrongRangeUnusedSlotAndOverflow()
    {
        (RunState runState, Player player) = NewRun("reward-decode-invalid");
        CardReward cardReward = NewCardReward(runState, player, ModelDb.Card<StrikeRegent>());
        RewardsSet rewards = RewardsSet.CreateCustom(player, card: cardReward);
        await rewards.Gold.Take();

        Assert.Throws<ArgumentException>(
            () => ActionDecoder.DecodeRewardChoice(ActionSpaceLayout.MapPointIndex(0), rewards));
        Assert.Throws<ArgumentException>(
            () => ActionDecoder.DecodeRewardChoice(ActionSpaceLayout.RewardIndex(2), rewards));

        CardReward overflowCard = NewCardReward(
            runState,
            player,
            ModelDb.Card<StrikeRegent>(),
            ModelDb.Card<DefendRegent>(),
            ModelDb.Card<StrikeRegent>(),
            ModelDb.Card<DefendRegent>(),
            ModelDb.Card<StrikeRegent>());
        RewardsSet overflow = RewardsSet.CreateCustom(player, card: overflowCard);
        await overflow.Gold.Take();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ActionDecoder.DecodeRewardChoice(ActionSpaceLayout.RewardIndex(0), overflow));
    }

    private static (RunState runState, Player player) NewRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static CardReward NewCardReward(
        RunState runState,
        Player player,
        params CardModel[] options)
    {
        var reward = new CardReward(player, options);
        reward.Populate(runState);
        return reward;
    }
}
