using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class HiveContentBehaviorTests : IDisposable
{
    public HiveContentBehaviorTests() { ModelDb.ResetForTests(); ModelDb.Init(ContentRegistry.AllTypes); }
    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 5)]
    public async Task Metamorphosis_GeneratesFreeAttacksIntoDraw(bool upgraded, int count)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"metamorphosis-{upgraded}");
        Metamorphosis card = AddToHand<Metamorphosis>(player, upgraded);
        int rng = player.RunState.Rng.CombatCardGeneration.Counter;
        await room.Engine.PlayCardAsync(player, card, null);
        Assert.Equal(count, player.PlayerCombatState!.DrawPile.Cards.Count);
        Assert.All(player.PlayerCombatState.DrawPile.Cards, generated =>
        {
            Assert.Equal(CardType.Attack, generated.Type);
            Assert.Equal(0, generated.EnergyCost);
            Assert.Null(generated.TemporaryCostOverrideThisCombat);
            Assert.True(generated.TemporaryFreeThisCombat);
        });
        Assert.Equal(count, player.RunState.Rng.CombatCardGeneration.Counter - rng);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public async Task Enlightenment_ReducesOnlyExpensiveCardsWithCorrectDuration(
        bool upgraded, bool thisTurn, bool thisCombat)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"enlightenment-{upgraded}");
        Enlightenment enlightenment = AddToHand<Enlightenment>(player, upgraded);
        Sneaky expensive = AddToHand<Sneaky>(player);
        Slice cheap = AddToHand<Slice>(player);
        await room.Engine.PlayCardAsync(player, enlightenment, null);
        Assert.Equal(1, expensive.EnergyCost);
        Assert.Null(expensive.TemporaryCostOverrideThisTurn);
        Assert.Equal(thisTurn ? 1 : null, expensive.TemporaryCostOverrideThisTurnOrUntilPlayed);
        Assert.Equal(thisCombat ? 1 : null, expensive.TemporaryCostOverrideThisCombat);
        Assert.Null(cheap.TemporaryCostOverrideThisTurn);
        Assert.Null(cheap.TemporaryCostOverrideThisTurnOrUntilPlayed);
        Assert.Null(cheap.TemporaryCostOverrideThisCombat);
    }

    [Fact]
    public async Task PerfectFit_MovesItsCardToTopOnlyOnRealReshuffle()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("perfect-fit-shuffle");
        StrikeRegent fit = NewCard<StrikeRegent>(player);
        await CardCmd.Enchant<PerfectFit>(fit, 1m);
        CardPileCmd.Add(fit, PileType.Discard);
        CardPileCmd.Add(NewCard<DefendRegent>(player), PileType.Discard);
        await CardPileCmd.Shuffle(room.Engine.State, player);
        Assert.Same(fit, player.PlayerCombatState!.DrawPile.Cards[0]);
    }

    [Fact]
    public async Task BugslayerCards_ResolveDamageAndDebuffInCombat()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("bugslayer-cards", 2);
        Creature[] enemies = room.Engine.State.HittableEnemies.ToArray();
        int[] hp = enemies.Select(e => e.CurrentHp).ToArray();
        await room.Engine.PlayCardAsync(player, AddToHand<Exterminate>(player), null);
        Assert.Equal(hp.Select(value => value - 12), enemies.Select(e => e.CurrentHp));
        Creature target = enemies[0];
        int before = target.CurrentHp;
        await room.Engine.PlayCardAsync(player, AddToHand<Squash>(player), target);
        Assert.Equal(before - 10, target.CurrentHp);
        Assert.Contains(target.Powers, power => power.GetType().Name == "VulnerablePower" && power.Amount == 2m);
    }

    [Fact]
    public async Task LostWisp_RelicDealsEightToEveryEnemyAfterOwnerPower()
    {
        (RunState run, Player player) = CreateRun("lost-wisp-relic");
        await RelicCmd.Obtain(ModelDb.Relic<Sts2Sim.Core.Models.Relics.LostWisp>(), player);
        CombatRoom room = await EnterCombat(run, 2);
        Creature[] enemies = room.Engine.State.HittableEnemies.ToArray();
        int[] hp = enemies.Select(e => e.CurrentHp).ToArray();
        await room.Engine.PlayCardAsync(player, AddToHand<Calamity>(player), null);
        Assert.Equal(hp.Select(value => value - 8), enemies.Select(e => e.CurrentHp));
    }

    [Fact]
    public async Task SandCastle_UsesNicheAndUpgradesExactlySix()
    {
        (RunState run, Player player) = CreateRun("sand-castle");
        int eligible = player.Deck.Cards.Count(card => card.IsUpgradable);
        int rng = run.Rng.Niche.Counter;
        await RelicCmd.Obtain(ModelDb.Relic<SandCastle>(), player);
        Assert.Equal(6, player.Deck.Cards.Count(card => card.IsUpgraded));
        Assert.Equal(eligible - 1, run.Rng.Niche.Counter - rng);
        Assert.Equal((2, 3, 1, 0), (
            player.Deck.Cards.Count(card => card is StrikeRegent && card.IsUpgraded),
            player.Deck.Cards.Count(card => card is DefendRegent && card.IsUpgraded),
            player.Deck.Cards.Count(card => card is FallingStar && card.IsUpgraded),
            player.Deck.Cards.Count(card => card is Venerate && card.IsUpgraded)));
        Assert.True(ModelDb.Relic<SandCastle>().HasUponPickupEffect);
    }

    [Fact]
    public async Task EnergyAndDrawAncients_ApplyTheirExactAmounts()
    {
        (RunState run, Player player) = CreateRun("hive-energy-draw");
        await RelicCmd.Obtain(ModelDb.Relic<PrismaticGem>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<PaelsBlood>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<VeryHotCocoa>(), player);
        CombatRoom room = await EnterCombat(run);
        Assert.Equal(player.MaxEnergy + 1 + 4, player.PlayerCombatState!.Energy);
        Assert.Equal(6m, Hook.ModifyHandDraw(room.Engine.State, player, 5m));
    }


    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed, int enemies = 1)
    {
        (RunState run, Player player) = CreateRun(seed);
        CombatRoom room = await EnterCombat(run, enemies);
        foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
            foreach (CardModel card in pile.Cards.ToArray()) CardPileCmd.Remove(card);
        player.PlayerCombatState.Energy = 99;
        return (player, room);
    }

    private static async Task<CombatRoom> EnterCombat(RunState run, int enemies = 1)
    {
        var room = new CombatRoom(() => Enumerable.Range(0, enemies)
            .Select(_ => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone()).ToArray());
        await room.Enter(run);
        return room;
    }

    private static (RunState Run, Player Player) CreateRun(string seed)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        return (run, player);
    }

    private static T AddToHand<T>(Player player, bool upgraded = false) where T : CardModel
    {
        T card = NewCard<T>(player);
        if (upgraded) card.Upgrade();
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static T NewCard<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        return card;
    }
}
