using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Potions;

file abstract class Task10AutoCard : CardModel
{
    public static List<string> Plays { get; } = new();

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;

    protected Task Record(string name)
    {
        Plays.Add(name);
        return Task.CompletedTask;
    }
}

file sealed class Task10AutoCardA : Task10AutoCard
{
    protected override Task OnPlay(CardPlay cardPlay) => Record("A");
}

file sealed class Task10AutoCardB : Task10AutoCard
{
    protected override Task OnPlay(CardPlay cardPlay) => Record("B");
}

file sealed class Task10AutoCardC : Task10AutoCard
{
    protected override Task OnPlay(CardPlay cardPlay) => Record("C");
}

file sealed class Task10FillerCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
}

[Collection("ModelDb")]
public sealed class RarePotionBatch1Tests : IDisposable
{
    public RarePotionBatch1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes.Concat(new[]
            {
                typeof(RarePotionBatch1TestCharacter),
                typeof(Task10AutoCardA),
                typeof(Task10AutoCardB),
                typeof(Task10AutoCardC),
                typeof(Task10FillerCard),
            }));
        Task10AutoCard.Plays.Clear();
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static TheoryData<string, PotionUsage, TargetType> MetadataCases => new()
    {
        { "BeetleJuice", PotionUsage.CombatOnly, TargetType.AnyEnemy },
        { "BottledPotential", PotionUsage.CombatOnly, TargetType.AnyPlayer },
        { "CosmicConcoction", PotionUsage.CombatOnly, TargetType.AnyPlayer },
        { "DistilledChaos", PotionUsage.CombatOnly, TargetType.AnyPlayer },
        { "DropletOfPrecognition", PotionUsage.CombatOnly, TargetType.AnyPlayer },
        { "EntropicBrew", PotionUsage.AnyTime, TargetType.AnyPlayer },
    };

    [Theory]
    [MemberData(nameof(MetadataCases))]
    public void Metadata_IsExactlyRareWithSpecifiedUsageAndTarget(
        string potionName,
        PotionUsage usage,
        TargetType targetType)
    {
        PotionModel potion = GetCanonicalPotion(potionName);

        Assert.Equal(PotionRarity.Rare, potion.Rarity);
        Assert.Equal(usage, potion.Usage);
        Assert.Equal(targetType, potion.TargetType);
    }

    [Fact]
    public async Task BeetleJuice_AppliesFourFixedThirtyPercentPoweredAttackReduction()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("beetle-factor");
        Creature enemy = room.Engine.State.Enemies.Single();

        await UsePotionAsync("BeetleJuice", player, enemy);

        PowerModel shrink = Assert.Single(
            enemy.Powers,
            power => power.GetType() == RequireTask10Type("ShrinkPower"));
        Assert.Equal(PowerType.Debuff, shrink.Type);
        Assert.Equal(PowerStackType.Counter, shrink.StackType);
        Assert.Equal(4, shrink.Amount);
        Assert.Same(player.Creature, shrink.Applier);
        Assert.Equal(0.70m, shrink.ModifyDamageMultiplicative(
            player.Creature, 20m, ValueProp.Move, enemy, null, null));
        shrink.SetAmount(1);
        Assert.Equal(0.70m, shrink.ModifyDamageMultiplicative(
            player.Creature, 20m, ValueProp.Move, enemy, null, null));
        Assert.Equal(1m, shrink.ModifyDamageMultiplicative(
            player.Creature, 20m, ValueProp.Unpowered, enemy, null, null));
        Assert.Equal(1m, shrink.ModifyDamageMultiplicative(
            enemy, 20m, ValueProp.Move, player.Creature, null, null));
    }

    [Fact]
    public async Task ShrinkPower_DecaysOnlyWhenOwnerParticipatesAndRemovesAtZero()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("beetle-decay");
        Creature enemy = room.Engine.State.Enemies.Single();
        await UsePotionAsync("BeetleJuice", player, enemy);
        PowerModel shrink = Assert.Single(
            enemy.Powers,
            power => power.GetType() == RequireTask10Type("ShrinkPower"));

        await shrink.AfterSideTurnEnd(CombatSide.Player, room.Engine.State.Allies);
        Assert.Equal(4, shrink.Amount);

        for (int expected = 3; expected >= 0; expected--)
        {
            await shrink.AfterSideTurnEnd(CombatSide.Enemy, room.Engine.State.Enemies);
            Assert.Equal(expected, shrink.Amount);
            Assert.Equal(expected > 0, enemy.Powers.Contains(shrink));
        }
    }

    [Fact]
    public async Task ShrinkPower_IsRemovedWhenItsApplierDies()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("beetle-applier");
        Creature enemy = room.Engine.State.Enemies.Single();
        await UsePotionAsync("BeetleJuice", player, enemy);
        PowerModel shrink = Assert.Single(
            enemy.Powers,
            power => power.GetType() == RequireTask10Type("ShrinkPower"));
        player.Creature.LoseHpInternal(player.Creature.CurrentHp, ValueProp.Unpowered);

        await Hook.AfterDeath(room.Engine.State, player.Creature);

        Assert.DoesNotContain(shrink, enemy.Powers);
    }

    [Fact]
    public async Task ShrinkPower_NegativeAmountIsInfiniteAndDoesNotDecay()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("beetle-infinite");
        Creature enemy = room.Engine.State.Enemies.Single();
        PowerModel shrink = (await PowerCmd.Apply(
            room.Engine.State,
            RequireTask10Type("ShrinkPower"),
            enemy,
            -1m,
            player.Creature,
            cardSource: null))!;

        Assert.True(shrink.AllowNegative);
        Assert.Equal(PowerStackType.Single, shrink.StackType);
        await shrink.AfterSideTurnEnd(CombatSide.Enemy, room.Engine.State.Enemies);
        Assert.Equal(-1, shrink.Amount);
        Assert.Contains(shrink, enemy.Powers);
    }

    [Fact]
    public async Task BottledPotential_ReturnsWholeHandCombinesPilesThenDrawsFive()
    {
        (Player player, _) = await CreateCombatAsync("bottled-potential");
        ClearCombatPiles(player);
        CardModel handA = AddCard<Task10FillerCard>(player, PileType.Hand);
        CardModel handB = AddCard<Task10FillerCard>(player, PileType.Hand);
        CardModel discard = AddCard<Task10FillerCard>(player, PileType.Discard);
        CardModel drawA = AddCard<Task10FillerCard>(player, PileType.Draw);
        CardModel drawB = AddCard<Task10FillerCard>(player, PileType.Draw);
        CardModel drawC = AddCard<Task10FillerCard>(player, PileType.Draw);
        CardModel drawD = AddCard<Task10FillerCard>(player, PileType.Draw);
        CardModel[] allCards = [handA, handB, discard, drawA, drawB, drawC, drawD];

        await UsePotionAsync("BottledPotential", player, player.Creature);

        Assert.Equal(5, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Empty(player.PlayerCombatState.DiscardPile.Cards);
        Assert.Equal(2, player.PlayerCombatState.DrawPile.Cards.Count);
        Assert.All(allCards, card =>
            Assert.True(
                player.PlayerCombatState.Hand.Cards.Contains(card) ||
                player.PlayerCombatState.DrawPile.Cards.Contains(card)));
    }

    [Fact]
    public async Task CosmicConcoction_GeneratesThreeDistinctUpgradedColorlessCardsIntoHand()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("cosmic-concoction");
        ClearCombatPiles(player);
        int rngBefore = room.Engine.State.RunState.Rng.CombatCardGeneration.Counter;

        await UsePotionAsync("CosmicConcoction", player, player.Creature);

        IReadOnlyList<CardModel> hand = player.PlayerCombatState!.Hand.Cards;
        Assert.Equal(3, hand.Count);
        Assert.Equal(3, hand.Select(card => card.GetType()).Distinct().Count());
        Assert.All(hand, card =>
        {
            Assert.True(card.IsColorless);
            Assert.True(card.IsUpgraded);
        });
        Assert.Equal(rngBefore + 49, room.Engine.State.RunState.Rng.CombatCardGeneration.Counter);
    }

    [Fact]
    public async Task DistilledChaos_AutoPlaysExactlyTopThreeInOrder()
    {
        (Player player, _) = await CreateCombatAsync("distilled-chaos");
        ClearCombatPiles(player);
        AddCard<Task10AutoCardA>(player, PileType.Draw);
        AddCard<Task10AutoCardB>(player, PileType.Draw);
        AddCard<Task10AutoCardC>(player, PileType.Draw);
        CardModel untouched = AddCard<Task10FillerCard>(player, PileType.Draw);

        await UsePotionAsync("DistilledChaos", player, player.Creature);

        Assert.Equal(["A", "B", "C"], Task10AutoCard.Plays);
        Assert.Contains(untouched, player.PlayerCombatState!.DrawPile.Cards);
        Assert.Equal(3, player.PlayerCombatState.DiscardPile.Cards.Count);
    }

    [Fact]
    public async Task DropletOfPrecognition_TakesFirstDrawPileCardAndEmptyPileIsNoOp()
    {
        (Player player, _) = await CreateCombatAsync("droplet");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        ClearCombatPiles(player);
        CardModel first = AddCard<Task10AutoCardA>(player, PileType.Draw);
        CardModel second = AddCard<Task10AutoCardB>(player, PileType.Draw);

        await UsePotionAsync("DropletOfPrecognition", player, player.Creature);

        Assert.Contains(first, player.PlayerCombatState!.Hand.Cards);
        Assert.Equal([second], player.PlayerCombatState.DrawPile.Cards);

        PotionModel secondPotion = player.AddPotionInternal(GetCanonicalPotion("DropletOfPrecognition"));
        CardPileCmd.Add(second, PileType.Hand);
        await PotionCmd.Use(secondPotion, player, player.Creature);
        Assert.Equal(2, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Empty(player.PlayerCombatState.DrawPile.Cards);
    }

    [Fact]
    public async Task DropletOfPrecognition_FullHandMovesSelectedCardToDiscard()
    {
        (Player player, _) = await CreateCombatAsync("droplet-full-hand");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        ClearCombatPiles(player);
        for (int index = 0; index < CardPile.MaxCardsInHand; index++)
        {
            AddCard<Task10FillerCard>(player, PileType.Hand);
        }
        CardModel selected = AddCard<Task10AutoCardA>(player, PileType.Draw);

        await UsePotionAsync("DropletOfPrecognition", player, player.Creature);

        Assert.Equal(CardPile.MaxCardsInHand, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Empty(player.PlayerCombatState.DrawPile.Cards);
        Assert.Contains(selected, player.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public async Task EntropicBrew_InCombatStillUsesTheOutOfCombatPotionPool()
    {
        (RunState runState, Player player) = CreateRun("entropic-in-combat-pool");
        PotionModel[] playerRarePool = PotionFactory.GetOutOfCombatPool(player)
            .Where(potion => potion.Rarity == PotionRarity.Rare)
            .ToArray();
        PotionModel[] rarePool = ModelDb.All<PotionModel>()
            .Where(potion => potion.Rarity == PotionRarity.Rare)
            .ToArray();
        ulong seed = Enumerable.Range(0, 10_000)
            .Select(value => (ulong)value)
            .FirstOrDefault(candidate =>
            {
                var playerProbe = new Rng(candidate);
                var flatProbe = new Rng(candidate);
                return PotionFactory.RollRarity(playerProbe) == PotionFactory.Rarity.Rare &&
                    PotionFactory.RollRarity(flatProbe) == PotionFactory.Rarity.Rare &&
                    playerProbe.NextItem(playerRarePool) is FairyInABottle &&
                    flatProbe.NextItem(rarePool) is not FairyInABottle;
            }, ulong.MaxValue);
        Assert.True(seed != ulong.MaxValue,
            "No seed in [0, 10000) distinguishes the player's rare potion pool from the flat pool.");

        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        runState.Rng.MockRng(RunRngType.CombatPotionGeneration, seed);
        PotionModel brew = player.AddPotionInternal(GetCanonicalPotion("EntropicBrew"));

        await PotionCmd.Use(brew, player, player.Creature);

        Assert.IsType<FairyInABottle>(player.PotionSlots[0]);
        Assert.Equal(6, runState.Rng.CombatPotionGeneration.Counter);
    }

    [Fact]
    public async Task EntropicBrew_WorksOutOfCombatFillsAllSlotsAndUsesDedicatedRng()
    {
        (RunState runState, Player player) = CreateRun("entropic-out-of-combat");
        PotionModel brew = player.AddPotionInternal(GetCanonicalPotion("EntropicBrew"));
        int potionRngBefore = runState.Rng.CombatPotionGeneration.Counter;
        int rewardsRngBefore = player.PlayerRng.Rewards.Counter;

        await PotionCmd.Use(brew, player, player.Creature);

        Assert.All(player.PotionSlots, potion => Assert.NotNull(potion));
        Assert.Equal(potionRngBefore + 6, runState.Rng.CombatPotionGeneration.Counter);
        Assert.Equal(rewardsRngBefore, player.PlayerRng.Rewards.Counter);
    }

    [Fact]
    public async Task EntropicBrew_CanTargetAnotherPlayerInTheSameRunOutsideCombat()
    {
        var runState = new RunState("entropic-same-run-ally", new Overgrowth());
        Player owner = Player.CreateForNewRun(
            ModelDb.Character<RarePotionBatch1TestCharacter>(), runState);
        Player ally = Player.CreateForNewRun(
            ModelDb.Character<RarePotionBatch1TestCharacter>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(ally);
        PotionModel brew = owner.AddPotionInternal(GetCanonicalPotion("EntropicBrew"));

        await PotionCmd.Use(brew, owner, ally.Creature);

        Assert.All(ally.PotionSlots, potion => Assert.NotNull(potion));
        Assert.All(owner.PotionSlots, potion => Assert.Null(potion));
    }

    [Fact]
    public async Task EntropicBrew_PreservesOccupiedSlotsAndFillsOnlyTheNewlyEmptySlot()
    {
        (RunState runState, Player player) = CreateRun("entropic-partial");
        PotionModel occupiedA = player.AddPotionInternal(ModelDb.Potion<EnergyPotion>());
        PotionModel occupiedB = player.AddPotionInternal(ModelDb.Potion<EnergyPotion>());
        PotionModel brew = player.AddPotionInternal(GetCanonicalPotion("EntropicBrew"));
        int rngBefore = runState.Rng.CombatPotionGeneration.Counter;

        await PotionCmd.Use(brew, player, player.Creature);

        Assert.Contains(occupiedA, player.PotionSlots);
        Assert.Contains(occupiedB, player.PotionSlots);
        Assert.All(player.PotionSlots, potion => Assert.NotNull(potion));
        Assert.Equal(rngBefore + 2, runState.Rng.CombatPotionGeneration.Counter);
    }

    [Fact]
    public async Task EntropicBrew_StopsWhenRolledRarityBucketIsEmpty()
    {
        Type entropicType = RequireTask10Type("EntropicBrew");
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(RarePotionBatch1TestCharacter),
            entropicType,
        });
        ulong commonSeed = Enumerable.Range(0, 100)
            .Select(value => (ulong)value)
            .First(seed =>
                PotionFactory.RollRarity(new Rng(seed)) == PotionFactory.Rarity.Common);

        (RunState runState, Player player) = CreateRun("entropic-empty-bucket");
        runState.Rng.MockRng(RunRngType.CombatPotionGeneration, commonSeed);
        PotionModel brew = player.AddPotionInternal(GetCanonicalPotion("EntropicBrew"));

        await PotionCmd.Use(brew, player, player.Creature);

        Assert.All(player.PotionSlots, potion => Assert.Null(potion));
        Assert.Equal(1, runState.Rng.CombatPotionGeneration.Counter);
    }

    [Fact]
    public async Task AnyTimeAnyPlayerPotion_RejectsForeignRunTargetWithoutConsumption()
    {
        (_, Player owner) = CreateRun("entropic-owner");
        (_, Player foreign) = CreateRun("entropic-foreign");
        PotionModel brew = owner.AddPotionInternal(GetCanonicalPotion("EntropicBrew"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PotionCmd.Use(brew, owner, foreign.Creature));

        Assert.Contains(brew, owner.PotionSlots);
    }

    private static PotionModel GetCanonicalPotion(string potionName)
    {
        Type potionType = RequireTask10Type(potionName);
        return ModelDb.GetById<PotionModel>(ModelDb.GetId(potionType));
    }

    private static async Task UsePotionAsync(string potionName, Player owner, Creature target)
    {
        PotionModel potion = owner.AddPotionInternal(GetCanonicalPotion(potionName));
        await PotionCmd.Use(potion, owner, target);
    }

    private static Type RequireTask10Type(string name)
    {
        string category = name.EndsWith("Power", StringComparison.Ordinal)
            ? "Powers"
            : "Potions";
        return typeof(PotionModel).Assembly.GetType($"Sts2Sim.Core.Models.{category}.{name}")
            ?? throw new Xunit.Sdk.XunitException($"Task 10 model {name} is not implemented.");
    }

    private static TCard AddCard<TCard>(Player player, PileType pile)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pile);
        return card;
    }

    private static void ClearCombatPiles(Player player)
    {
        foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
        {
            foreach (CardModel card in pile.Cards.ToList())
            {
                CardPileCmd.Remove(card);
            }
        }
    }

    private static (RunState runState, Player player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(
            ModelDb.Character<RarePotionBatch1TestCharacter>(),
            runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        (RunState runState, Player player) = CreateRun(seed);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}

file sealed class RarePotionBatch1TestCharacter : CharacterModel
{
    public override int StartingHp => 75;
    public override int StartingGold => 99;
}
