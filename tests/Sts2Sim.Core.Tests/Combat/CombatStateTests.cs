namespace Sts2Sim.Core.Tests.Combat;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Random;

[Collection("ModelDb")]
public class CombatStateTests
{
    public CombatStateTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight), typeof(TrainingDummy), typeof(StrengthPower), typeof(SnapshotMutationPower), typeof(SnapshotCountingPower), typeof(SnapshotCard),
        });
    }

    private sealed class FakeRunState : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        private readonly List<Player> _players = new();

        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            childCombatState?.IterateHookListeners() ?? Array.Empty<AbstractModel>();

        public RunRngSet Rng { get; } = new("combat_state_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => _players;
        public int TotalFloor => 0;

        public void AddPlayer(Player player) => _players.Add(player);
    }


    private sealed class SnapshotMutationPower : PowerModel
    {
        public Action? Mutation { get; set; }

        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public override Task AfterCardPlayed(CardPlay cardPlay)
        {
            Mutation?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class SnapshotCountingPower : PowerModel
    {
        public int AfterCardPlayedCount { get; private set; }

        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public override Task AfterCardPlayed(CardPlay cardPlay)
        {
            AfterCardPlayedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class SnapshotCard : CardModel
    {
        public int AfterCardPlayedCount { get; private set; }

        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Basic;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 0;

        public override Task AfterCardPlayed(CardPlay cardPlay)
        {
            AfterCardPlayedCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void AddPlayerCreature_AddsToAllies_AndSetsCombatState()
    {
        var runState = new FakeRunState();
        var combatState = new CombatState(runState);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        combatState.AddPlayerCreature(player.Creature);

        Assert.Contains(player.Creature, combatState.Allies);
        Assert.Same(combatState, player.Creature.CombatState);
    }

    [Fact]
    public void AddMonster_AssignsSequentialCombatId_AndNativeIndividualRngSeed()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
        var runState = new RunState(
            "combat_state_tests",
            [new Overgrowth(), new Hive()]);
        runState.AdvanceToNextAct();
        var currentCoord = new MapCoord(3, 5);
        runState.AddVisitedMapCoord(currentCoord);
        var combatState1 = new CombatState(runState);
        var combatState2 = new CombatState(runState);
        var dummy1 = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();
        var dummy2 = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();

        Creature c1 = combatState1.AddMonster(dummy1, CombatSide.Enemy);
        Creature c2 = combatState2.AddMonster(dummy2, CombatSide.Enemy);
        var expectedRng = new Rng((ulong)(
            (long)runState.Rng.Seed
            + (long)currentCoord.col
            + currentCoord.row
            + runState.CurrentActIndex
            + c1.CombatId!.Value));

        Assert.Equal(0u, c1.CombatId);
        Assert.Equal(0u, c2.CombatId);
        Assert.Equal(expectedRng.ToSerializable(), dummy1.Rng.ToSerializable());
    }

    [Fact]
    public void AddMonster_SecondMonster_GetsDifferentCombatIdAndRngSeed()
    {
        var runState = new FakeRunState();
        var combatState = new CombatState(runState);
        var dummy1 = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();
        var dummy2 = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();

        combatState.AddMonster(dummy1, CombatSide.Enemy);
        Creature c2 = combatState.AddMonster(dummy2, CombatSide.Enemy);

        Assert.Equal(1u, c2.CombatId);
        Assert.NotEqual(dummy1.Rng.NextInt(1000), dummy2.Rng.NextInt(1000));
    }

    [Fact]
    public void GetOpponentsOf_ReturnsTheOtherSide()
    {
        var runState = new FakeRunState();
        var combatState = new CombatState(runState);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        combatState.AddPlayerCreature(player.Creature);
        var dummy = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();
        Creature enemy = combatState.AddMonster(dummy, CombatSide.Enemy);

        Assert.Equal(new[] { enemy }, combatState.GetOpponentsOf(player.Creature));
        Assert.Equal(new[] { player.Creature }, combatState.GetOpponentsOf(enemy));
    }

    [Fact]
    public void IterateHookListeners_YieldsCreaturePowersBeforeItsMonsterModel_AlliesBeforeEnemies()
    {
        var runState = new FakeRunState();
        var combatState = new CombatState(runState);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.ResetCombatState();
        combatState.AddPlayerCreature(player.Creature);
        var strength = (StrengthPower)ModelDb.Power<StrengthPower>().MutableClone();
        strength.ApplyInternal(player.Creature, 3m);
        var dummy = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();
        combatState.AddMonster(dummy, CombatSide.Enemy);

        var listeners = combatState.IterateHookListeners().ToList();

        int strengthIndex = listeners.IndexOf(strength);
        int dummyIndex = listeners.IndexOf(dummy);
        Assert.True(strengthIndex >= 0);
        Assert.True(dummyIndex >= 0);
        Assert.True(strengthIndex < dummyIndex);
    }

    [Fact]
    public void IterateHookListeners_YieldsCardsFromAllPiles_ForActivePlayer()
    {
        var runState = new FakeRunState();
        var combatState = new CombatState(runState);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.ResetCombatState();
        combatState.AddPlayerCreature(player.Creature);
        var hand = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        var draw = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        var discard = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        var exhaust = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        var play = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        player.PlayerCombatState!.Hand.AddInternal(hand);
        player.PlayerCombatState.DrawPile.AddInternal(draw);
        player.PlayerCombatState.DiscardPile.AddInternal(discard);
        player.PlayerCombatState.ExhaustPile.AddInternal(exhaust);
        player.PlayerCombatState.PlayPile.AddInternal(play);

        var listeners = combatState.IterateHookListeners().ToList();

        Assert.Equal(new CardModel[] { hand, draw, discard, exhaust, play }, listeners.OfType<CardModel>());
    }

    [Fact]
    public async Task IterateHookListeners_UsesWholeDispatchSnapshot_WhenEarlyListenerMutatesLaterPowersAndCards()
    {
        var runState = new FakeRunState();
        var combatState = new CombatState(runState);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.ResetCombatState();
        combatState.AddPlayerCreature(player.Creature);
        Creature enemy = combatState.AddMonster((TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(), CombatSide.Enemy);

        var mutationPower = (SnapshotMutationPower)ModelDb.Power<SnapshotMutationPower>().MutableClone();
        mutationPower.ApplyInternal(player.Creature, 1m);
        var existingPower = (SnapshotCountingPower)ModelDb.Power<SnapshotCountingPower>().MutableClone();
        existingPower.ApplyInternal(enemy, 1m);
        var addedPower = (SnapshotCountingPower)ModelDb.Power<SnapshotCountingPower>().MutableClone();
        var existingCard = (SnapshotCard)ModelDb.Card<SnapshotCard>().MutableClone();
        existingCard.AssignOwner(player);
        player.PlayerCombatState!.DrawPile.AddInternal(existingCard);
        var addedCard = (SnapshotCard)ModelDb.Card<SnapshotCard>().MutableClone();
        addedCard.AssignOwner(player);
        bool mutated = false;
        mutationPower.Mutation = () =>
        {
            if (mutated)
            {
                return;
            }

            mutated = true;
            addedPower.ApplyInternal(enemy, 1m);
            player.PlayerCombatState.DrawPile.AddInternal(addedCard);
        };
        var cardPlay = new CardPlay
        {
            Card = existingCard,
            Player = player,
            Target = null,
            ResultPile = PileType.Discard,
            Resources = new ResourceInfo(0, 0, 0, 0),
            IsAutoPlay = false,
            PlayIndex = 0,
            PlayCount = 1,
        };

        await Hook.AfterCardPlayed(combatState, cardPlay);

        Assert.Equal(1, existingPower.AfterCardPlayedCount);
        Assert.Equal(1, existingCard.AfterCardPlayedCount);
        Assert.Equal(0, addedPower.AfterCardPlayedCount);
        Assert.Equal(0, addedCard.AfterCardPlayedCount);

        await Hook.AfterCardPlayed(combatState, cardPlay);

        Assert.Equal(2, existingPower.AfterCardPlayedCount);
        Assert.Equal(2, existingCard.AfterCardPlayedCount);
        Assert.Equal(1, addedPower.AfterCardPlayedCount);
        Assert.Equal(1, addedCard.AfterCardPlayedCount);
    }
}
