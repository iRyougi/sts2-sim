namespace Sts2Sim.Core.Tests.Hooks;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public class DamageBlockHookTests
{
    private sealed class FakeRunState(params AbstractModel[] listeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => listeners;

        public RunRngSet Rng { get; } = new RunRngSet("damage_block_hook_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class FakeCombatState : ICombatState
    {
        public FakeCombatState(IRunState runState)
        {
            RunState = runState;
        }

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

    private sealed class ProbeModel : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public List<string>? SharedLog { get; init; }

        public string Name { get; init; } = "probe";

        public bool AllowClearBlock { get; init; } = true;

        public bool AllowHitting { get; init; } = true;

        public bool StopsCombatFromEnding { get; init; }

        public Func<decimal, decimal>? OnModifyDamageAdditive { get; init; }

        public Func<decimal, decimal>? OnModifyDamageMultiplicative { get; init; }

        public decimal? DamageCap { get; init; }

        public Func<decimal, decimal>? OnModifyHpLost { get; init; }

        public Func<decimal, decimal>? OnModifyBlockAdditive { get; init; }

        public Func<decimal, decimal>? OnModifyBlockMultiplicative { get; init; }

        private void Log(string suffix = "") => SharedLog?.Add(suffix.Length == 0 ? Name : $"{Name}:{suffix}");

        public override Task BeforeAttack(AttackCommand command)
        {
            Log();
            return Task.CompletedTask;
        }

        public override Task AfterAttack(AttackCommand command)
        {
            Log();
            return Task.CompletedTask;
        }

        public override int ModifyAttackHitCount(AttackCommand command, int hitCount) => hitCount;

        public override Task BeforeDamageReceived(
            Creature target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource)
        {
            Log();
            return Task.CompletedTask;
        }

        public override Task AfterDamageReceived(
            Creature target,
            DamageResult result,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource)
        {
            Log();
            return Task.CompletedTask;
        }

        public override decimal ModifyDamageAdditive(
            Creature? target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource,
            CardPlay? cardPlay)
        {
            return OnModifyDamageAdditive?.Invoke(amount) ?? 0m;
        }

        public override decimal ModifyDamageMultiplicative(
            Creature? target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource,
            CardPlay? cardPlay)
        {
            return OnModifyDamageMultiplicative?.Invoke(amount) ?? 1m;
        }

        public override decimal ModifyDamageCap(
            Creature? target,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource,
            CardPlay? cardPlay)
        {
            return DamageCap ?? decimal.MaxValue;
        }

        public override decimal ModifyHpLost(
            Creature target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource)
        {
            return OnModifyHpLost?.Invoke(amount) ?? amount;
        }

        public override Task BeforeBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
        {
            Log();
            return Task.CompletedTask;
        }

        public override Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
        {
            Log();
            return Task.CompletedTask;
        }

        public override decimal ModifyBlockAdditive(
            Creature target,
            decimal amount,
            ValueProp props,
            CardModel? cardSource,
            CardPlay? cardPlay)
        {
            return OnModifyBlockAdditive?.Invoke(amount) ?? 0m;
        }

        public override decimal ModifyBlockMultiplicative(
            Creature target,
            decimal amount,
            ValueProp props,
            CardModel? cardSource,
            CardPlay? cardPlay)
        {
            return OnModifyBlockMultiplicative?.Invoke(amount) ?? 1m;
        }

        public override bool ShouldClearBlock(Creature creature)
        {
            Log();
            return AllowClearBlock;
        }

        public override Task AfterBlockCleared(Creature creature)
        {
            Log();
            return Task.CompletedTask;
        }

        public override bool ShouldAllowHitting(Creature creature)
        {
            Log();
            return AllowHitting;
        }

        public override bool ShouldStopCombatFromEnding() => StopsCombatFromEnding;
    }

    private static Creature MakeCreature(int hp = 50) => Creature.CreateStandaloneForTests(hp, hp);

    [Fact]
    public void ModifyDamage_AppliesAdditiveThenMultiplicativeThenCap_InThatOrder()
    {
        var strength = new ProbeModel { OnModifyDamageAdditive = v => 3m };
        var vulnerable = new ProbeModel { OnModifyDamageMultiplicative = v => 1.5m };
        var cap = new ProbeModel { DamageCap = 10m };
        var combatState = new FakeCombatState(new FakeRunState(strength, vulnerable, cap));

        decimal result = Hook.ModifyDamage(
            combatState,
            MakeCreature(),
            MakeCreature(),
            6m,
            ValueProp.Move,
            null,
            null,
            out IEnumerable<AbstractModel> modifiers);

        Assert.Equal(10m, result);
        Assert.Contains(modifiers, modifier => ReferenceEquals(strength, modifier));
        Assert.Contains(modifiers, modifier => ReferenceEquals(vulnerable, modifier));
        Assert.Contains(modifiers, modifier => ReferenceEquals(cap, modifier));
    }

    [Fact]
    public void ModifyDamage_NeverGoesBelowZero()
    {
        var negator = new ProbeModel { OnModifyDamageAdditive = v => -100m };
        var combatState = new FakeCombatState(new FakeRunState(negator));

        decimal result = Hook.ModifyDamage(combatState, MakeCreature(), MakeCreature(), 6m, ValueProp.Move, null, null, out _);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void ModifyHpLost_ChainsThroughListeners_AndTracksModifiersByTruncatedDelta()
    {
        var noop = new ProbeModel { OnModifyHpLost = v => v };
        var halver = new ProbeModel { OnModifyHpLost = v => v / 2m };
        var combatState = new FakeCombatState(new FakeRunState(noop, halver));

        decimal result = Hook.ModifyHpLost(
            combatState,
            MakeCreature(),
            10m,
            ValueProp.Move,
            null,
            null,
            out IEnumerable<AbstractModel> modifiers);

        Assert.Equal(5m, result);
        Assert.DoesNotContain(modifiers, modifier => ReferenceEquals(noop, modifier));
        Assert.Contains(modifiers, modifier => ReferenceEquals(halver, modifier));
    }

    [Fact]
    public void ModifyBlock_AppliesAdditiveThenMultiplicative_AndFloorsAtZero()
    {
        var addFive = new ProbeModel { OnModifyBlockAdditive = v => 5m };
        var combatState = new FakeCombatState(new FakeRunState(addFive));

        decimal result = Hook.ModifyBlock(
            combatState,
            MakeCreature(),
            3m,
            ValueProp.Move,
            null,
            null,
            out IEnumerable<AbstractModel> modifiers);

        Assert.Equal(8m, result);
        Assert.Contains(modifiers, modifier => ReferenceEquals(addFive, modifier));
    }

    [Fact]
    public void ShouldClearBlock_AndFold_ShortCircuitsOnFalse()
    {
        var log = new List<string>();
        var allow = new ProbeModel { Name = "allow", SharedLog = log, AllowClearBlock = true };
        var veto = new ProbeModel { Name = "veto", SharedLog = log, AllowClearBlock = false };
        var after = new ProbeModel { Name = "after", SharedLog = log, AllowClearBlock = true };
        var combatState = new FakeCombatState(new FakeRunState(allow, veto, after));

        bool result = Hook.ShouldClearBlock(combatState, MakeCreature());

        Assert.False(result);
        Assert.Equal(new[] { "allow", "veto" }, log);
    }

    [Fact]
    public void ShouldAllowHitting_AndFold_ShortCircuitsOnFalse()
    {
        var log = new List<string>();
        var allow = new ProbeModel { Name = "allow", SharedLog = log, AllowHitting = true };
        var veto = new ProbeModel { Name = "veto", SharedLog = log, AllowHitting = false };
        var combatState = new FakeCombatState(new FakeRunState(allow, veto));

        Assert.False(Hook.ShouldAllowHitting(combatState, MakeCreature()));
        Assert.Equal(new[] { "allow", "veto" }, log);
    }

    [Fact]
    public void ShouldStopCombatFromEnding_OrFold_ReturnsTrueIfAnyListenerStops()
    {
        var combatState = new FakeCombatState(new FakeRunState(new ProbeModel(), new ProbeModel { StopsCombatFromEnding = true }));

        Assert.True(Hook.ShouldStopCombatFromEnding(combatState));
        Assert.False(Hook.ShouldStopCombatFromEnding(new FakeCombatState(new FakeRunState(new ProbeModel()))));
    }

    [Fact]
    public async Task NotifyHooks_FireForEachListener()
    {
        var log = new List<string>();
        var a = new ProbeModel { Name = "a", SharedLog = log };
        var combatState = new FakeCombatState(new FakeRunState(a));
        var command = new AttackCommand(6m);

        await Hook.BeforeAttack(combatState, command);
        await Hook.AfterAttack(combatState, command);
        await Hook.BeforeDamageReceived(combatState, MakeCreature(), 6m, ValueProp.Move, null, null);
        await Hook.AfterDamageReceived(combatState, MakeCreature(), new DamageResult(MakeCreature(), ValueProp.Move), ValueProp.Move, null, null);
        await Hook.BeforeBlockGained(combatState, MakeCreature(), 5m, ValueProp.Move, null);
        await Hook.AfterBlockGained(combatState, MakeCreature(), 5m, ValueProp.Move, null);
        await Hook.AfterBlockCleared(combatState, MakeCreature());

        Assert.Equal(7, log.Count);
    }
}
