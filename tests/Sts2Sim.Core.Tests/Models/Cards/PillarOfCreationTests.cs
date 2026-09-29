using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class PillarOfCreationTests : IDisposable
{
    public PillarOfCreationTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WanderingGrunt), typeof(SovereignBlade), typeof(DivineRight),
            typeof(PillarOfCreation), typeof(PillarOfCreationPower), typeof(FastenPower),
            typeof(GeneratedSnapshotMutationPower), typeof(GeneratedSnapshotCountingCard),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task PillarOfCreation_AppliesTwoAndUpgradeAppliesThree_AtOneEnergy()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("pillar-card");
        PillarOfCreation card = AddToHand<PillarOfCreation>(player);

        Assert.Equal(1, card.EnergyCost);
        await card.PlayAsync(target: null);
        Assert.Equal(2, player.Creature.Powers.OfType<PillarOfCreationPower>().Single().Amount);

        PillarOfCreation upgraded = AddToHand<PillarOfCreation>(player);
        upgraded.Upgrade();
        await upgraded.PlayAsync(target: null);

        Assert.Equal(1, upgraded.EnergyCost);
        Assert.Equal(5, player.Creature.Powers.OfType<PillarOfCreationPower>().Single().Amount);
        Assert.Same(room.Engine.State, player.Creature.CombatState);
    }

    [Fact]
    public async Task Generate_FirstOwnerCardInTurn_GainsUnpoweredBlock()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("pillar-first");
        await ApplyPillarAsync(room.Engine.State, player, 5m);
        await PowerCmd.Apply<FastenPower>(room.Engine.State, player.Creature, 3m, player.Creature, null);

        await GenerateAsync<StrikeRegent>(room.Engine.State, player, PileType.Hand);

        Assert.Equal(5, player.Creature.Block);
    }

    [Fact]
    public async Task Generate_SecondOwnerCardInTurn_DoesNotGainMoreBlock()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("pillar-second");
        await ApplyPillarAsync(room.Engine.State, player, 5m);

        await GenerateAsync<StrikeRegent>(room.Engine.State, player, PileType.Hand);
        await GenerateAsync<DefendRegent>(room.Engine.State, player, PileType.Discard);

        Assert.Equal(5, player.Creature.Block);
    }

    [Fact]
    public async Task Generate_NextOwnerPlayerTurn_ResetsGate()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("pillar-reset");
        await ApplyPillarAsync(room.Engine.State, player, 5m);
        await GenerateAsync<StrikeRegent>(room.Engine.State, player, PileType.Discard);

        await room.Engine.EndPlayerTurnAsync();
        await GenerateAsync<DefendRegent>(room.Engine.State, player, PileType.Discard);

        Assert.Equal(5, player.Creature.Block);
    }

    [Fact]
    public async Task Generate_OtherPlayersCard_DoesNotConsumeOwnersGate()
    {
        var runState = new RunState("pillar-other-player", new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player other = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(other);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        await ApplyPillarAsync(room.Engine.State, owner, 5m);

        await GenerateAsync<StrikeRegent>(room.Engine.State, other, PileType.Hand);
        Assert.Equal(0, owner.Creature.Block);

        await GenerateAsync<DefendRegent>(room.Engine.State, owner, PileType.Hand);
        Assert.Equal(5, owner.Creature.Block);
    }

    [Fact]
    public async Task Generate_PlacesCardInRequestedCombatPile_WithCorrectMembership()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("pillar-membership");
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        card.AssignOwner(player);

        await CardPileCmd.Generate(room.Engine.State, card, PileType.Hand);

        Assert.Same(player.PlayerCombatState!.Hand, card.Pile);
        Assert.Contains(card, player.PlayerCombatState.Hand.Cards);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Generate_AfterCombatEnds_RejectsWithoutChangingPileOrDispatchingHooks(bool victory)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("pillar-ended-combat");
        await ApplyPillarAsync(room.Engine.State, player, 5m);
        Creature endedCreature = victory ? room.Engine.State.Enemies.Single() : player.Creature;
        endedCreature.LoseHpInternal(decimal.MaxValue, default);
        Assert.True(room.Engine.CheckWinCondition());
        Assert.False(room.Engine.IsInProgress);
        Assert.Equal(victory, room.Engine.Won);
        int handCountBefore = player.PlayerCombatState!.Hand.Cards.Count;
        int blockBefore = player.Creature.Block;
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        card.AssignOwner(player);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardPileCmd.Generate(room.Engine.State, card, PileType.Hand));

        Assert.Null(card.Pile);
        Assert.Equal(handCountBefore, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Equal(blockBefore, player.Creature.Block);
    }

    [Fact]
    public async Task Generate_NonCombatPile_FailsClearly()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("pillar-invalid-pile");
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        card.AssignOwner(player);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => CardPileCmd.Generate(room.Engine.State, card, PileType.Deck));
    }

    [Fact]
    public async Task Add_DoesNotDispatchGeneratedCardHook()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("pillar-ordinary-add");
        await ApplyPillarAsync(room.Engine.State, player, 5m);
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        card.AssignOwner(player);

        CardPileCmd.Add(card, PileType.Hand);

        Assert.Equal(0, player.Creature.Block);
    }

    [Fact]
    public async Task AfterCardGenerated_UsesSnapshotForRunLevelLazyListenerEnumeration()
    {
        var runState = new LazyRunState();
        var combatState = new LazyCombatState(runState);
        var mutation = (GeneratedSnapshotMutationPower)ModelDb.Power<GeneratedSnapshotMutationPower>().MutableClone();
        var existing = (GeneratedSnapshotCountingCard)ModelDb.Card<GeneratedSnapshotCountingCard>().MutableClone();
        var added = (GeneratedSnapshotCountingCard)ModelDb.Card<GeneratedSnapshotCountingCard>().MutableClone();
        mutation.Mutation = () => runState.Listeners.Add(added);
        runState.Listeners.Add(mutation);
        runState.Listeners.Add(existing);

        await Hook.AfterCardGenerated(combatState, existing);

        Assert.Equal(1, existing.GeneratedCount);
        Assert.Equal(0, added.GeneratedCount);
    }

    [Fact]
    public async Task PillarOfCreation_StacksAsOneCounter_AndUsesCombinedAmountOnce()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("pillar-stack");
        await ApplyPillarAsync(room.Engine.State, player, 5m);
        await ApplyPillarAsync(room.Engine.State, player, 7m);

        await GenerateAsync<StrikeRegent>(room.Engine.State, player, PileType.Hand);

        PillarOfCreationPower power = player.Creature.Powers.OfType<PillarOfCreationPower>().Single();
        Assert.Equal(12, power.Amount);
        Assert.Equal(12, player.Creature.Block);
        Assert.Equal(PowerType.Buff, power.Type);
        Assert.Equal(PowerStackType.Counter, power.StackType);
    }

    private static async Task ApplyPillarAsync(ICombatState combatState, Player player, decimal amount)
    {
        await PowerCmd.Apply<PillarOfCreationPower>(combatState, player.Creature, amount, player.Creature, null);
    }

    private static async Task GenerateAsync<TCard>(ICombatState combatState, Player player, PileType pileType)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        await CardPileCmd.Generate(combatState, card, pileType);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
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

    private sealed class GeneratedSnapshotMutationPower : PowerModel
    {
        public Action? Mutation { get; set; }

        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public override Task AfterCardGenerated(CardModel card)
        {
            Mutation?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class LazyRunState : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public List<AbstractModel> Listeners { get; } = new();

        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState)
        {
            foreach (AbstractModel listener in Listeners)
            {
                yield return listener;
            }
        }

        public RunRngSet Rng { get; } = new("pillar-lazy-snapshot");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class LazyCombatState(IRunState runState) : ICombatState
    {
        public IEnumerable<AbstractModel> IterateHookListeners() => RunState.IterateHookListeners(this);

        public IRunState RunState { get; } = runState;

        public IReadOnlyList<Creature> Allies => Array.Empty<Creature>();

        public IReadOnlyList<Creature> Enemies => Array.Empty<Creature>();

        public IReadOnlyList<Creature> Creatures => Array.Empty<Creature>();

        public IReadOnlyList<Player> Players => Array.Empty<Player>();

        public IReadOnlyList<Creature> HittableEnemies => Array.Empty<Creature>();

        public CombatSide CurrentSide { get; set; }

        public int RoundNumber { get; set; } = 1;

        public IReadOnlyList<Creature> GetOpponentsOf(Creature creature) => Array.Empty<Creature>();

        public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side) => Array.Empty<Creature>();

        public bool ContainsCreature(Creature creature) => false;

        public bool IsLiveCombat() => true;
    }

    private sealed class GeneratedSnapshotCountingCard : CardModel
    {
        public int GeneratedCount { get; private set; }

        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Basic;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 0;

        public override Task AfterCardGenerated(CardModel card)
        {
            GeneratedCount++;
            return Task.CompletedTask;
        }
    }
}
