using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rewards;

file sealed class TestCommonSkill : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;
}

file sealed class TestUncommonAttack : CardModel
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;
}

[Collection("ModelDb")]
public class CardRewardTests : IDisposable
{
    public CardRewardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(TestCommonSkill), typeof(TestUncommonAttack),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Populate_OffersUpToThreeDistinctNonBasicOptions()
    {
        CardReward reward = CreateReward("card-reward-options");

        Assert.InRange(reward.Options.Count, 0, 3);
        Assert.All(reward.Options, card => Assert.NotEqual(CardRarity.Basic, card.Rarity));
        Assert.Equal(reward.Options.Count, reward.Options.Distinct().Count());
    }

    [Fact]
    public async Task SelectOption_AddsOneOwnedMutableCopyToDeck_AndIsIdempotent()
    {
        (Player player, CardReward reward) = CreateRewardWithOption();
        CardModel offered = reward.Options[0];
        int deckSizeBefore = player.Deck.Cards.Count;
        Assert.Null(reward.SelectedOption);

        Task first = reward.SelectOption(offered);
        Task retry = reward.SelectOption(offered);
        await Task.WhenAll(first, retry);
        Assert.Same(offered, reward.SelectedOption);

        CardModel added = Assert.Single(player.Deck.Cards.Skip(deckSizeBefore));
        Assert.Same(first, retry);
        Assert.Equal(offered.Id, added.Id);
        Assert.NotSame(offered, added);
        Assert.False(added.IsCanonical);
        Assert.Same(player, added.Owner);
        Assert.Same(player.Deck, added.Pile);
        Assert.True(reward.IsResolved);
    }

    [Fact]
    public void SelectOption_WithCardOutsideTheOfferedOptions_ThrowsWithoutResolving()
    {
        (Player player, CardReward reward) = CreateRewardWithOption();
        int deckSizeBefore = player.Deck.Cards.Count;

        Assert.Throws<InvalidOperationException>(() => SelectOption(reward, ModelDb.Card<StrikeRegent>()));
        Assert.Null(reward.SelectedOption);

        Assert.Equal(deckSizeBefore, player.Deck.Cards.Count);
        Assert.False(reward.IsResolved);
    }

    [Fact]
    public async Task SelectOption_ConflictingOptionOrSkipAfterSelectionThrowsWithoutReplaying()
    {
        (Player player, CardReward reward) = CreateRewardWithOptions(2);
        CardModel chosen = reward.Options[0];
        CardModel conflicting = reward.Options[1];
        int deckSizeBefore = player.Deck.Cards.Count;

        Task selected = reward.SelectOption(chosen);
        await selected;

        Assert.Throws<InvalidOperationException>(() => SelectOption(reward, conflicting));
        Assert.Throws<InvalidOperationException>(() => Skip(reward));
        Assert.Equal(deckSizeBefore + 1, player.Deck.Cards.Count);
        Assert.True(reward.IsResolved);
    }

    [Fact]
    public async Task Skip_ResolvesWithoutAddingCards_AndRejectsSelection()
    {
        (Player player, CardReward reward) = CreateRewardWithOption();
        CardModel offered = reward.Options[0];
        int deckSizeBefore = player.Deck.Cards.Count;

        Task first = reward.Skip();
        Task retry = reward.Skip();
        await Task.WhenAll(first, retry);
        Assert.Null(reward.SelectedOption);

        Assert.Same(first, retry);
        Assert.Throws<InvalidOperationException>(() => SelectOption(reward, offered));
        Assert.Equal(deckSizeBefore, player.Deck.Cards.Count);
        Assert.True(reward.IsResolved);
    }

    [Fact]
    public async Task SelectOption_ConcurrentSkip_AllowsOneResolutionAndOneSideEffect()
    {
        (Player player, CardReward reward) = CreateRewardWithOption();
        CardModel offered = reward.Options[0];
        int deckSizeBefore = player.Deck.Cards.Count;
        using var start = new ManualResetEventSlim(false);
        Task? selectTask = null;
        Task? skipTask = null;
        Exception? selectException = null;
        Exception? skipException = null;

        Task selectCaller = Task.Run(() =>
        {
            start.Wait();
            try
            {
                selectTask = reward.SelectOption(offered);
            }
            catch (Exception exception)
            {
                selectException = exception;
            }
        });
        Task skipCaller = Task.Run(() =>
        {
            start.Wait();
            try
            {
                skipTask = reward.Skip();
            }
            catch (Exception exception)
            {
                skipException = exception;
            }
        });

        start.Set();
        await Task.WhenAll(selectCaller, skipCaller);
        await Task.WhenAll(new[] { selectTask, skipTask }.OfType<Task>());

        Assert.True((selectTask is null) != (skipTask is null));
        Assert.True(selectException is InvalidOperationException || skipException is InvalidOperationException);
        Assert.Equal(deckSizeBefore + (selectTask is null ? 0 : 1), player.Deck.Cards.Count);
        Assert.True(reward.IsResolved);
    }

    private static CardReward CreateReward(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var reward = new CardReward(player, CardRarityOddsType.RegularEncounter);
        reward.Populate(runState);
        return reward;
    }

    private static void SelectOption(CardReward reward, CardModel option)
    {
        _ = reward.SelectOption(option);
    }

    private static void Skip(CardReward reward)
    {
        _ = reward.Skip();
    }

    private static (Player Player, CardReward Reward) CreateRewardWithOption()
    {
        return CreateRewardWithOptions(1);
    }

    private static (Player Player, CardReward Reward) CreateRewardWithOptions(int minimumOptionCount)
    {
        for (int i = 0; i < 100; i++)
        {
            var runState = new RunState($"card-reward-select-{i}", new Overgrowth());
            Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
            runState.AddPlayer(player);
            var reward = new CardReward(player, CardRarityOddsType.RegularEncounter);
            reward.Populate(runState);
            if (reward.Options.Count >= minimumOptionCount)
            {
                return (player, reward);
            }
        }

        throw new InvalidOperationException($"Expected a deterministic test seed to offer at least {minimumOptionCount} non-basic card options.");
    }
}
