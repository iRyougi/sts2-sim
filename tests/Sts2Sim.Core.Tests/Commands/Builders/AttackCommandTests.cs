namespace Sts2Sim.Core.Tests.Commands.Builders;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public class AttackCommandTests
{
    private sealed class FakeMonster : MonsterModel
    {
        public override int MinInitialHp => 12;

        public override int MaxInitialHp => 12;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            throw new NotSupportedException("AttackCommandTests do not use monster move state.");
        }
    }

    private sealed class FakeRunState(RunRngSet? rng = null) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => Array.Empty<AbstractModel>();

        public RunRngSet Rng { get; } = rng ?? new RunRngSet("attack_command_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players { get; init; } = Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class FakeCombatState(IRunState runState) : ICombatState
    {
        public IRunState RunState { get; } = runState;

        public List<Creature> AlliesList { get; } = new();

        public List<Creature> EnemiesList { get; } = new();

        public IEnumerable<AbstractModel> IterateHookListeners() => RunState.IterateHookListeners(this);

        public IReadOnlyList<Creature> Allies => AlliesList;

        public IReadOnlyList<Creature> PlayerCreatures => AlliesList;

        public IReadOnlyList<Creature> Enemies => EnemiesList;

        public IReadOnlyList<Creature> Creatures => AlliesList.Concat(EnemiesList).ToList();

        public IReadOnlyList<Player> Players => RunState.Players;

        public IReadOnlyList<Creature> HittableEnemies => EnemiesList.Where(c => c.IsAlive).ToList();

        public CombatSide CurrentSide { get; set; }

        public int RoundNumber { get; set; } = 1;

        public IReadOnlyList<Creature> GetOpponentsOf(Creature creature)
        {
            return creature.Side == CombatSide.Player ? EnemiesList : AlliesList;
        }

        public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side)
        {
            return side == CombatSide.Player ? AlliesList : EnemiesList;
        }

        public bool ContainsCreature(Creature creature) => Creatures.Contains(creature);

        public bool IsLiveCombat() => true;
    }

    private static (FakeCombatState CombatState, Creature Attacker, FakeMonster Monster) MakeMonsterAttacker()
    {
        var runState = new FakeRunState();
        var combatState = new FakeCombatState(runState);
        var monster = (FakeMonster)new FakeMonster().MutableClone();
        var attacker = new Creature(monster, CombatSide.Enemy) { CombatState = combatState };
        monster.AssignCreature(attacker);
        combatState.EnemiesList.Add(attacker);
        return (combatState, attacker, monster);
    }

    [Fact]
    public async Task Execute_FromMonster_TargetsAllOpponents_AndDealsDamage()
    {
        (FakeCombatState combatState, _, FakeMonster monster) = MakeMonsterAttacker();
        Creature target = Creature.CreateStandaloneForTests(30, 30);
        target.CombatState = combatState;
        combatState.AlliesList.Add(target);

        await DamageCmd.Attack(6m).FromMonster(monster).Execute();

        Assert.Equal(24, target.CurrentHp);
    }

    [Fact]
    public async Task Execute_WithHitCount_HitsMultipleTimes()
    {
        (FakeCombatState combatState, _, FakeMonster monster) = MakeMonsterAttacker();
        Creature target = Creature.CreateStandaloneForTests(30, 30);
        target.CombatState = combatState;
        combatState.AlliesList.Add(target);

        await DamageCmd.Attack(2m).WithHitCount(3).FromMonster(monster).Execute();

        Assert.Equal(24, target.CurrentHp);
    }

    [Fact]
    public async Task Execute_StopsEarly_WhenAttackerDiesMidAttack()
    {
        (FakeCombatState combatState, Creature attacker, FakeMonster monster) = MakeMonsterAttacker();
        Creature target = Creature.CreateStandaloneForTests(30, 30);
        target.CombatState = combatState;
        combatState.AlliesList.Add(target);
        attacker.LoseHpInternal(999m, ValueProp.Move);

        AttackCommand result = await DamageCmd.Attack(6m).WithHitCount(3).FromMonster(monster).Execute();

        Assert.Empty(result.Results);
        Assert.Equal(30, target.CurrentHp);
    }
}
