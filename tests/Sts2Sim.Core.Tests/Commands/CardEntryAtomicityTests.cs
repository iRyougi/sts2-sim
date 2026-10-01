namespace Sts2Sim.Core.Tests.Commands;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Models.Afflictions;

[Collection("ModelDb")]
public sealed class CardEntryAtomicityTests : IDisposable
{
    private const string EntryFailureMessage = "entry-hook-failure";

    private sealed class ThrowingEntryPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public CardModel? ObservedCard { get; private set; }

        public CardModel[] ObservedPileCards { get; private set; } = [];

        public override Task AfterCardEnteredCombat(CardModel card)
        {
            ObservedCard = card;
            ObservedPileCards = card.Pile?.Cards.ToArray() ?? [];
            throw new InvalidOperationException(EntryFailureMessage);
        }
    }

    private sealed class AbortTrackingPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public int EntryCount { get; private set; }

        public int AbortCount { get; private set; }

        public override Task AfterCardEnteredCombat(CardModel card)
        {
            EntryCount++;
            return Task.CompletedTask;
        }

        public override Task AfterCardEntryAborted(CardModel card)
        {
            AbortCount++;
            return Task.CompletedTask;
        }
    }

    public CardEntryAtomicityTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(DivineRight), typeof(WanderingGrunt), typeof(SovereignBlade),
            typeof(WeakPower), typeof(VulnerablePower), typeof(TangledPower), typeof(Entangled), typeof(ThrowingEntryPower),
            typeof(AbortTrackingPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Generate_WhenEntryHookThrows_RollsBackPileCounterAndTangledMark()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("entry-rollback-generate");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<TangledPower>(room.Engine.State, player.Creature, 1m, enemy, null);
        await PowerCmd.Apply<ThrowingEntryPower>(room.Engine.State, player.Creature, 1m, enemy, null);
        SovereignBlade generated = CreateOwned<SovereignBlade>(player);
        Dictionary<CardPile, CardModel[]> pilesBefore = SnapshotPiles(player);
        int generatedBefore = player.PlayerCombatState!.CardsGeneratedThisCombat;

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardPileCmd.Generate(room.Engine.State, generated, PileType.Hand));

        Assert.Equal(EntryFailureMessage, exception.Message);
        AssertPilesEqual(pilesBefore);
        Assert.Equal(generatedBefore, player.PlayerCombatState.CardsGeneratedThisCombat);
        Assert.Null(generated.Pile);
        Assert.Equal(2, generated.EnergyCost);
    }

    [Fact]
    public async Task Generate_WhenEarlierEntryHookThrows_DoesNotCompensateUninvokedListener()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("entry-rollback-invoked-prefix");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<ThrowingEntryPower>(room.Engine.State, player.Creature, 1m, enemy, null);
        await PowerCmd.Apply<AbortTrackingPower>(room.Engine.State, player.Creature, 1m, enemy, null);
        AbortTrackingPower tracking = Assert.Single(player.Creature.Powers.OfType<AbortTrackingPower>());
        SovereignBlade generated = CreateOwned<SovereignBlade>(player);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardPileCmd.Generate(room.Engine.State, generated, PileType.Hand));

        Assert.Equal(EntryFailureMessage, exception.Message);
        Assert.Equal(0, tracking.EntryCount);
        Assert.Equal(0, tracking.AbortCount);
        Assert.Null(generated.Pile);
        Assert.Equal(0, player.PlayerCombatState!.CardsGeneratedThisCombat);
    }

    [Fact]
    public async Task Forge_WhenEntryHookThrows_DoesNotCreateOrModifyBlades()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("entry-rollback-forge");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await ForgeCmd.Forge(2m, player, source: null);
        SovereignBlade exhausted = Assert.Single(AllBlades(player));
        CardPileCmd.Add(exhausted, PileType.Exhaust);
        Dictionary<CardPile, CardModel[]> pilesBefore = SnapshotPiles(player);
        await PowerCmd.Apply<ThrowingEntryPower>(room.Engine.State, player.Creature, 1m, enemy, null);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ForgeCmd.Forge(5m, player, source: null));

        Assert.Equal(EntryFailureMessage, exception.Message);
        AssertPilesEqual(pilesBefore);
        Assert.Same(exhausted, Assert.Single(AllBlades(player)));

        ThrowingEntryPower throwing = Assert.Single(player.Creature.Powers.OfType<ThrowingEntryPower>());
        await PowerCmd.Remove(throwing);
        CardPileCmd.Add(exhausted, PileType.Hand);
        int hpBefore = enemy.CurrentHp;
        player.PlayerCombatState!.Energy = 2;
        await exhausted.PlayAsync(enemy);
        Assert.Equal(hpBefore - 12, enemy.CurrentHp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transform_WhenEntryHookThrows_RestoresOriginalPositionAndCleansReplacement(bool useBatch)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("entry-rollback-transform");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        DefendRegent leftPeer = CreateOwned<DefendRegent>(player);
        DefendRegent original = CreateOwned<DefendRegent>(player);
        DefendRegent rightPeer = CreateOwned<DefendRegent>(player);
        CardPileCmd.Add(leftPeer, PileType.Hand);
        CardPileCmd.Add(original, PileType.Hand);
        CardPileCmd.Add(rightPeer, PileType.Hand);
        Dictionary<CardPile, CardModel[]> pilesBefore = SnapshotPiles(player);
        CardPile originalPile = original.Pile!;
        int originalIndex = Array.IndexOf(pilesBefore[originalPile], original);
        var replacement = (SovereignBlade)ModelDb.Card<SovereignBlade>().MutableClone();
        await PowerCmd.Apply<TangledPower>(room.Engine.State, player.Creature, 1m, enemy, null);
        await PowerCmd.Apply<ThrowingEntryPower>(room.Engine.State, player.Creature, 1m, enemy, null);
        ThrowingEntryPower throwing = Assert.Single(player.Creature.Powers.OfType<ThrowingEntryPower>());
        Creature? originalCombatStateOwner = original.Owner.Creature;
        int generatedBefore = player.PlayerCombatState!.CardsGeneratedThisCombat;
        if (useBatch) replacement.AssignOwner(player);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
            {
                if (useBatch)
                    await CardCmd.Transform([new CardTransformation(original, replacement)], rng: null);
                else
                    await CardCmd.Transform(original, replacement);
            });

        Assert.Equal(EntryFailureMessage, exception.Message);
        Assert.Same(replacement, throwing.ObservedCard);
        CardModel[] expectedEntryCards = pilesBefore[originalPile].ToArray();
        expectedEntryCards[originalIndex] = replacement;
        Assert.True(expectedEntryCards.SequenceEqual(throwing.ObservedPileCards, ReferenceEqualityComparer.Instance),
            $"entry-rollback-transform useBatch={useBatch}: entry hook must observe replacement at original index {originalIndex} between the same peers.");
        Assert.Same(leftPeer, throwing.ObservedPileCards[originalIndex - 1]);
        Assert.Same(rightPeer, throwing.ObservedPileCards[originalIndex + 1]);
        AssertPilesEqual(pilesBefore);
        Assert.Same(player, original.Owner);
        Assert.Same(originalCombatStateOwner, original.Owner.Creature);
        Assert.Same(room.Engine.State, original.CombatState);
        Assert.Null(replacement.Pile);
        Assert.Equal(useBatch ? room.Engine.State : null, replacement.CombatState);
        Assert.Equal(generatedBefore, player.PlayerCombatState!.CardsGeneratedThisCombat);
        Assert.DoesNotContain(
            player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards),
            card => ReferenceEquals(card, replacement));
        Assert.Equal(2, replacement.EnergyCost);
    }

    [Fact]
    public async Task Transform_WithReplacementAlreadyInPile_RejectsBeforeRemovingOriginal()
    {
        (Player player, _) = await CreateCombatAsync("entry-precondition-transform");
        DefendRegent original = CreateOwned<DefendRegent>(player);
        SovereignBlade replacement = CreateOwned<SovereignBlade>(player);
        CardPileCmd.Add(original, PileType.Hand);
        CardPileCmd.Add(replacement, PileType.Draw);
        Dictionary<CardPile, CardModel[]> pilesBefore = SnapshotPiles(player);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.Transform(original, replacement));

        AssertPilesEqual(pilesBefore);
        Assert.Same(player.PlayerCombatState!.Hand, original.Pile);
        Assert.Same(player.PlayerCombatState.DrawPile, replacement.Pile);
    }

    private static IEnumerable<SovereignBlade> AllBlades(Player player) =>
        player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).OfType<SovereignBlade>();

    private static Dictionary<CardPile, CardModel[]> SnapshotPiles(Player player) =>
        player.PlayerCombatState!.AllPiles.ToDictionary(pile => pile, pile => pile.Cards.ToArray());

    private static void AssertPilesEqual(Dictionary<CardPile, CardModel[]> expected)
    {
        foreach ((CardPile pile, CardModel[] cards) in expected)
        {
            Assert.Equal(cards, pile.Cards);
        }
    }

    private static TCard CreateOwned<TCard>(Player player) where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}
