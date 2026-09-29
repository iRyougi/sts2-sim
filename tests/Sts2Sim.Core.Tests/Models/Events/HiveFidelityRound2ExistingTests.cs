using Sts2Sim.Core.Combat;
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
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;
using PaelsLegion = Sts2Sim.Core.Models.Relics.PaelsLegion;

namespace Sts2Sim.Core.Tests.Models.Events;

file sealed class HiveRound2MultiBlockCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, 5m, ValueProp.Unpowered, this, cardPlay);
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, 5m, ValueProp.Move, this, cardPlay);
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, 5m, ValueProp.Move, this, cardPlay);
    }
}

[Collection("ModelDb")]
public sealed class HiveFidelityRound2ExistingTests : IDisposable
{
    public HiveFidelityRound2ExistingTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Append(typeof(HiveRound2MultiBlockCard)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task InfestedAutomaton_UsesBaseOddsWithoutMutatingEncounterPity()
    {
        (RunState run, Player player) = CreateRun("automaton-base-odds");
        player.Odds.CardRarity.OverrideCurrentValue(0.39f);
        InfestedAutomaton ev = Begin<InfestedAutomaton>(run, player);
        int rewardsBefore = player.PlayerRng.Rewards.Counter;

        await ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == "STUDY"));

        Assert.Equal(0.39f, player.Odds.CardRarity.CurrentValue);
        Assert.Equal(2, player.PlayerRng.Rewards.Counter - rewardsBefore);
    }

    [Fact]
    public void CardFactory_MissingCommonRarityFallsForwardToUncommonBeforeRare()
    {
        int checkedCommonRolls = 0;
        for (int seed = 0; seed < 100 && checkedCommonRolls < 12; seed++)
        {
            (RunState expectedRun, Player expectedPlayer) = CreateRun($"automaton-fallback-{seed}");
            CardRarity rolled = expectedPlayer.Odds.CardRarity.RollWithBaseOdds(CardRarityOddsType.RegularEncounter);
            if (rolled != CardRarity.Common)
                continue;

            (RunState actualRun, Player actualPlayer) = CreateRun($"automaton-fallback-{seed}");
            CardModel card = Assert.Single(CardFactory.CreateForReward(
                actualPlayer,
                1,
                actualPlayer.Character.CardPool,
                CardRarityOddsType.RegularEncounter,
                candidate => candidate.Type == CardType.Power,
                noUpgradeRoll: true));

            Assert.Equal(CardRarity.Uncommon, card.Rarity);
            Assert.Equal(-0.05f, actualPlayer.Odds.CardRarity.CurrentValue);
            checkedCommonRolls++;
        }

        Assert.Equal(12, checkedCommonRolls);
    }

    [Fact]
    public async Task MakeFreeThisCombat_MakesFallingStarFreeForEnergyAndStars()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("metamorphosis-falling-star");
        ClearCombatPiles(player);
        FallingStar star = AddToHand<FallingStar>(player);
        player.PlayerCombatState!.Energy = 0;

        star.MakeFreeThisCombat();

        Assert.Equal((0, 0), (star.EnergyCost, star.StarCost));
        Assert.True(star.CanPlay(out _));
        await room.Engine.PlayCardAsync(player, star, room.Engine.State.HittableEnemies[0]);
        Assert.Equal((0, 3), (player.PlayerCombatState.Energy, player.PlayerCombatState.Stars));
    }
    [Fact]
    public async Task Enlightenment_UnupgradedCostExpiresAfterFirstPlayEvenWhenReturnedToHandThisTurn()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("enlightenment-replay");
        ClearCombatPiles(player);
        player.PlayerCombatState!.Energy = 99;
        Enlightenment enlightenment = AddToHand<Enlightenment>(player);
        Prophesize expensive = AddToHand<Prophesize>(player);

        await room.Engine.PlayCardAsync(player, enlightenment, null);
        Assert.Equal(1, expensive.EnergyCost);
        await room.Engine.PlayCardAsync(player, expensive, null);
        CardPileCmd.Add(expensive, PileType.Hand);

        Assert.Equal(2, expensive.EnergyCost);
        int before = player.PlayerCombatState.Energy;
        await room.Engine.PlayCardAsync(player, expensive, null);
        Assert.Equal(before - 2, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task Enlightenment_UnupgradedCostExpiresAtTurnEndWhenUnplayed()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("enlightenment-turn-end");
        ClearCombatPiles(player);
        Enlightenment enlightenment = AddToHand<Enlightenment>(player);
        Prophesize expensive = AddToHand<Prophesize>(player);

        await room.Engine.PlayCardAsync(player, enlightenment, null);
        Assert.Equal(1, expensive.EnergyCost);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Null(expensive.TemporaryCostOverrideThisTurnOrUntilPlayed);
        Assert.Equal(2, expensive.EnergyCost);
    }

    [Fact]
    public async Task Goopy_CombatCopyGrowthPersistsToDeckAcrossConsecutiveCombats()
    {
        (RunState run, Player player) = CreateRun("goopy-persistence");
        DefendRegent deckCard = player.Deck.Cards.OfType<DefendRegent>().First();
        await CardCmd.Enchant<Sts2Sim.Core.Models.Enchantments.Goopy>(deckCard, 1m);

        for (int combat = 1; combat <= 2; combat++)
        {
            CombatRoom room = await EnterCombat(run);
            CardModel combatCard = player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards)
                .Single(card => card.GetType() == typeof(DefendRegent) &&
                                card.Enchantments.OfType<Sts2Sim.Core.Models.Enchantments.Goopy>().Any());
            CardPileCmd.Add(combatCard, PileType.Hand);
            player.PlayerCombatState.Energy = 99;

            await room.Engine.PlayCardAsync(player, combatCard, null);

            Assert.Equal(1m + combat,
                Assert.Single(deckCard.Enchantments.OfType<Sts2Sim.Core.Models.Enchantments.Goopy>()).Magnitude);
            await room.Exit(run);
        }
    }

    [Fact]
    public async Task PollinousCore_AccumulatesHandDrawsAcrossCombatsUntilFourthTrigger()
    {
        (RunState run, Player player) = CreateRun("pollinous-cross-combat");
        await RelicCmd.Obtain(ModelDb.Relic<PollinousCore>(), player);
        int[] expectedHandSizes = [5, 5, 5, 7, 5];

        foreach (int expected in expectedHandSizes)
        {
            CombatRoom room = await EnterCombat(run);
            Assert.Equal(expected, player.PlayerCombatState!.Hand.Cards.Count);
            await room.Exit(run);
        }
    }

    [Fact]
    public async Task PaelsLegion_DoublesEveryPoweredBlockInOneCardPlayAndResetsBetweenCombats()
    {
        (RunState run, Player player) = CreateRun("paels-legion-multi-block");
        await RelicCmd.Obtain(ModelDb.Relic<PaelsLegion>(), player);
        CombatRoom room = await EnterCombat(run);
        ClearCombatPiles(player);
        HiveRound2MultiBlockCard card = AddToHand<HiveRound2MultiBlockCard>(player);

        await room.Engine.PlayCardAsync(player, card, null);

        Assert.Equal(25, player.Creature.Block);
        DefendRegent cooldownCard = AddToHand<DefendRegent>(player);
        await room.Engine.PlayCardAsync(player, cooldownCard, null);
        Assert.Equal(30, player.Creature.Block);
        await CreatureCmd.Kill(room.Engine.State.Enemies.Single());
        Assert.True(room.Engine.CheckWinCondition());
        await room.ResolveOutcomeAsync(generateRewards: false);
        await room.Exit(run);

        CombatRoom nextRoom = await EnterCombat(run);
        ClearCombatPiles(player);
        DefendRegent nextCombatCard = AddToHand<DefendRegent>(player);
        await nextRoom.Engine.PlayCardAsync(player, nextCombatCard, null);
        Assert.Equal(10, player.Creature.Block);
    }

    [Fact]
    public async Task PaelsTears_LatestTurnEndWinsEvenWhenEnergyBecomesZero()
    {
        (RunState run, Player player) = CreateRun("paels-tears-latest");
        await RelicCmd.Obtain(ModelDb.Relic<PaelsTears>(), player);
        CombatRoom room = await EnterCombat(run);
        player.PlayerCombatState!.Energy = 1;
        await Hook.BeforeSideTurnEnd(room.Engine.State, CombatSide.Player, [player.Creature]);
        player.PlayerCombatState.Energy = 0;
        await Hook.BeforeSideTurnEnd(room.Engine.State, CombatSide.Player, [player.Creature]);
        player.PlayerCombatState.ResetEnergy();

        await Hook.AfterEnergyReset(room.Engine.State, player);

        Assert.Equal(player.PlayerCombatState.MaxEnergy, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task PaelsTears_DoesNotCarryPendingEnergyAcrossCombatEnd()
    {
        (RunState run, Player player) = CreateRun("paels-tears-combat-end");
        await RelicCmd.Obtain(ModelDb.Relic<PaelsTears>(), player);
        CombatRoom room = await EnterCombat(run);
        player.PlayerCombatState!.Energy = 1;
        await Hook.BeforeSideTurnEnd(room.Engine.State, CombatSide.Player, [player.Creature]);
        await CreatureCmd.Kill(room.Engine.State.Enemies.Single());
        Assert.True(room.Engine.CheckWinCondition());
        await room.ResolveOutcomeAsync(generateRewards: false);
        await room.Exit(run);

        CombatRoom nextRoom = await EnterCombat(run);

        Assert.Equal(player.PlayerCombatState!.MaxEnergy, player.PlayerCombatState.Energy);
        await nextRoom.Exit(run);
    }

    [Fact]
    public async Task ToastyMittens_EmptyDeckStillGainsStrengthAfterTheEmptyHandDraw()
    {
        (RunState run, Player player) = CreateRun("toasty-empty-deck");
        foreach (CardModel card in player.Deck.Cards.ToList())
            CardPileCmd.Remove(card);
        await RelicCmd.Obtain(ModelDb.Relic<ToastyMittens>(), player);

        _ = await EnterCombat(run);

        Assert.Empty(player.PlayerCombatState!.Hand.Cards);
        Assert.Empty(player.PlayerCombatState.DrawPile.Cards);
        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
    }

    [Fact]
    public async Task ToastyMittens_NoDrawAndEmptyHandStillGainsStrength()
    {
        (RunState run, Player player) = CreateRun("toasty-no-draw");
        await RelicCmd.Obtain(ModelDb.Relic<ToastyMittens>(), player);
        var selection = new LegacySelectionDecisionSource();
        CombatRoom room = await EnterCombat(run, selection);
        ClearCombatPiles(player);
        await PowerCmd.Apply<NoDrawPower>(room.Engine.State, player.Creature, 1m, player.Creature, null);
        await CardPileCmd.Draw(room.Engine.State, 5, player, fromHandDraw: true);

        await Hook.AfterSideTurnStart(room.Engine.State, CombatSide.Player, [player.Creature]);

        Assert.Empty(player.PlayerCombatState!.Hand.Cards);
        Assert.Equal(2, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
    }

    private static (RunState Run, Player Player) CreateRun(string seed)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        return (run, player);
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed)
    {
        (RunState run, Player player) = CreateRun(seed);
        return (player, await EnterCombat(run));
    }

    private static async Task<CombatRoom> EnterCombat(RunState run, LegacySelectionDecisionSource? selection = null)
    {
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        if (selection is not null) room.ConfigureCardSelectionSource(selection);
        await room.Enter(run);
        return room;
    }

    private static T AddToHand<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static TEvent Begin<TEvent>(RunState run, Player player) where TEvent : EventModel
    {
        var ev = (TEvent)ModelDb.Event<TEvent>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(run);
        return ev;
    }

    private static void ClearCombatPiles(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).ToList())
            CardPileCmd.Remove(card);
    }
}
