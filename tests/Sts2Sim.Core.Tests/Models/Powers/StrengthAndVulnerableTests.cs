namespace Sts2Sim.Core.Tests.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public class StrengthAndVulnerableTests
{
    public StrengthAndVulnerableTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(StrengthPower), typeof(VulnerablePower) });
    }

    private sealed class FakeRunState : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => childCombatState?.Creatures.SelectMany(c => c.Powers).Cast<AbstractModel>() ?? Array.Empty<AbstractModel>();
        public RunRngSet Rng { get; } = new RunRngSet("strength_vulnerable_tests");
        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);
        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class FakeCombatState : ICombatState
    {
        public FakeCombatState(IRunState runState, params Creature[] creatures) { RunState = runState; CreatureList = creatures.ToList(); }
        public IEnumerable<AbstractModel> IterateHookListeners() => RunState.IterateHookListeners(this);
        public IRunState RunState { get; }
        public List<Creature> CreatureList { get; }
        public IReadOnlyList<Creature> Allies => CreatureList.Where(c => c.Side == CombatSide.Player).ToList();
        public IReadOnlyList<Creature> Enemies => CreatureList.Where(c => c.Side == CombatSide.Enemy).ToList();
        public IReadOnlyList<Creature> Creatures => CreatureList;
        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public IReadOnlyList<Creature> HittableEnemies => Enemies.Where(c => c.IsAlive).ToList();
        public CombatSide CurrentSide { get; set; }
        public int RoundNumber { get; set; } = 1;
        public IReadOnlyList<Creature> GetOpponentsOf(Creature creature) => creature.Side == CombatSide.Player ? Enemies : Allies;
        public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side) => side == CombatSide.Player ? Allies : Enemies;
        public bool ContainsCreature(Creature creature) => CreatureList.Contains(creature);
        public bool IsLiveCombat() => true;
    }

    [Fact]
    public async Task StrengthPower_AddsAmountToOwnersPoweredDamage()
    {
        var attacker = Creature.CreateStandaloneForTests(30, 30);
        var target = Creature.CreateStandaloneForTests(30, 30);
        var combatState = new FakeCombatState(new FakeRunState(), attacker, target);
        attacker.CombatState = combatState;
        target.CombatState = combatState;
        await PowerCmd.Apply<StrengthPower>(combatState, attacker, 3m, attacker, null);

        var results = await CreatureCmd.Damage(combatState, new[] { target }, 6m, ValueProp.Move, attacker, null, null);

        Assert.Equal(21, target.CurrentHp); // 30 - (6+3)
        Assert.Equal(9, results[0].UnblockedDamage);
    }

    [Fact]
    public async Task StrengthPower_DoesNotAffectUnpoweredDamage()
    {
        var attacker = Creature.CreateStandaloneForTests(30, 30);
        var target = Creature.CreateStandaloneForTests(30, 30);
        var combatState = new FakeCombatState(new FakeRunState(), attacker, target);
        attacker.CombatState = combatState;
        target.CombatState = combatState;
        await PowerCmd.Apply<StrengthPower>(combatState, attacker, 3m, attacker, null);

        var results = await CreatureCmd.Damage(combatState, new[] { target }, 6m, ValueProp.Unpowered, attacker, null, null);

        Assert.Equal(24, target.CurrentHp); // 30 - 6, Strength is inactive.
        Assert.Equal(6, results[0].UnblockedDamage);
    }

    [Fact]
    public async Task VulnerablePower_MultipliesDamageReceivedByOwner_ByOneAndAHalf()
    {
        var attacker = Creature.CreateStandaloneForTests(30, 30);
        var target = Creature.CreateStandaloneForTests(30, 30);
        var combatState = new FakeCombatState(new FakeRunState(), attacker, target);
        attacker.CombatState = combatState;
        target.CombatState = combatState;
        await PowerCmd.Apply<VulnerablePower>(combatState, target, 1m, attacker, null);

        var results = await CreatureCmd.Damage(combatState, new[] { target }, 6m, ValueProp.Move, attacker, null, null);

        Assert.Equal(21, target.CurrentHp); // 30 - (6*1.5)=30-9
        Assert.Equal(9, results[0].UnblockedDamage);
    }

    [Fact]
    public async Task VulnerablePower_NewPlayerDebuffSkipsFirstEnemyTurnEndThenTicksDown()
    {
        var target = Creature.CreateStandaloneForTests(30, 30);
        var combatState = new FakeCombatState(new FakeRunState(), target);
        target.CombatState = combatState;
        VulnerablePower power = (await PowerCmd.Apply<VulnerablePower>(combatState, target, 2m, null, null))!;

        await power.AfterSideTurnEnd(CombatSide.Player, Array.Empty<Creature>());
        Assert.Equal(2, power.Amount);

        await power.AfterSideTurnEnd(CombatSide.Enemy, Array.Empty<Creature>());
        Assert.Equal(2, power.Amount);
        Assert.False(power.SkipNextDurationTick);

        await power.AfterSideTurnEnd(CombatSide.Enemy, Array.Empty<Creature>());

        Assert.Equal(1, power.Amount);
    }
}
