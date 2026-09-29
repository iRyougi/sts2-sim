using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using CloneEnchantment = Sts2Sim.Core.Models.Enchantments.Clone;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class HiveFidelityRound2ContentTests : IDisposable
{
    public HiveFidelityRound2ContentTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RadiantPearl_FirstTurnGeneratesLuminesceWithExactPlayLifecycle()
    {
        (RunState run, Player player) = CreateRun("radiant-pearl");
        await RelicCmd.Obtain(ModelDb.Relic<RadiantPearl>(), player);

        CombatRoom room = await EnterCombat(run);
        Luminesce card = Assert.Single(player.PlayerCombatState!.Hand.Cards.OfType<Luminesce>());
        Assert.Equal(0, card.EnergyCost);
        Assert.True(card.HasKeyword(CardKeyword.Retain));
        Assert.True(card.HasKeyword(CardKeyword.Exhaust));
        player.PlayerCombatState.Energy = 0;

        await room.Engine.PlayCardAsync(player, card, null);

        Assert.Equal(2, player.PlayerCombatState.Energy);
        Assert.Contains(card, player.PlayerCombatState.ExhaustPile.Cards);
    }

    [Fact]
    public async Task ArchaicTooth_TransformsRegentStarterIntoMeteorShower()
    {
        (RunState run, Player player) = CreateRun("archaic-regent");
        FallingStar original = player.Deck.Cards.OfType<FallingStar>().Single();
        original.Upgrade();
        await CardCmd.Enchant<PerfectFit>(original, 2m);
        int count = player.Deck.Cards.Count;

        await RelicCmd.Obtain(ModelDb.Relic<ArchaicTooth>(), player);

        Assert.Equal(count, player.Deck.Cards.Count);
        Assert.Empty(player.Deck.Cards.OfType<FallingStar>());
        MeteorShower replacement = Assert.Single(player.Deck.Cards.OfType<MeteorShower>());
        Assert.True(replacement.IsUpgraded);
        PerfectFit enchantment = Assert.IsType<PerfectFit>(Assert.Single(replacement.Enchantments));
        Assert.Equal(2m, enchantment.Magnitude);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArchaicTooth_TransformsSilentOrIroncladStarter(bool ironclad)
    {
        var run = new RunState(ironclad ? "archaic-ironclad" : "archaic-silent", new Overgrowth());
        Player player = Player.CreateForNewRun(ironclad
            ? (CharacterModel)ModelDb.Character<Ironclad>()
            : ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        CardModel original = Assert.Single(player.Deck.Cards, card => ironclad
            ? card is Bash
            : card is Neutralize);
        if (ironclad)
        {
            var dustyTome = (DustyTome)ModelDb.Relic<DustyTome>().MutableClone();
            dustyTome.SetupForPlayer(player);
            Assert.Equal(ModelDb.Card<Corruption>().Id, dustyTome.AncientCard);
        }

        await RelicCmd.Obtain(ModelDb.Relic<ArchaicTooth>(), player);

        Assert.DoesNotContain(original, player.Deck.Cards);
        if (ironclad)
            Assert.Single(player.Deck.Cards.OfType<Break>());
        else
            Assert.Single(player.Deck.Cards.OfType<Suppress>());
    }

    [Theory]
    [InlineData(false, 16, 2)]
    [InlineData(true, 18, 3)]
    public async Task Relax_GrantsBlockAndNextTurnResourcesThenExhausts(
        bool upgraded, int block, int nextTurnAmount)
    {
        (RunState run, Player player) = CreateRun($"relax-{upgraded}");
        CombatRoom room = await EnterCombat(run);
        ClearCombatPiles(player);
        Relax card = AddToHand<Relax>(player, upgraded);
        player.PlayerCombatState!.Energy = 99;

        await room.Engine.PlayCardAsync(player, card, null);

        Assert.Equal(block, player.Creature.Block);
        Assert.Equal(nextTurnAmount, Assert.Single(player.Creature.Powers.OfType<DrawCardsNextTurnPower>()).Amount);
        Assert.Equal(nextTurnAmount, Assert.Single(player.Creature.Powers.OfType<EnergyNextTurnPower>()).Amount);
        Assert.Contains(card, player.PlayerCombatState.ExhaustPile.Cards);
    }

    [Fact]
    public async Task PaelsHorn_AddsExactlyTwoRelaxCardsOnPickup()
    {
        (RunState run, Player player) = CreateRun("paels-horn");
        int count = player.Deck.Cards.Count;

        await RelicCmd.Obtain(ModelDb.Relic<PaelsHorn>(), player);

        Assert.Equal(count + 2, player.Deck.Cards.Count);
        Assert.Equal(2, player.Deck.Cards.OfType<Relax>().Count());
    }

    [Fact]
    public async Task PaelsEye_ExhaustsHandTakesOneExtraTurnAndResetsNextCombat()
    {
        (RunState run, Player player) = CreateRun("paels-eye");
        await RelicCmd.Obtain(ModelDb.Relic<PaelsEye>(), player);
        CombatRoom first = await EnterCombat(run);
        int initialHand = player.PlayerCombatState!.Hand.Cards.Count;

        await first.Engine.EndPlayerTurnAsync();

        Assert.Equal(2, player.PlayerCombatState.TurnNumber);
        Assert.True(player.PlayerCombatState.ExhaustPile.Cards.Count >= initialHand);
        Assert.False(Hook.ShouldTakeExtraTurn(first.Engine.State, player));
        await first.Exit(run);

        CombatRoom second = await EnterCombat(run);
        await second.Engine.EndPlayerTurnAsync();

        Assert.Equal(2, player.PlayerCombatState!.TurnNumber);
    }

    [Fact]
    public async Task PaelsEye_AutoplayDoesNotCountAsManualAndStillAllowsExtraTurn()
    {
        (RunState run, Player player) = CreateRun("paels-eye-autoplay");
        await RelicCmd.Obtain(ModelDb.Relic<PaelsEye>(), player);
        CombatRoom room = await EnterCombat(run);
        ClearCombatPiles(player);
        DefendRegent card = AddToHand<DefendRegent>(player);

        await AutoPlayCmd.FromCards(room.Engine.State, player, [card]);
        DefendRegent marker = AddToHand<DefendRegent>(player);
        await room.Engine.EndPlayerTurnAsync();

        Assert.Contains(marker, player.PlayerCombatState!.ExhaustPile.Cards);
    }

    [Fact]
    public async Task PaelsEye_ManualPlayPreventsExtraTurn()
    {
        (RunState run, Player player) = CreateRun("paels-eye-manual");
        await RelicCmd.Obtain(ModelDb.Relic<PaelsEye>(), player);
        CombatRoom room = await EnterCombat(run);
        ClearCombatPiles(player);
        DefendRegent card = AddToHand<DefendRegent>(player);
        player.PlayerCombatState!.Energy = 99;

        await room.Engine.PlayCardAsync(player, card, null);
        DefendRegent marker = AddToHand<DefendRegent>(player);
        Assert.Equal(1, player.PlayerCombatState.ManualCardsPlayedThisTurn);

        await Hook.BeforeSideTurnEndEarly(
            room.Engine.State, CombatSide.Player, [player.Creature]);

        Assert.Contains(marker, player.PlayerCombatState.Hand.Cards);
        Assert.False(Hook.ShouldTakeExtraTurn(room.Engine.State, player));
    }

    [Fact]
    public async Task PaelsEye_DoesNotTriggerWhenOwnerMissedPreviousPlayerTurn()
    {
        (RunState run, Player player) = CreateRun("paels-eye-participant");
        await RelicCmd.Obtain(ModelDb.Relic<PaelsEye>(), player);
        CombatRoom room = await EnterCombat(run);
        ClearCombatPiles(player);
        DefendRegent marker = AddToHand<DefendRegent>(player);

        await Hook.AfterSideTurnStart(room.Engine.State, CombatSide.Player, Array.Empty<Creature>());
        await Hook.BeforeSideTurnEndEarly(
            room.Engine.State, CombatSide.Player, [player.Creature]);

        Assert.Contains(marker, player.PlayerCombatState!.Hand.Cards);
        Assert.False(Hook.ShouldTakeExtraTurn(room.Engine.State, player));
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public async Task BrightestFlame_GainsResourcesDrawsAndLosesMaxHp(bool upgraded, int amount)
    {
        (RunState run, Player player) = CreateRun($"brightest-{upgraded}");
        CombatRoom room = await EnterCombat(run);
        ClearCombatPiles(player);
        BrightestFlame card = AddToHand<BrightestFlame>(player, upgraded);
        for (int i = 0; i < amount; i++)
            AddToPile<DefendRegent>(player, PileType.Draw);
        player.PlayerCombatState!.Energy = 0;
        int maxHp = player.Creature.MaxHp;

        await room.Engine.PlayCardAsync(player, card, null);

        Assert.Equal(amount, player.PlayerCombatState.Energy);
        Assert.Equal(amount, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Equal(maxHp - 2, player.Creature.MaxHp);
        Assert.False(card.HasKeyword(CardKeyword.Exhaust));
        Assert.Contains(card, player.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public async Task Storybook_AddsExactlyOneBrightestFlameOnPickup()
    {
        (RunState run, Player player) = CreateRun("storybook");
        int count = player.Deck.Cards.Count;

        await RelicCmd.Obtain(ModelDb.Relic<Storybook>(), player);

        Assert.Equal(count + 1, player.Deck.Cards.Count);
        Assert.Single(player.Deck.Cards.OfType<BrightestFlame>());
    }

    [Fact]
    public async Task PaelsGrowth_EnchantsExactlyOneEligibleDeckCardWithFourClone()
    {
        (RunState run, Player player) = CreateRun("paels-growth");

        await RelicCmd.Obtain(ModelDb.Relic<PaelsGrowth>(), player);

        EnchantmentModel[] enchantments = player.Deck.Cards.SelectMany(card => card.Enchantments).ToArray();
        CloneEnchantment clone = Assert.IsType<CloneEnchantment>(Assert.Single(enchantments));
        Assert.Equal(4m, clone.Magnitude);
        Assert.True(clone.CanEnchant(player.Deck.Cards.First(card => card.Enchantments.Count == 0)));
    }

    private static (RunState Run, Player Player) CreateRun(string seed)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        return (run, player);
    }

    private static async Task<CombatRoom> EnterCombat(RunState run)
    {
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        return room;
    }

    private static T AddToHand<T>(Player player, bool upgraded = false) where T : CardModel =>
        AddToPile<T>(player, PileType.Hand, upgraded);

    private static T AddToPile<T>(Player player, PileType pile, bool upgraded = false) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        if (upgraded) card.Upgrade();
        CardPileCmd.Add(card, pile);
        return card;
    }

    private static void ClearCombatPiles(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).ToList())
            CardPileCmd.Remove(card);
    }
}
