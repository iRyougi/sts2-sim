using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class UnderdocksEventBatchBTests : IDisposable
{
    public UnderdocksEventBatchBTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("SunkenTreasury", "FIRST_CHEST")]
    [InlineData("DoorsOfLightAndDark", "LIGHT")]
    [InlineData("TrashHeap", "GRAB")]
    [InlineData("WaterloggedScriptorium", "TENTACLE_QUILL")]
    public async Task EventMainBranchMatchesAuthoritativePort(string eventName, string optionKey)
    {
        Type? eventType = typeof(EventModel).Assembly.GetType($"Sts2Sim.Core.Models.Events.{eventName}");
        Assert.NotNull(eventType);
        ModelDb.Init([eventType!]);

        var run = new RunState($"task6-{eventName}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        NonFirstCardsSelectionSource? selectionSource = null;
        CardModel? firstSelectable = null;
        if (eventName == "WaterloggedScriptorium")
        {
            selectionSource = new NonFirstCardsSelectionSource();
            run.ConfigureCardSelectionSource(selectionSource);
            Steady steady = ModelDb.GetById<Steady>(ModelDb.GetId<Steady>());
            firstSelectable = player.Deck.Cards.First(steady.CanEnchant);
        }
        else if (eventName == "DoorsOfLightAndDark")
        {
            selectionSource = new NonFirstCardsSelectionSource();
            run.ConfigureCardSelectionSource(selectionSource);
            firstSelectable = player.Deck.Cards.First(card => card.IsUpgradable);
        }
        EventModel ev = (EventModel)ModelDb.Get(eventType!).MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(run);

        Assert.Equal(optionKey is "TENTACLE_QUILL" ? 3 : 2, ev.CurrentOptions.Count);
        EventOption option = Assert.Single(ev.CurrentOptions, candidate => candidate.Key == optionKey);

        int goldBefore = player.Gold;
        int deckBefore = player.Deck.Cards.Count;
        int upgradesBefore = player.Deck.Cards.Count(card => card.IsUpgraded);
        int steadyBefore = player.Deck.Cards.Count(card => card.Enchantments.OfType<Steady>().Any());

        await ev.ChooseOption(option);

        Assert.True(ev.IsFinished);
        switch (eventName)
        {
            case "SunkenTreasury":
                Assert.InRange(player.Gold - goldBefore, 52, 67);
                break;
            case "DoorsOfLightAndDark":
                Assert.Equal(1, player.Deck.Cards.Count(card => card.IsUpgraded) - upgradesBefore);
                Assert.True(Assert.Single(selectionSource!.Selected).IsUpgraded);
                Assert.False(firstSelectable!.IsUpgraded);
                var darkRun = new RunState("task6-doors-dark", new Overgrowth());
                Player darkPlayer = Player.CreateForNewRun(ModelDb.Character<Regent>(), darkRun);
                darkRun.AddPlayer(darkPlayer);
                var darkSelector = new NonFirstCardsSelectionSource();
                darkRun.ConfigureCardSelectionSource(darkSelector);
                var darkEvent = (DoorsOfLightAndDark)ModelDb.Event<DoorsOfLightAndDark>().MutableClone();
                darkEvent.AssignOwner(darkPlayer);
                darkEvent.BeginEvent(darkRun);
                CardModel firstRemovable = darkPlayer.Deck.Cards.First(card => card.IsRemovable);
                int darkDeckBefore = darkPlayer.Deck.Cards.Count;
                await darkEvent.ChooseOption(darkEvent.CurrentOptions.Single(candidate => candidate.Key == "DARK"));
                CardModel removed = Assert.Single(darkSelector.Selected);
                Assert.NotSame(firstRemovable, removed);
                Assert.DoesNotContain(darkPlayer.Deck.Cards, card => ReferenceEquals(card, removed));
                Assert.Contains(darkPlayer.Deck.Cards, card => ReferenceEquals(card, firstRemovable));
                Assert.Equal(darkDeckBefore - 1, darkPlayer.Deck.Cards.Count);
                break;
            case "TrashHeap":
                Assert.Equal(deckBefore + 1, player.Deck.Cards.Count);
                break;
            case "WaterloggedScriptorium":
                Assert.Equal(goldBefore - 55, player.Gold);
                Assert.Equal(steadyBefore + 1, player.Deck.Cards.Count(card => card.Enchantments.OfType<Steady>().Any()));
                CardModel quillSelected = Assert.Single(selectionSource!.Selected);
                Assert.NotSame(firstSelectable, quillSelected);
                Assert.Single(quillSelected.Enchantments.OfType<Steady>());
                Assert.Empty(firstSelectable!.Enchantments.OfType<Steady>());
                var pricklyRun = new RunState("task6-scriptorium-prickly", new Overgrowth());
                Player pricklyPlayer = Player.CreateForNewRun(ModelDb.Character<Regent>(), pricklyRun);
                pricklyRun.AddPlayer(pricklyPlayer);
                pricklyPlayer.Gold = 200;
                var pricklySelector = new NonFirstCardsSelectionSource();
                pricklyRun.ConfigureCardSelectionSource(pricklySelector);
                var prickly = (WaterloggedScriptorium)ModelDb.Event<WaterloggedScriptorium>().MutableClone();
                prickly.AssignOwner(pricklyPlayer);
                prickly.BeginEvent(pricklyRun);
                await prickly.ChooseOption(prickly.CurrentOptions.Single(candidate => candidate.Key == "PRICKLY_SPONGE"));
                Assert.Equal(2, pricklySelector.Selected.Count);
                Assert.All(pricklySelector.Selected, card =>
                    Assert.Single(card.Enchantments.OfType<Steady>()));
                Assert.Equal(80, pricklyPlayer.Gold);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(eventName), eventName, null);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task DoorsLight_HandlesZeroOrOneUpgradeTargetWithoutRandomness(int eligibleCount)
    {
        var run = new RunState($"926-light-{eligibleCount}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        CardModel[] candidates = player.Deck.Cards.Where(card => card.IsUpgradable).ToArray();
        foreach (CardModel card in candidates.Skip(eligibleCount)) CardCmd.Upgrade(card);
        var selector = new NonFirstCardsSelectionSource();
        run.ConfigureCardSelectionSource(selector);
        var ev = (DoorsOfLightAndDark)ModelDb.Event<DoorsOfLightAndDark>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(run);
        int nicheBefore = run.Rng.Niche.Counter;
        int rngBefore = ev.Rng.Counter;

        EventOption light = Assert.Single(ev.CurrentOptions, option => option.Key == "LIGHT");
        Assert.True(light.IsEnabled);
        await ev.ChooseOption(light);

        Assert.True(ev.IsFinished);
        Assert.All(candidates, card => Assert.True(card.IsUpgraded));
        Assert.Null(selector.Request);
        Assert.Equal(nicheBefore, run.Rng.Niche.Counter);
        Assert.Equal(rngBefore, ev.Rng.Counter);
    }

    [Theory]
    [InlineData(119, 3)]
    [InlineData(120, 0)]
    [InlineData(120, 1)]
    [InlineData(120, 3)]
    public async Task PricklySponge_Uses120GoldAndAvailableTargets(int gold, int eligibleCount)
    {
        var run = new RunState($"926-sponge-{gold}-{eligibleCount}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        player.Gold = gold;
        Steady steady = ModelDb.GetById<Steady>(ModelDb.GetId<Steady>());
        CardModel[] enchantable = player.Deck.Cards.Where(steady.CanEnchant).ToArray();
        foreach (CardModel card in enchantable.Skip(eligibleCount)) await CardCmd.Enchant<Steady>(card, 1m);
        CardModel[] candidates = enchantable.Take(eligibleCount).ToArray();
        var selector = new NonFirstCardsSelectionSource();
        run.ConfigureCardSelectionSource(selector);
        var ev = (WaterloggedScriptorium)ModelDb.Event<WaterloggedScriptorium>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(run);
        int nicheBefore = run.Rng.Niche.Counter;
        int rngBefore = ev.Rng.Counter;
        EventOption sponge = Assert.Single(ev.CurrentOptions, option => option.Key == "PRICKLY_SPONGE");
        VisibleDecisionSnapshot snapshot = ev.GetVisibleState();
        VisibleOption description = snapshot.DescribeCandidate(sponge);
        Assert.Contains(VisibleValue.Int("Gold", 120), description.Values);
        Assert.Equal(gold >= 120, description.IsEnabled);
        Assert.Equal(gold, player.Gold);

        if (gold < 120)
        {
            Assert.True(sponge.IsLocked);
            Assert.Single(description.Values);
            Assert.Empty(description.Contents);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ev.ChooseOption(sponge));
            Assert.Equal(gold, player.Gold);
            Assert.All(candidates, card => Assert.Empty(card.Enchantments));
        }
        else
        {
            Assert.Contains(VisibleValue.Int("EnchantCount", 2), description.Values);
            await ev.ChooseOption(sponge);
            Assert.True(ev.IsFinished);
            Assert.Equal(0, player.Gold);
            Assert.Equal(Math.Min(2, eligibleCount), candidates.Count(card => card.Enchantments.OfType<Steady>().Any()));
            if (eligibleCount > 2)
            {
                Assert.Equal(candidates, selector.Request!.Candidates);
                Assert.Equal(0, selector.GoldAtRequest);
                Assert.Same(ev, selector.Request.Source);
                Assert.Equal(2, selector.Request.MinCount);
                Assert.Equal(2, selector.Request.MaxCount);
                Assert.All(selector.Selected, card => Assert.Equal(1m, Assert.Single(card.Enchantments).Magnitude));
                Assert.Empty(candidates[0].Enchantments);
            }
            else Assert.Null(selector.Request);
        }
        Assert.Equal(nicheBefore, run.Rng.Niche.Counter);
        Assert.Equal(rngBefore, ev.Rng.Counter);
    }

    private sealed class NonFirstCardsSelectionSource : ICardSelectionDecisionSource
    {
        public IReadOnlyList<CardModel> Selected { get; private set; } = [];
        public CardSelectionRequest? Request { get; private set; }
        public int? GoldAtRequest { get; private set; }

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Request = request;
            GoldAtRequest = request.Player.Gold;
            Selected = request.Candidates.Skip(1).Take(request.MaxCount).ToArray();
            return Task.FromResult(Selected);
        }
    }
}
