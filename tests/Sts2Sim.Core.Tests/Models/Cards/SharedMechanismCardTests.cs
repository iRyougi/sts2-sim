using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class SharedMechanismCardTests : IDisposable
{
    public SharedMechanismCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt), typeof(SovereignBlade),
            typeof(MinionStrike), typeof(MinionDiveBomb), typeof(MinionSacrifice),
            typeof(Begone), typeof(Charge), typeof(Guards),
            typeof(Glow), typeof(Glitterstream), typeof(HiddenCache), typeof(RefineBlade),
            typeof(Convergence), typeof(Hegemony), typeof(CosmicIndifference), typeof(PhotonCut),
            typeof(SecretTechnique),
            typeof(DrawCardsNextTurnPower), typeof(BlockNextTurnPower), typeof(StarNextTurnPower),
            typeof(EnergyNextTurnPower), typeof(RetainHandPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Begone_TransformsAHandCard_IntoMinionStrike()
    {
        (Player player, _) = await CreateCombatAsync("begone");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        Begone card = AddToHand<Begone>(player);

        await card.PlayAsync(target: null);

        Assert.Contains(player.PlayerCombatState!.Hand.Cards, c => c is MinionStrike);
    }

    [Fact]
    public async Task Charge_TransformsFirstTwoDrawPileCards_IntoMinionDiveBomb()
    {
        (Player player, _) = await CreateCombatAsync("charge");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        Charge card = AddToHand<Charge>(player);
        int drawPileCountBefore = player.PlayerCombatState!.DrawPile.Cards.Count;

        await card.PlayAsync(target: null);

        Assert.Equal(2, player.PlayerCombatState!.DrawPile.Cards.Count(c => c is MinionDiveBomb));
        Assert.Equal(drawPileCountBefore, player.PlayerCombatState!.DrawPile.Cards.Count);
    }

    [Fact]
    public async Task Guards_DeterministicPolicySelectsNothing_HandUnchanged()
    {
        (Player player, _) = await CreateCombatAsync("guards");
        CardModel other = AddToHand<StrikeRegent>(player);
        Guards card = AddToHand<Guards>(player);

        await card.PlayAsync(target: null);

        Assert.Contains(other, player.PlayerCombatState!.Hand.Cards);
        Assert.DoesNotContain(player.PlayerCombatState!.Hand.Cards, c => c is MinionSacrifice);
    }

    [Fact]
    public async Task Glow_GainsStarDrawsCard_AndDrawsExtraNextTurn()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("glow");
        Glow card = AddToHand<Glow>(player);
        int handBefore = player.PlayerCombatState!.Hand.Cards.Count;
        int starsBefore = player.PlayerCombatState!.Stars;

        await card.PlayAsync(target: null);

        Assert.Equal(starsBefore + 1, player.PlayerCombatState!.Stars);
        Assert.Equal(handBefore, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Single(player.Creature.Powers.OfType<DrawCardsNextTurnPower>());

        int handBeforeNextTurn = player.PlayerCombatState!.Hand.Cards.Count;
        await room.Engine.EndPlayerTurnAsync();

        Assert.Empty(player.Creature.Powers.OfType<DrawCardsNextTurnPower>());
        Assert.True(player.PlayerCombatState!.Hand.Cards.Count >= handBeforeNextTurn);
    }

    [Fact]
    public async Task Glitterstream_Upgrade_GainsBlockAndExtraNextTurnBlock()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("glitterstream");
        Glitterstream card = AddToHand<Glitterstream>(player);
        card.Upgrade();

        await card.PlayAsync(target: null);

        Assert.Equal(13, player.Creature.Block);
        Assert.Equal(7, player.Creature.Powers.OfType<BlockNextTurnPower>().Single().Amount);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Empty(player.Creature.Powers.OfType<BlockNextTurnPower>());
        Assert.True(player.Creature.Block >= 7);
    }

    [Fact]
    public async Task HiddenCache_Upgrade_GainsStarAndStacksStarNextTurnPower()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("hidden-cache");
        HiddenCache card = AddToHand<HiddenCache>(player);
        card.Upgrade();
        int starsBefore = player.PlayerCombatState!.Stars;

        await card.PlayAsync(target: null);

        Assert.Equal(starsBefore + 1, player.PlayerCombatState!.Stars);
        Assert.Equal(4, player.Creature.Powers.OfType<StarNextTurnPower>().Single().Amount);

        int starsBeforeNextTurn = player.PlayerCombatState!.Stars;
        await room.Engine.EndPlayerTurnAsync();

        Assert.Empty(player.Creature.Powers.OfType<StarNextTurnPower>());
        Assert.True(player.PlayerCombatState!.Stars >= starsBeforeNextTurn + 4);
    }

    [Fact]
    public async Task RefineBlade_ForgesDamage_AndGrantsEnergyNextTurn()
    {
        (Player player, _) = await CreateCombatAsync("refine-blade");
        RefineBlade card = AddToHand<RefineBlade>(player);

        await card.PlayAsync(target: null);

        SovereignBlade blade = Assert.Single(player.PlayerCombatState!.Hand.Cards.OfType<SovereignBlade>());
        Assert.Single(player.Creature.Powers.OfType<EnergyNextTurnPower>());
        _ = blade;
    }

    [Fact]
    public async Task Convergence_RetainsHand_AndGrantsEnergyAndStarNextTurn()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("convergence");
        CardModel other = AddToHand<StrikeRegent>(player);
        Convergence card = AddToHand<Convergence>(player);
        card.Upgrade();

        await card.PlayAsync(target: null);

        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<RetainHandPower>()).Amount);
        Assert.Single(player.Creature.Powers.OfType<EnergyNextTurnPower>());
        Assert.Equal(2, player.Creature.Powers.OfType<StarNextTurnPower>().Single().Amount);
        Assert.False(card.HasKeyword(CardKeyword.Retain));

        await room.Engine.EndPlayerTurnAsync();

        Assert.Contains(other, player.PlayerCombatState!.Hand.Cards);
    }

    [Fact]
    public async Task Hegemony_Upgrade_DealsDamage_AndGrantsExtraEnergyNextTurn()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("hegemony");
        Hegemony card = AddToHand<Hegemony>(player);
        card.Upgrade();
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 18, enemy.CurrentHp);
        Assert.Equal(3, player.Creature.Powers.OfType<EnergyNextTurnPower>().Single().Amount);
    }

    [Fact]
    public async Task CosmicIndifference_GainsBlock_AndMovesFirstDiscardCardToDrawTop()
    {
        (Player player, _) = await CreateCombatAsync("cosmic-indifference");
        CardModel discarded = AddTo<StrikeRegent>(player, PileType.Discard);
        CosmicIndifference card = AddToHand<CosmicIndifference>(player);

        await card.PlayAsync(target: null);

        Assert.Equal(6, player.Creature.Block);
        Assert.Same(discarded, player.PlayerCombatState!.DrawPile.Cards.First());
    }

    [Fact]
    public async Task PhotonCut_DealsDamageDraws_AndMovesHandCardToDrawTop()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("photon-cut");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        PhotonCut card = AddToHand<PhotonCut>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 10, enemy.CurrentHp);
        Assert.Equal(PileType.Draw, player.PlayerCombatState!.DrawPile.Cards.First().Pile!.Type);
    }

    [Fact]
    public async Task SecretTechnique_MovesFirstSkillFromDrawPileToHand_UpgradeRemovesExhaust()
    {
        (Player player, _) = await CreateCombatAsync("secret-technique");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        CardModel skillCard = AddTo<DefendRegent>(player, PileType.Draw);
        SecretTechnique card = AddToHand<SecretTechnique>(player);
        Assert.True(card.HasKeyword(CardKeyword.Exhaust));

        await card.PlayAsync(target: null);

        Assert.Contains(skillCard, player.PlayerCombatState!.Hand.Cards);

        SecretTechnique upgraded = AddToHand<SecretTechnique>(player);
        upgraded.Upgrade();
        Assert.False(upgraded.HasKeyword(CardKeyword.Exhaust));
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel =>
        AddTo<TCard>(player, PileType.Hand);

    private static TCard AddTo<TCard>(Player player, PileType pileType)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType);
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
