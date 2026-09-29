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

    [Fact]
    public async Task AfterObtained_OffersTwoCardChoicesDrawnFromOtherCharacterPools()
    {
        (RunState runState, Player player) = CreateRun("kaleidoscope-offer");
        EventRoom room = await EnterEventRoom(runState);
        int deckCountBefore = player.Deck.Cards.Count;

        // 偏离 #307：权威用 Rng.Niche 打乱"非本角色卡池"取前 3 个、每池抽 1 张。
        // 期望张数因此随已实现角色数增长：当前 2 个角色 ⇒ 他系池 1 个 ⇒ 每份 1 张。
        // 这里不写死 3，否则新增角色时又要回来改。
        List<CardPoolModel> otherPools = player.UnlockState.CharacterCardPools
            .Where(pool => pool.GetType() != player.Character.CardPool.GetType())
            .ToList();
        int expectedOptions = Math.Min(3, otherPools.Count);
        HashSet<ModelId> otherPoolCardIds = otherPools
            .SelectMany(pool => pool.GetUnlockedCards(player.UnlockState, isMultiplayer: false))
            .Select(card => card.Id)
            .ToHashSet();
        Assert.NotEmpty(otherPoolCardIds);

        await RelicCmd.Obtain(ModelDb.Relic<Kaleidoscope>(), player);

        Assert.True(room.Event.TryDequeuePendingRewardOffer(out RewardsSet? rewards));
        Assert.NotNull(rewards);
        Assert.False(room.Event.TryDequeuePendingRewardOffer(out _));
        Assert.Equal(expectedOptions, rewards.Card.Options.Count);
        CardReward second = Assert.IsType<CardReward>(Assert.Single(rewards.ExtraRewards));
        Assert.Equal(expectedOptions, second.Options.Count);
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
    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
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
