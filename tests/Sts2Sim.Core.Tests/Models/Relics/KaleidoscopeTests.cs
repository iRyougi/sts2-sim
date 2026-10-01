using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class KaleidoscopeTests : IDisposable
{
    public KaleidoscopeTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("kaleidoscope-offer", false, null, null)]
    [InlineData("34WWJ6MXE9NN", true,
        new[] { "WISP", "SWEEPING_BEAM", "CRUSH_UNDER" },
        new[] { "SOW", "VICIOUS", "ASTRAL_PULSE" })]
    [InlineData("7LTB4PEDGP3E", true,
        new[] { "TESLA_COIL", "SHROUD", "TREMBLE" },
        new[] { "ITERATION", "PALE_BLUE_DOT", "DEATHS_DOOR" })]
    public async Task AfterObtained_OffersTwoCardChoicesDrawnFromOtherCharacterPools(
        string seed, bool isSilent, string[]? firstNativeCandidates, string[]? secondNativeCandidates)
    {
        (RunState runState, Player player) = CreateRun(seed, isSilent);
        EventRoom room = await EnterEventRoom(runState);
        int deckCountBefore = player.Deck.Cards.Count;
        int nicheCounterBefore = runState.Rng.Niche.Counter;
        int rewardsCounterBefore = player.PlayerRng.Rewards.Counter;

        // v0.111.0 sorts other character pools by their ModelId before Niche shuffling.
        // All five character pools are registered: each choice draws from three of the other four.
        List<CardPoolModel> otherPools = player.UnlockState.CharacterCardPools
            .Where(pool => pool.GetType() != player.Character.CardPool.GetType())
            .ToList();
        int expectedOptions = Math.Min(3, otherPools.Count);
        HashSet<ModelId> otherPoolCardIds = otherPools
            .SelectMany(pool => pool.GetUnlockedCards(player.UnlockState, isMultiplayer: false))
            .Select(card => card.Id)
            .ToHashSet();
        Assert.NotEmpty(otherPoolCardIds);
        if (firstNativeCandidates is not null)
        {
            // Public 09a2 rerun-200 decisions rows 0→1: both streams start at zero.
            Assert.True(nicheCounterBefore == 0 && rewardsCounterBefore == 0,
                $"seed={seed}, before obtain: Niche={nicheCounterBefore}, Rewards={rewardsCounterBefore}");
        }

        await RelicCmd.Obtain(ModelDb.Relic<Kaleidoscope>(), player);

        Assert.True(runState.Rng.Niche.Counter - nicheCounterBefore == 6,
            $"seed={seed}, obtain: Niche delta={runState.Rng.Niche.Counter - nicheCounterBefore}, expected=6");
        Assert.True(player.PlayerRng.Rewards.Counter - rewardsCounterBefore == 18,
            $"seed={seed}, obtain: Rewards delta={player.PlayerRng.Rewards.Counter - rewardsCounterBefore}, expected=18");

        Assert.True(room.Event.TryDequeuePendingRewardOffer(out RewardsSet? rewards));
        Assert.NotNull(rewards);
        Assert.False(room.Event.TryDequeuePendingRewardOffer(out _));
        Assert.Equal(expectedOptions, rewards.Card.Options.Count);
        CardReward second = Assert.IsType<CardReward>(Assert.Single(rewards.ExtraRewards));
        Assert.Equal(expectedOptions, second.Options.Count);
        if (firstNativeCandidates is not null && secondNativeCandidates is not null)
        {
            // Native ordered candidates from archive/09a2-captures, rerun-200 rows 1 and 2.
            // Keep these independent of the simulator's pool ordering and card factory.
            string[] firstActual = rewards.Card.Options.Select(card => card.Id.Entry).ToArray();
            string[] secondActual = second.Options.Select(card => card.Id.Entry).ToArray();
            Assert.True(firstNativeCandidates.SequenceEqual(firstActual),
                $"seed={seed}, reward=1: expected=[{string.Join(",", firstNativeCandidates)}], actual=[{string.Join(",", firstActual)}]");
            Assert.True(secondNativeCandidates.SequenceEqual(secondActual),
                $"seed={seed}, reward=2: expected=[{string.Join(",", secondNativeCandidates)}], actual=[{string.Join(",", secondActual)}]");
        }
        foreach (CardReward choice in new[] { rewards.Card, second })
        {
            Assert.Equal(expectedOptions, choice.Options.Select(card => card.Id).Distinct().Count());
            Assert.All(choice.Options, card =>
            {
                // 核心断言：卡来自他系角色池，而不是玩家自己的卡池。
                Assert.Contains(card.Id, otherPoolCardIds);
                Assert.False(card.IsCanonical);
                Assert.Same(player, card.Owner);
                Assert.False(card.IsColorless);
                Assert.False(card.IsMultiplayerOnly);
            });
        }

        CardModel firstChosen = rewards.Card.Options[0];
        CardModel secondChosen = second.Options[0];
        await rewards.Card.SelectOption(firstChosen);
        await second.SelectOption(secondChosen);

        CardModel[] added = player.Deck.Cards.Skip(deckCountBefore).ToArray();
        Assert.Equal(2, added.Length);
        Assert.Equal(new[] { firstChosen.Id, secondChosen.Id }, added.Select(card => card.Id));
        Assert.All(added, card =>
        {
            Assert.False(card.IsCanonical);
            Assert.Same(player, card.Owner);
            Assert.Same(player.Deck, card.Pile);
        });
    }

    [Fact]
    public void ExplicitCardReward_PopulateIsIdempotent()
    {
        (RunState runState, Player player) = CreateRun("kaleidoscope-explicit-idempotent");
        CardModel[] explicitOptions = ModelDb.All<CardModel>()
            .Where(card => !card.IsColorless && !card.IsMultiplayerOnly)
            .Take(3)
            .ToArray();
        var reward = new CardReward(player, explicitOptions);
        int rewardsCounterBefore = player.PlayerRng.Rewards.Counter;

        reward.Populate(runState);
        CardModel[] firstPopulate = reward.Options.ToArray();
        reward.Populate(runState);

        Assert.Equal(explicitOptions, reward.Options);
        Assert.Equal(firstPopulate, reward.Options);
        Assert.Equal(rewardsCounterBefore, player.PlayerRng.Rewards.Counter);
    }

    [Fact]
    public void Metadata_MatchesAncientUponPickupBehavior()
    {
        Kaleidoscope relic = ModelDb.Relic<Kaleidoscope>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }

    [Fact]
    public void IsAllowedAtNeow_FullUnlockSave_AllowsCandidateWithoutRunCharacters()
    {
        var runState = new RunState("kaleidoscope-full-unlock", new Overgrowth());

        Assert.True(ModelDb.Relic<Kaleidoscope>().IsAllowedAtNeow(runState));
    }
    private static (RunState RunState, Player Player) CreateRun(string seed, bool isSilent = false)
    {
        var runState = new RunState(seed, new Overgrowth(), ascensionLevel: isSilent ? 10 : 0);
        CharacterModel character = isSilent ? ModelDb.Character<Silent>() : ModelDb.Character<Regent>();
        Player player = Player.CreateForNewRun(character, runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static async Task<EventRoom> EnterEventRoom(RunState runState)
    {
        var room = new EventRoom(() =>
            (EventModel)ModelDb.Event<JungleMazeAdventure>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        return room;
    }
}
