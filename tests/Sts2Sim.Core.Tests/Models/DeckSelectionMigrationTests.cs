using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class DeckSelectionMigrationTests : IDisposable
{
    public DeckSelectionMigrationTests() { ModelDb.ResetForTests(); ModelDb.Init(ContentRegistry.AllTypes); }
    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("AromaOfChaos", "LET_GO", "transform", 1)]
    [InlineData("AromaOfChaos", "MAINTAIN_CONTROL", "upgrade", 1)]
    [InlineData("FieldOfManSizedHoles", "RESIST", "remove", 2)]
    [InlineData("LuminousChoir", "REACH_INTO_THE_FLESH", "remove", 2)]
    [InlineData("MorphicGrove", "GROUP", "transform", 2)]
    [InlineData("SapphireSeed", "EAT", "upgrade", 1)]
    [InlineData("SpiritGrafter", "REJECTION", "upgrade", 1)]
    [InlineData("Wellspring", "BATHE", "remove", 1)]
    [InlineData("WhisperingHollow", "HUG", "transform", 1)]
    [InlineData("ZenWeaver", "EMOTIONAL_AWARENESS", "remove", 1)]
    [InlineData("ZenWeaver", "ARACHNID_ACUPUNCTURE", "remove", 2)]
    [InlineData("BiiigHug", null, "remove", 4)]
    [InlineData("NewLeaf", null, "transform", 1)]
    [InlineData("Pomander", null, "upgrade", 1)]
    [InlineData("PrecariousShears", null, "remove", 2)]
    [InlineData("YummyCookie", null, "upgrade", 4)]
    public async Task CallSites_WaitForNonFirstChoice(string name, string? option, string operation, int count)
    {
        var (run, player, source) = Setup("deck-choice-" + name + option);
        player.Gold = 300;
        AbstractModel model = (AbstractModel)(option is null
            ? ModelDb.All<RelicModel>().Single(m => m.GetType().Name == name).MutableClone()
            : ModelDb.All<EventModel>().Single(m => m.GetType().Name == name).MutableClone());
        Task effect;
        if (model is EventModel ev)
        {
            ev.AssignOwner(player); ev.BeginEvent(run);
            effect = ev.ChooseOption(ev.CurrentOptions.Single(o => o.Key == option));
        }
        else
        {
            var relic = (RelicModel)model; relic.AssignOwner(player); effect = relic.AfterObtained();
        }
        CardSelectionRequest request = Assert.IsType<CardSelectionRequest>(source.Request);
        Assert.False(effect.IsCompleted);
        Assert.Same(model, request.Source);
        Assert.Equal(count, request.MinCount); Assert.Equal(count, request.MaxCount);
        Assert.False(request.Cancelable);
        Assert.All(request.Candidates, c => Assert.Contains(c, player.Deck.Cards));
        if (name == "ZenWeaver") Assert.Equal(300, player.Gold);
        int nicheBefore = run.Rng.Niche.Counter;
        CardModel[] chosen = request.Candidates.TakeLast(count).Reverse().ToArray();
        CardModel untouched = request.Candidates[0];
        Assert.All(chosen, c => Assert.False(c.IsUpgraded));
        Assert.Equal(nicheBefore, run.Rng.Niche.Counter);
        source.Complete(chosen);
        await effect;
        Assert.Contains(untouched, player.Deck.Cards);
        if (operation == "upgrade")
        {
            Assert.All(chosen, c => Assert.True(c.IsUpgraded));
            Assert.False(untouched.IsUpgraded);
        }
        else Assert.All(chosen, c => Assert.DoesNotContain(player.Deck.Cards, other => ReferenceEquals(c, other)));
        if (name == "ZenWeaver") Assert.Equal(option == "EMOTIONAL_AWARENESS" ? 175 : 50, player.Gold);
        if (name == "NewLeaf") Assert.True(run.Rng.Niche.Counter > nicheBefore);
    }

    [Theory]
    [InlineData("upgrade")]
    [InlineData("transform")]
    [InlineData("remove")]
    public async Task Helpers_FilterCandidates_HandleForcedSelection_AndRejectInvalidChoices(string kind)
    {
        var (run, player, source) = Setup("deck-helper-" + kind);
        CardModel curse = (CardModel)ModelDb.Card<Greed>().MutableClone();
        curse.AssignOwner(player); CardPileCmd.Add(curse, PileType.Deck);
        CardCmd.Upgrade(player.Deck.Cards[0]);
        Task<IReadOnlyList<CardModel>> Select(int count) => kind switch
        {
            "upgrade" => CardSelectCmd.FromDeckForUpgrade(player, count),
            "transform" => CardSelectCmd.FromDeckForTransformation(player, count),
            _ => CardSelectCmd.FromDeckForRemoval(player, count)
        };
        var task = Select(1);
        var expected = player.Deck.Cards.Where(c => kind switch
        {
            "upgrade" => c.IsUpgradable,
            "transform" => c.Type != CardType.Quest && c.IsTransformable,
            _ => c.IsRemovable
        });
        if (kind == "remove") expected = expected.OrderBy(c => c.Type == CardType.Curse ? 0 : 1);
        Assert.Equal(expected, source.Request!.Candidates);
        source.Complete([curse, curse]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal(expected, await Select(99));
        foreach (CardModel card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        Assert.Empty(await Select(1));
    }

    [Fact]
    public async Task ScrollBoxes_SelectsWholeSecondBundle_AfterInstantiatingBoth()
    {
        var (run, player, source) = Setup("scroll-bundle-choice");
        var relic = (ScrollBoxes)ModelDb.Relic<ScrollBoxes>().MutableClone(); relic.AssignOwner(player);
        int before = player.Deck.Cards.Count;
        var effect = relic.AfterObtained();
        var request = source.Request!;
        Assert.False(effect.IsCompleted);
        Assert.Equal(before, player.Deck.Cards.Count);
        Assert.Equal(2, request.Bundles!.Count);
        Assert.All(request.Bundles.SelectMany(b => b), card => { Assert.Same(player, card.Owner); Assert.Null(card.Pile); });
        source.Complete([request.Candidates[1]]);
        await effect;
        Assert.Equal(request.Bundles[1], player.Deck.Cards.Skip(before));
        Assert.All(request.Bundles[0], card => Assert.Null(card.Pile));
    }

    private static (RunState, Player, DeferredSource) Setup(string seed)
    {
        var run = new RunState(seed, new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run); run.AddPlayer(player);
        var source = new DeferredSource(); run.ConfigureCardSelectionSource(source);
        return (run, player, source);
    }
    private sealed class DeferredSource : ICardSelectionDecisionSource
    {
        private readonly TaskCompletionSource<IReadOnlyList<CardModel>> _selection = new();
        public CardSelectionRequest? Request { get; private set; }
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) { Request = request; return _selection.Task; }
        public void Complete(IReadOnlyList<CardModel> cards) => _selection.SetResult(cards);
    }
}
