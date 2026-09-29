namespace Sts2Sim.Core.Tests.Commands;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public class PowerCmdTests
{
    private sealed class CounterPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Counter;
    }

    private sealed class DebuffPower : PowerModel
    {
        public override PowerType Type => PowerType.Debuff;
        public override PowerStackType StackType => PowerStackType.Counter;
    }

    private sealed class InstancedPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Counter;
        public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    }

    public PowerCmdTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(CounterPower), typeof(DebuffPower), typeof(InstancedPower),
            typeof(Wriggler),
        });
    }

    private sealed class FakeRunState(params AbstractModel[] listeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => listeners;
        public RunRngSet Rng { get; } = new RunRngSet("power_cmd_tests");
        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);
        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class FakeCombatState : ICombatState
    {
        public FakeCombatState(IRunState runState) => RunState = runState;
        public IEnumerable<AbstractModel> IterateHookListeners() => RunState.IterateHookListeners(this);
        public IRunState RunState { get; }
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

    [Fact]
    public async Task Apply_NewPower_CreatesInstanceWithAppliedAmount()
    {
        var combatState = new FakeCombatState(new FakeRunState());
        Creature target = Creature.CreateStandaloneForTests(30, 30);

        CounterPower? power = await PowerCmd.Apply<CounterPower>(combatState, target, 3m, null, null);

        Assert.NotNull(power);
        Assert.Equal(3, power!.Amount);
        Assert.Contains(power, target.Powers);
    }

    [Fact]
    public async Task Apply_NewPlayerDebuff_SetsSkipNextDurationTick()
    {
        var combatState = new FakeCombatState(new FakeRunState());
        Creature target = Creature.CreateStandaloneForTests(30, 30);

        DebuffPower? power = await PowerCmd.Apply<DebuffPower>(combatState, target, 1m, null, null);

        Assert.NotNull(power);
        Assert.True(power!.SkipNextDurationTick);
    }

    [Fact]
    public async Task Apply_NewEnemyDebuff_DoesNotSetSkipNextDurationTick()
    {
        var combatState = new FakeCombatState(new FakeRunState());
        var monster = (Wriggler)ModelDb.Monster<Wriggler>().MutableClone();
        var target = new Creature(monster, CombatSide.Enemy);

        DebuffPower? power = await PowerCmd.Apply<DebuffPower>(combatState, target, 1m, null, null);

        Assert.NotNull(power);
        Assert.False(power!.SkipNextDurationTick);
    }

    [Fact]
    public async Task Apply_ExistingPlayerDebuff_DoesNotResetClearedSkipFlag()
    {
        var combatState = new FakeCombatState(new FakeRunState());
        Creature target = Creature.CreateStandaloneForTests(30, 30);
        DebuffPower power = (await PowerCmd.Apply<DebuffPower>(combatState, target, 1m, null, null))!;
        power.SkipNextDurationTick = false;

        DebuffPower? stacked = await PowerCmd.Apply<DebuffPower>(combatState, target, 1m, null, null);

        Assert.Same(power, stacked);
        Assert.Equal(2, power.Amount);
        Assert.False(power.SkipNextDurationTick);
    }

    [Fact]
    public async Task Apply_DefaultInstanceType_StacksOntoExistingInstance()
    {
        var combatState = new FakeCombatState(new FakeRunState());
        Creature target = Creature.CreateStandaloneForTests(30, 30);

        await PowerCmd.Apply<CounterPower>(combatState, target, 3m, null, null);
        await PowerCmd.Apply<CounterPower>(combatState, target, 2m, null, null);

        Assert.Single(target.Powers);
        Assert.Equal(5, target.Powers[0].Amount);
    }

    [Fact]
    public async Task Apply_InstancedType_AlwaysCreatesNewInstance()
    {
        var combatState = new FakeCombatState(new FakeRunState());
        Creature target = Creature.CreateStandaloneForTests(30, 30);

        await PowerCmd.Apply<InstancedPower>(combatState, target, 3m, null, null);
        await PowerCmd.Apply<InstancedPower>(combatState, target, 3m, null, null);

        Assert.Equal(2, target.Powers.Count);
    }

    [Fact]
    public async Task ModifyAmount_RemovesPower_WhenAmountReachesZero()
    {
        var combatState = new FakeCombatState(new FakeRunState());
        Creature target = Creature.CreateStandaloneForTests(30, 30);
        CounterPower power = (await PowerCmd.Apply<CounterPower>(combatState, target, 2m, null, null))!;

        await PowerCmd.ModifyAmount(combatState, power, -2m, null, null);

        Assert.Empty(target.Powers);
    }

    [Fact]
    public async Task TickDownDuration_DecrementsAmountByOne()
    {
        var combatState = new FakeCombatState(new FakeRunState());
        Creature target = Creature.CreateStandaloneForTests(30, 30);
        CounterPower power = (await PowerCmd.Apply<CounterPower>(combatState, target, 3m, null, null))!;

        await PowerCmd.TickDownDuration(combatState, power);

        Assert.Equal(2, power.Amount);

        // A listener snapshot can retain a power after its creature leaves combat.
        var realCombat = new CombatState(new RunState("detached-power-tick", new Overgrowth()));
        var monster = (Wriggler)ModelDb.Monster<Wriggler>().MutableClone();
        Creature detachedOwner = realCombat.AddMonster(monster, CombatSide.Enemy);
        CounterPower detachedPower = (await PowerCmd.Apply<CounterPower>(
            realCombat, detachedOwner, 2m, null, null))!;
        realCombat.RemoveCreature(detachedOwner);

        await PowerCmd.TickDownDuration(realCombat, detachedPower);

        Assert.Null(detachedOwner.CombatState);
        Assert.Equal(2, detachedPower.Amount);
    }

    [Fact]
    public async Task TickDownDuration_SkipFlagConsumesFirstTick_ThenRemovesAtZero()
    {
        var combatState = new FakeCombatState(new FakeRunState());
        Creature target = Creature.CreateStandaloneForTests(30, 30);
        DebuffPower power = (await PowerCmd.Apply<DebuffPower>(combatState, target, 1m, null, null))!;

        await PowerCmd.TickDownDuration(combatState, power);

        Assert.Equal(1, power.Amount);
        Assert.False(power.SkipNextDurationTick);

        await PowerCmd.TickDownDuration(combatState, power);

        Assert.DoesNotContain(power, target.Powers);
    }
}
