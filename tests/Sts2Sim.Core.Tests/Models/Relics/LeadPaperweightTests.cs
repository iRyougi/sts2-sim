using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class LeadPaperweightTests : IDisposable
{
    public LeadPaperweightTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("367ZPGQGPUEH", 10, "CARD.SEEKER_STRIKE", "CARD.STRATAGEM")]
    [InlineData("ANG5LBQQNZ9B", 10, "CARD.THINKING_AHEAD", "CARD.SHOCKWAVE")]
    [InlineData("U66QY28XY3JN", 0, "CARD.SECRET_TECHNIQUE", "CARD.PANIC_BUTTON")]
    [InlineData("ZYUAG4ZYJAY2", 10, "CARD.SPLASH", "CARD.PURITY")]
    public async Task NeowLeadPaperweight_OffersNativeOrderedCandidates(
        string seed, int ascensionLevel, string first, string second)
    {
        // 09a counterFirstStop: all four runs diverged at this card_select, with matching prior RNG rolls.
        var run = new RunState(seed, ascensionLevel: ascensionLevel);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var source = new CaptureOnlyChoice();
        run.ConfigureCardSelectionSource(source);
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
        var room = Assert.IsType<EventRoom>(RoomFactory.CreateAncientEventRoom(run));
        run.PushRoom(room);
        try
        {
            await room.Enter(run);
            await room.Event.ChooseOption(
                room.Event.CurrentOptions.Single(option => option.Key == "LeadPaperweight"));

            Assert.NotNull(source.Request);
            Assert.Equal(new[] { first, second },
                source.Request.Candidates.Select(card => card.Id.ToString()));
        }
        finally
        {
            await room.Exit(run);
            run.PopCurrentRoom();
        }
    }

    private sealed class CaptureOnlyChoice : ICardSelectionDecisionSource
    {
        public CardSelectionRequest? Request { get; private set; }

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Request = request;
            return Task.FromResult<IReadOnlyList<CardModel>>([]);
        }
    }

    [Fact]
    public async Task ArchivedR8P62QK7DEDJ_NeowLeadPaperweightAddsChosenThinkingAhead()
    {
        // Archive F1: LeadPaperweight chosen, ThinkingAhead gained, FlashOfSteel unpicked.
        // These expected identities come from run_history.json, not a factory replay.
        var run = new RunState("R8P62QK7DEDJ", ascensionLevel: 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var source = new ArchivedLeadPaperweightChoice();
        run.ConfigureCardSelectionSource(source);
        int deckCountBefore = player.Deck.Cards.Count;
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
        var room = Assert.IsType<EventRoom>(RoomFactory.CreateAncientEventRoom(run));
        run.PushRoom(room);
        try
        {
            await room.Enter(run);
            await room.Event.ChooseOption(room.Event.CurrentOptions.Single(option => option.Key == "LeadPaperweight"));

            Assert.NotNull(source.Request);
            Assert.Equal(0, source.Request.MinCount);
            Assert.Equal(1, source.Request.MaxCount);
            Assert.True(source.Request.Cancelable);
            Assert.IsType<LeadPaperweight>(source.Request.Source);
            Assert.Equal(new[] { "CARD.FLASH_OF_STEEL", "CARD.THINKING_AHEAD" },
                source.Request.Candidates.Select(card => card.Id.ToString()).Order());
            CardModel added = Assert.Single(player.Deck.Cards.Skip(deckCountBefore));
            Assert.Equal("CARD.THINKING_AHEAD", added.Id.ToString());
            Assert.Same(source.Chosen, added);
            Assert.Same(player, added.Owner);
            Assert.Same(player.Deck, added.Pile);
        }
        finally
        {
            await room.Exit(run);
            run.PopCurrentRoom();
        }
    }

    private sealed class ArchivedLeadPaperweightChoice : ICardSelectionDecisionSource
    {
        public CardSelectionRequest? Request { get; private set; }
        public CardModel? Chosen { get; private set; }

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Request = request;
            Chosen = request.Candidates.Single(card => card.Id.ToString() == "CARD.THINKING_AHEAD");
            return Task.FromResult<IReadOnlyList<CardModel>>([Chosen]);
        }
    }

    [Fact]
    public async Task AfterObtained_GeneratesTwoColorlessPoolOptionsAndAddsOnlyOne()
    {
        const string seed = "lead-paperweight";

        // 偏离 #305：权威就是从 ColorlessCardPool 抽 2 张。此前本测试把旧实现的
        // "滚稀有度 + 空桶回退到 C/U/R" 整段复算了一遍当预期——那是在复制被修的 bug。
        HashSet<ModelId> colorlessPoolIds = ColorlessCardPool.Instance
            .GetUnlockedCards(PlayerUnlockState.AllUnlocked(), isMultiplayer: false)
            .Select(card => card.Id)
            .ToHashSet();
        Assert.NotEmpty(colorlessPoolIds);

        Player player = CreatePlayer(seed);
        int deckCountBefore = player.Deck.Cards.Count;
        int rewardsCounterBefore = player.PlayerRng.Rewards.Counter;

        await RelicCmd.Obtain(ModelDb.Relic<LeadPaperweight>(), player);

        CardModel added = Assert.Single(player.Deck.Cards.Skip(deckCountBefore));
        Assert.Contains(added.Id, colorlessPoolIds);
        Assert.True(added.IsColorless);
        Assert.False(added.IsMultiplayerOnly);
        Assert.False(added.IsCanonical);
        Assert.Same(player, added.Owner);
        Assert.Same(player.Deck, added.Pile);
        Assert.True(player.PlayerRng.Rewards.Counter > rewardsCounterBefore);
    }

    [Fact]
    public void Metadata_MatchesAncientWithoutUponPickupPreview()
    {
        LeadPaperweight relic = ModelDb.Relic<LeadPaperweight>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.HasUponPickupEffect);
    }

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
