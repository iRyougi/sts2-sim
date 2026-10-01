using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class CardGenerationCardTests : IDisposable
{
    public CardGenerationCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt),
            typeof(Debris), typeof(CollisionCourse), typeof(CrashLanding),
            typeof(Quasar), typeof(BundleOfJoy), typeof(Discovery), typeof(Splash), typeof(Jackpot),
            typeof(HeirloomHammer),
            typeof(JackOfAllTrades), typeof(SecretTechnique), typeof(Automation), typeof(Fasten), typeof(Coordinate),
            typeof(Abundance), typeof(Exterminate), typeof(Caltrops), typeof(Shiv),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task CollisionCourse_DealsDamage_AndGeneratesDebrisIntoHand()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("collision-course");
        CollisionCourse card = AddToHand<CollisionCourse>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 10, enemy.CurrentHp);
        Assert.Contains(player.PlayerCombatState!.Hand.Cards, c => c is Debris);
    }

    [Fact]
    public async Task CrashLanding_Upgrade_DealsDamageToAll_AndFillsHandWithDebris()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("crash-landing");
        CrashLanding card = AddToHand<CrashLanding>(player);
        card.Upgrade();
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(target: null);

        Assert.Equal(hpBefore - 26, enemy.CurrentHp);
        Assert.Equal(CardPile.MaxCardsInHand, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Contains(player.PlayerCombatState!.Hand.Cards, c => c is Debris);
    }

    [Fact]
    public async Task Quasar_GeneratesThreeColorlessCandidates_AndAddsFirstPickToHand()
    {
        (Player player, _) = await CreateCombatAsync("quasar");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        Quasar card = AddToHand<Quasar>(player);
        int handBefore = player.PlayerCombatState!.Hand.Cards.Count;
        int colorlessInHandBefore = player.PlayerCombatState!.Hand.Cards.Count(c => c.IsColorless);

        await card.PlayAsync(target: null);

        Assert.Equal(handBefore, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Equal(colorlessInHandBefore + 1, player.PlayerCombatState!.Hand.Cards.Count(c => c.IsColorless));
    }

    [Fact]
    public async Task BundleOfJoy_Upgrade_GeneratesFourDistinctColorlessCards()
    {
        (Player player, _) = await CreateCombatAsync("bundle-of-joy");
        BundleOfJoy card = AddToHand<BundleOfJoy>(player);
        card.Upgrade();
        int colorlessInHandBefore = player.PlayerCombatState!.Hand.Cards.Count(c => c.IsColorless);

        await card.PlayAsync(target: null);

        Assert.Equal(colorlessInHandBefore + 4, player.PlayerCombatState!.Hand.Cards.Count(c => c.IsColorless));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Discovery_GeneratesFromCharacterPool_UpgradeRemovesExhaust(bool upgraded)
    {
        (Player player, _) = await CreateCombatAsync("discovery");
        var selection = new PositiveCostSelectionObserver();
        ((CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        Discovery card = AddToHand<Discovery>(player);
        if (upgraded) card.Upgrade();
        Assert.Equal(!upgraded, card.HasKeyword(CardKeyword.Exhaust));

        await card.PlayAsync(target: null);

        CardModel selected = Assert.IsAssignableFrom<CardModel>(selection.Selected);
        Assert.True(selection.SelectedCostBefore > 0);
        Assert.Null(selection.SelectedOverrideBefore);
        Assert.Contains(selected, player.PlayerCombatState!.Hand.Cards);
        Assert.False(selected.IsColorless);
        Assert.IsNotType<Discovery>(selected);
        Assert.Equal(0, selected.LocalEnergyCost);
        Assert.Equal(0, selected.TemporaryCostOverrideThisTurnOrUntilPlayed);
        foreach (var offer in selection.Offers.Where(offer => offer.Card != selected))
        {
            Assert.Equal(offer.Cost, offer.Card.LocalEnergyCost);
            Assert.Equal(offer.UntilPlayed, offer.Card.TemporaryCostOverrideThisTurnOrUntilPlayed);
        }
    }

    private sealed class PositiveCostSelectionObserver : ICardSelectionDecisionSource
    {
        public CardModel? Selected { get; private set; }
        public int SelectedCostBefore { get; private set; }
        public int? SelectedOverrideBefore { get; private set; }
        public (CardModel Card, int Cost, int? UntilPlayed)[] Offers { get; private set; } = [];

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Offers = request.Candidates.Select(card =>
                (card, card.LocalEnergyCost, card.TemporaryCostOverrideThisTurnOrUntilPlayed)).ToArray();
            Selected = request.Candidates.First(card => card.LocalEnergyCost > 0);
            SelectedCostBefore = Selected.LocalEnergyCost;
            SelectedOverrideBefore = Selected.TemporaryCostOverrideThisTurnOrUntilPlayed;
            return Task.FromResult<IReadOnlyList<CardModel>>([Selected]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Splash_GeneratesAttackCard_FromCharacterPool(bool upgraded)
    {
        (Player player, _) = await CreateCombatAsync("splash");
        var selection = new SplashSelectionSource(upgraded);
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        Splash card = AddToHand<Splash>(player);
        if (upgraded) card.Upgrade();
        int attackCardsInHandBefore = player.PlayerCombatState!.Hand.Cards.Count(c => c.Type == CardType.Attack);

        await card.PlayAsync(target: null);

        Assert.Equal(attackCardsInHandBefore + 1, player.PlayerCombatState!.Hand.Cards.Count(c => c.Type == CardType.Attack));
        CrashLanding selected = Assert.IsType<CrashLanding>(selection.Selected);
        Assert.Same(selected, player.PlayerCombatState.Hand.Cards.Single(c => c is CrashLanding));
        Assert.Equal(0, selected.EnergyCost);
        Assert.Equal(0, selected.StarCost);
        Assert.Equal(0, selected.TemporaryCostOverrideThisTurnOrUntilPlayed);
        Assert.Equal(0, selected.TemporaryStarCostOverrideThisTurn);
        CollisionCourse unselected = Assert.IsType<CollisionCourse>(selection.Unselected);
        Assert.Null(unselected.TemporaryCostOverrideThisTurnOrUntilPlayed);
        Assert.Null(unselected.TemporaryStarCostOverrideThisTurn);
    }

    [Fact]
    public async Task Jackpot_Upgrade_DealsDamage_AndGeneratesThreeZeroCostCards()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("jackpot");
        Jackpot card = AddToHand<Jackpot>(player);
        card.Upgrade();
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;
        int zeroCostBefore = player.PlayerCombatState!.Hand.Cards.Count(c => c.EnergyCost == 0);

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 30, enemy.CurrentHp);
        Assert.Equal(zeroCostBefore + 3, player.PlayerCombatState!.Hand.Cards.Count(c => c.EnergyCost == 0));
    }

    [Theory]
    [InlineData(typeof(JackOfAllTrades), true)]
    [InlineData(typeof(Abundance), true)]
    [InlineData(typeof(Exterminate), true)]
    [InlineData(typeof(Shiv), true)]
    [InlineData(typeof(Caltrops), false)]
    public async Task HeirloomHammer_DealsDamage_AndClonesVisuallyColorlessHandCard(
        Type candidateType, bool shouldClone)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"heirloom-hammer-{candidateType.Name}");
        CardModel candidate = (CardModel)ModelDb.Get(candidateType).MutableClone();
        candidate.AssignOwner(player);
        CardPileCmd.Add(candidate, PileType.Hand);
        HeirloomHammer card = AddToHand<HeirloomHammer>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 20, enemy.CurrentHp);
        Assert.Equal(shouldClone ? 2 : 1,
            player.PlayerCombatState!.Hand.Cards.Count(c => c.GetType() == candidateType));
        Assert.Contains(candidate, player.PlayerCombatState!.Hand.Cards);
    }

    private sealed class SplashSelectionSource(bool upgraded) : Sts2Sim.Core.Combat.ICardSelectionDecisionSource
    {
        public CrashLanding? Selected { get; private set; }
        public CollisionCourse? Unselected { get; private set; }

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(Sts2Sim.Core.Combat.CardSelectionRequest request)
        {
            Selected = Assert.Single(request.Candidates.OfType<CrashLanding>());
            Unselected = Assert.Single(request.Candidates.OfType<CollisionCourse>());
            Assert.Equal(1, Selected.EnergyCost);
            Assert.All(request.Candidates, candidate =>
            {
                Assert.Equal(upgraded, candidate.IsUpgraded);
                Assert.Null(candidate.TemporaryCostOverrideThisTurnOrUntilPlayed);
                Assert.Null(candidate.TemporaryStarCostOverrideThisTurn);
            });
            return Task.FromResult<IReadOnlyList<CardModel>>([Selected]);
        }
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}
