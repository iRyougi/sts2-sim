using Sts2Sim.Core.Combat;
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
                Assert.Equal(2, player.Deck.Cards.Count(card => card.IsUpgraded) - upgradesBefore);
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
                Assert.Equal(101, pricklyPlayer.Gold);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(eventName), eventName, null);
        }
    }

    private sealed class NonFirstCardsSelectionSource : ICardSelectionDecisionSource
    {
        public IReadOnlyList<CardModel> Selected { get; private set; } = [];

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Selected = request.Candidates.Skip(1).Take(request.MaxCount).ToArray();
            return Task.FromResult(Selected);
        }
    }
}
