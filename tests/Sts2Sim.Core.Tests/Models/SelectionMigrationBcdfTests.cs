using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class SelectionMigrationBcdfTests : IDisposable
{
    public SelectionMigrationBcdfTests() { ModelDb.ResetForTests(); ModelDb.Init(ContentRegistry.AllTypes); }
    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("Discovery")]
    [InlineData("Quasar")]
    [InlineData("Splash")]
    [InlineData("AttackPotion")]
    [InlineData("Toolbox")]
    public async Task GeneratedChoices_SelectNonFirstInstance_AndKeepUnselectedOutOfPiles(string effect)
    {
        foreach (bool skip in effect == "Toolbox" ? new[] { false } : new[] { false, true })
        {
            var (_, player, state) = CreateState("migration-b-" + effect + skip);
            var source = new ChoiceSource(request => skip ? Array.Empty<CardModel>() : new[] { request.Candidates[1] });
            state.CardSelectionSource = source;
            PotionModel? usedPotion = null;
            if (effect == "Toolbox")
            {
                var relic = (Toolbox)ModelDb.Relic<Toolbox>().MutableClone(); relic.AssignOwner(player);
                await relic.BeforeHandDraw(player);
            }
            else if (effect == "AttackPotion")
            {
                var potion = (AttackPotion)ModelDb.Potion<AttackPotion>().MutableClone(); potion.AssignOwner(player); usedPotion = potion;
                await potion.UseInternal(player.Creature);
            }
            else
            {
                CardModel card = effect switch { "Discovery" => ModelDb.Card<Discovery>(), "Quasar" => ModelDb.Card<Quasar>(), _ => ModelDb.Card<Splash>() };
                card = (CardModel)card.MutableClone(); card.AssignOwner(player); CardPileCmd.Add(card, PileType.Hand);
                await card.AutoPlayAsync(null);
            }
            CardSelectionRequest request = Assert.Single(source.Requests);
            if (usedPotion is not null) Assert.Same(usedPotion, request.Source);
            Assert.Equal(effect == "Toolbox" ? 1 : 0, request.MinCount);
            Assert.Equal(1, request.MaxCount); Assert.Equal(effect != "Toolbox", request.Cancelable);
            Assert.Equal(3, request.Candidates.Count);
            Assert.Equal(3, request.Candidates.Distinct(ReferenceEqualityComparer.Instance).Count());
            foreach (CardModel candidate in request.Candidates)
            {
                Assert.Same(player, candidate.Owner);
                if (!skip && ReferenceEquals(candidate, request.Candidates[1])) Assert.Contains(candidate, player.PlayerCombatState!.Hand.Cards);
                else Assert.Null(candidate.Pile);
            }
        }
    }

    [Theory]
    [InlineData("DollysMirror")]
    [InlineData("PaelsGrowth")]
    public async Task PickupChoice_UsesRunSource_AndChangesSelectedDeckInstance(string effect)
    {
        var (run, player, _) = CreateState("migration-c-" + effect);
        CardModel[] before = player.Deck.Cards.ToArray();
        CardModel[] expectedCandidates = (effect == "DollysMirror" ? before.Where(c => c.Type != CardType.Quest) : before.Where(ModelDb.GetById<Clone>(ModelDb.GetId<Clone>()).CanEnchant)).ToArray();
        var source = new ChoiceSource(request => new[] { request.Candidates[1] }); run.ConfigureCardSelectionSource(source);
        if (effect == "DollysMirror") await RelicCmd.Obtain(ModelDb.Relic<DollysMirror>(), player);
        else await RelicCmd.Obtain(ModelDb.Relic<PaelsGrowth>(), player);
        CardSelectionRequest request = Assert.Single(source.Requests);
        Assert.Equal(1, request.MinCount); Assert.Equal(1, request.MaxCount); Assert.False(request.Cancelable);
        Assert.Same(player, request.Player);
        Assert.Same(player.Relics.Single(r => r.GetType().Name == effect), request.Source);
        Assert.Equal(expectedCandidates, request.Candidates);
        CardModel selected = request.Candidates[1];
        if (effect == "DollysMirror")
        {
            CardModel copy = Assert.Single(player.Deck.Cards.Except(before)); Assert.Equal(selected.Id, copy.Id); Assert.NotSame(selected, copy);
        }
        else { Assert.Single(selected.Enchantments); Assert.Empty(request.Candidates[0].Enchantments); }
    }

    [Fact]
    public async Task GamblingEffects_DiscardAndDrawExactlyChosenZeroPartialOrWholeHand()
    {
        foreach (bool relicEffect in new[] { false, true })
        foreach (int count in new[] { 0, 1, 3 })
        {
            var (_, player, state) = CreateState("migration-d-" + relicEffect + count);
            var hand = Enumerable.Range(0, 3).Select(_ => Add<StrikeRegent>(player, PileType.Hand)).ToArray();
            var draw = Enumerable.Range(0, 3).Select(_ => Add<DefendRegent>(player, PileType.Draw)).ToArray();
            var source = new ChoiceSource(request => request.Candidates.TakeLast(count).ToArray()); state.CardSelectionSource = source;
            if (relicEffect)
            {
                var relic = (GamblingChip)ModelDb.Relic<GamblingChip>().MutableClone(); relic.AssignOwner(player);
                await relic.AfterSideTurnStart(CombatSide.Player, new[] { player.Creature });
            }
            else
            {
                var potion = (GamblersBrew)ModelDb.Potion<GamblersBrew>().MutableClone(); potion.AssignOwner(player);
                player.AddPotionInternal(potion);
                await PotionCmd.Use(potion, player, player.Creature);
                Assert.DoesNotContain(potion, player.PotionSlots);
            }
            CardSelectionRequest request = Assert.Single(source.Requests);
            Assert.Equal(0, request.MinCount); Assert.Equal(3, request.MaxCount); Assert.False(request.Cancelable);
            Assert.Equal(3, player.PlayerCombatState!.Hand.Cards.Count);
            Assert.Equal(count, hand.Count(card => card.Pile!.Type == PileType.Discard));
            Assert.Equal(count, draw.Count(card => card.Pile!.Type == PileType.Hand));
        }
        var run = new RunState("migration-d-room", new Overgrowth());
        var owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), run); run.AddPlayer(owner);
        await RelicCmd.Obtain(ModelDb.Relic<GamblingChip>(), owner);
        CardModel[]? openingHand = null;
        var roomSource = new ChoiceSource(request => { openingHand = request.Candidates.ToArray(); return new[] { request.Candidates[^1] }; });
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        room.ConfigureCardSelectionSource(roomSource);
        await room.Enter(run);
        Assert.Single(roomSource.Requests);
        Assert.NotNull(openingHand);
        Assert.Equal(PileType.Discard, openingHand![^1].Pile!.Type);
        Assert.Equal(owner.PlayerCombatState!.Hand.Cards.Count, openingHand.Length);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RandomUpgrade_UsesNicheAndOnlyUpgradableMatchingCards(bool skills)
    {
        var (run, player, _) = CreateState("migration-f-" + skills);
        foreach (CardModel card in player.Deck.Cards.ToArray()) CardPileCmd.Remove(card);
        for (int i = 0; i < 5; i++) { Add<StrikeRegent>(player, PileType.Deck); Add<DefendRegent>(player, PileType.Deck); }
        var alreadyUpgraded = skills ? (CardModel)Add<DefendRegent>(player, PileType.Deck) : Add<StrikeRegent>(player, PileType.Deck);
        alreadyUpgraded.Upgrade();
        var type = skills ? CardType.Skill : CardType.Attack;
        var eligible = player.Deck.Cards.Where(c => c.Type == type && c.IsUpgradable).ToList();
        var expectedRng = run.Rng.Niche.CloneExact();
        var expected = eligible.ToList().StableShuffle(expectedRng).Take(2).ToArray();
        int rewardsBefore = run.Rng.CombatCardGeneration.Counter, selectionBefore = run.Rng.CombatCardSelection.Counter;
        var source = new ChoiceSource(_ => throw new InvalidOperationException("Random upgrades must not ask a player."));
        run.ConfigureCardSelectionSource(source);
        if (skills) await RelicCmd.Obtain(ModelDb.Relic<WarPaint>(), player);
        else await RelicCmd.Obtain(ModelDb.Relic<Whetstone>(), player);
        Assert.True(expected.ToHashSet().SetEquals(eligible.Where(c => c.IsUpgraded)));
        Assert.Equal(expectedRng.Counter, run.Rng.Niche.Counter);
        Assert.Equal(rewardsBefore, run.Rng.CombatCardGeneration.Counter); Assert.Equal(selectionBefore, run.Rng.CombatCardSelection.Counter);
        Assert.Empty(source.Requests);
        Assert.All(player.Deck.Cards.Where(c => c.Type != type), card => Assert.False(card.IsUpgraded));
    }
    private static (RunState, Player, CombatState) CreateState(string seed)
    {
        var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run); run.AddPlayer(player);
        player.ResetCombatState(); player.PlayerCombatState!.TurnNumber = 1;
        var state = new CombatState(run); state.AddPlayerCreature(player.Creature); return (run, player, state);
    }
    private static T Add<T>(Player player, PileType pile) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone(); card.AssignOwner(player); CardPileCmd.Add(card, pile); return card;
    }
    private sealed class ChoiceSource(Func<CardSelectionRequest, IReadOnlyList<CardModel>> choose) : ICardSelectionDecisionSource
    {
        public List<CardSelectionRequest> Requests { get; } = new();
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) { Requests.Add(request); return Task.FromResult(choose(request)); }
    }
}
