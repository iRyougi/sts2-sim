namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public class TrainingDummyTests
{
    public TrainingDummyTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(TrainingDummy) });
    }

    private sealed class FakeRunState : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => Array.Empty<AbstractModel>();

        public RunRngSet Rng { get; } = new("training_dummy_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class FakeCombatState : ICombatState
    {
        public FakeCombatState(IRunState runState, Creature ally, Creature enemy)
        {
            RunState = runState;
            Allies = new[] { ally };
            Enemies = new[] { enemy };
        }

        public IEnumerable<AbstractModel> IterateHookListeners() => RunState.IterateHookListeners(this);

        public IRunState RunState { get; }

        public IReadOnlyList<Creature> Allies { get; }

        public IReadOnlyList<Creature> Enemies { get; }

        public IReadOnlyList<Creature> Creatures => Allies.Concat(Enemies).ToList();

        public IReadOnlyList<Player> Players => Array.Empty<Player>();

        public IReadOnlyList<Creature> HittableEnemies => Enemies.Where(c => c.IsAlive).ToList();

        public CombatSide CurrentSide { get; set; }

        public int RoundNumber { get; set; } = 1;

        public IReadOnlyList<Creature> GetOpponentsOf(Creature creature) => creature.Side == CombatSide.Player ? Enemies : Allies;

        public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side) => side == CombatSide.Player ? Allies : Enemies;

        public bool ContainsCreature(Creature creature) => Creatures.Contains(creature);

        public bool IsLiveCombat() => true;
    }

    private sealed class ThrowingMonster : MonsterModel
    {
        public override int MinInitialHp => 10;

        public override int MaxInitialHp => 10;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            var throwingMove = new MoveState("THROW", _ => throw new InvalidOperationException("move failed"));
            var followUpMove = new MoveState("FOLLOW_UP", _ => Task.CompletedTask);
            throwingMove.FollowUpState = followUpMove;
            followUpMove.FollowUpState = followUpMove;
            return new MonsterMoveStateMachine(new[] { throwingMove, followUpMove }, throwingMove);
        }
    }

    [Fact]
    public void MinAndMaxInitialHp_Are20()
    {
        TrainingDummy dummy = ModelDb.Monster<TrainingDummy>();

        Assert.Equal(20, dummy.MinInitialHp);
        Assert.Equal(20, dummy.MaxInitialHp);
    }

    [Fact]
    public async Task PerformMove_DealsEightDamage_ToAllyCreature()
    {
        var runState = new FakeRunState();
        var dummy = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();
        var dummyCreature = new Creature(dummy, CombatSide.Enemy);
        var target = Creature.CreateStandaloneForTests(75, 75);
        var combatState = new FakeCombatState(runState, target, dummyCreature);
        dummy.AssignCreature(dummyCreature);
        dummyCreature.CombatState = combatState;
        target.CombatState = combatState;
        dummy.RunRng = runState.Rng;
        dummy.SetUpForCombat();
        dummy.RollMove(new[] { target });

        await dummy.PerformMove();

        Assert.Equal(67, target.CurrentHp);
    }

    [Fact]
    public async Task PerformMove_WhenMoveThrows_ResetsPerformingFlag_AndDoesNotAdvanceMoveState()
    {
        var runState = new FakeRunState();
        var monster = (ThrowingMonster)new ThrowingMonster().MutableClone();
        var monsterCreature = new Creature(monster, CombatSide.Enemy);
        var target = Creature.CreateStandaloneForTests(75, 75);
        var combatState = new FakeCombatState(runState, target, monsterCreature);
        monster.AssignCreature(monsterCreature);
        monsterCreature.CombatState = combatState;
        target.CombatState = combatState;
        monster.RunRng = runState.Rng;
        monster.SetUpForCombat();
        monster.RollMove(new[] { target });

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => monster.PerformMove());
        MoveState nextMoveAfterFailure = monster.MoveStateMachine!.RollMove(new[] { target }, monsterCreature, runState.Rng.MonsterAi);

        Assert.Equal("move failed", error.Message);
        Assert.False(monster.IsPerformingMove);
        Assert.Equal("THROW", nextMoveAfterFailure.Id);
    }

    [Fact]
    public void RollMove_AlwaysReturnsTheSamePokeMove_AcrossManyRolls()
    {
        var runState = new FakeRunState();
        var dummy = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();
        var dummyCreature = new Creature(dummy, CombatSide.Enemy);
        var target = Creature.CreateStandaloneForTests(75, 75);
        var combatState = new FakeCombatState(runState, target, dummyCreature);
        dummy.AssignCreature(dummyCreature);
        dummyCreature.CombatState = combatState;
        dummy.RunRng = runState.Rng;
        dummy.SetUpForCombat();

        for (int i = 0; i < 5; i++)
        {
            dummy.RollMove(new[] { target });
            Assert.Equal("POKE", dummy.NextMove!.Id);
            dummy.MoveStateMachine!.OnMovePerformed(dummy.NextMove);
        }
    }
}
