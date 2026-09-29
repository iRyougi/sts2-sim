namespace Sts2Sim.Core.Tests.Commands;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public class CreatureCmdTests
{
    private sealed class FakeRunState(params AbstractModel[] listeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => listeners;

        public RunRngSet Rng { get; } = new("creature_cmd_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class MutableRunState(List<AbstractModel> listeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;

        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            listeners.ToArray();

        public RunRngSet Rng { get; } = new("creature_cmd_mutable_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class FakeCombatState(IRunState runState) : ICombatState
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

    private sealed class AddFlatDamage(decimal delta) : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public override decimal ModifyDamageAdditive(
            Creature? target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
        {
            return delta;
        }
    }

    private sealed class RemoveDamageModifierAfterDeath(
        Creature firstTarget,
        Creature secondTarget,
        Action removeListener) : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public override decimal ModifyDamageAdditive(
            Creature? target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource,
            CardPlay? cardPlay) => ReferenceEquals(target, secondTarget) ? -10m : 0m;

        public override Task AfterDeath(Creature target)
        {
            if (ReferenceEquals(target, firstTarget))
                removeListener();
            return Task.CompletedTask;
        }
    }

    private sealed class MultiplyBlock(decimal factor) : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public override decimal ModifyBlockMultiplicative(
            Creature target,
            decimal amount,
            ValueProp props,
            CardModel? cardSource,
            CardPlay? cardPlay)
        {
            return factor;
        }
    }

    private sealed class RemoveListenerDuringAdditive(Action removeListener) : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public override decimal ModifyDamageAdditive(
            Creature? target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource,
            CardPlay? cardPlay)
        {
            removeListener();
            return 0m;
        }
    }

    private sealed class MultiplicativeDamageProbe : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public int CallCount { get; private set; }

        public override decimal ModifyDamageMultiplicative(
            Creature? target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource,
            CardPlay? cardPlay)
        {
            CallCount++;
            return 2m;
        }
    }

    private sealed class RunLevelDamageProbe(List<string> calls) : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public override decimal ModifyDamageAdditive(
            Creature? target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource,
            CardPlay? cardPlay)
        {
            calls.Add("modify-damage");
            return 2m;
        }

        public override Task BeforeDamageReceived(
            Creature target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource)
        {
            calls.Add("before");
            return Task.CompletedTask;
        }

        public override decimal ModifyHpLostAfterOsty(
            Creature target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource)
        {
            calls.Add("modify-hp");
            return amount - 1m;
        }

        public override Task AfterDamageReceived(
            Creature target,
            DamageResult result,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource)
        {
            calls.Add("after");
            return Task.CompletedTask;
        }
    }

    private static Creature MakeCreature(int hp) => Creature.CreateStandaloneForTests(hp, hp);

    [Fact]
    public async Task Damage_AbsorbsBlockFirst_ThenAppliesRemainderToHp()
    {
        var combatState = new FakeCombatState(new FakeRunState());
        Creature target = MakeCreature(30);
        target.GainBlockInternal(5m);

        IReadOnlyList<DamageResult> results =
            await CreatureCmd.Damage(combatState, new[] { target }, 8m, ValueProp.Move, null, null, null);

        Assert.Equal(0, target.Block);
        Assert.Equal(27, target.CurrentHp);
        Assert.Equal(3, results[0].UnblockedDamage);
        Assert.Equal(5, results[0].BlockedDamage);
        Assert.False(results[0].WasFullyBlocked);

        target.GainBlockInternal(7m);
        DamageResult fullyBlocked = Assert.Single(await CreatureCmd.Damage(
            combatState, [target], 3m, ValueProp.Move, null, null, null));
        Assert.True(fullyBlocked.WasFullyBlocked);
        Assert.Equal(27, target.CurrentHp);
        DamageResult unblockable = Assert.Single(await CreatureCmd.Damage(
            combatState, [target], 1m, ValueProp.Unblockable, null, null, null));
        Assert.False(unblockable.WasFullyBlocked);
    }

    [Fact]
    public async Task Damage_ModifyDamageHookIncreasesFinalAmount()
    {
        var combatState = new FakeCombatState(new FakeRunState(new AddFlatDamage(4m)));
        Creature target = MakeCreature(30);

        IReadOnlyList<DamageResult> results =
            await CreatureCmd.Damage(combatState, new[] { target }, 6m, ValueProp.Move, null, null, null);

        Assert.Equal(20, target.CurrentHp);
        Assert.Equal(10, results[0].UnblockedDamage);
    }

    [Fact]
    public void ModifyDamage_ReenumeratesListenersBetweenPhases()
    {
        var listeners = new List<AbstractModel>();
        var multiplier = new MultiplicativeDamageProbe();
        var remover = new RemoveListenerDuringAdditive(() => listeners.Remove(multiplier));
        listeners.Add(remover);
        listeners.Add(multiplier);
        var combatState = new FakeCombatState(new MutableRunState(listeners));

        decimal result = Hook.ModifyDamage(
            combatState,
            null,
            null,
            10m,
            ValueProp.Move,
            null,
            null,
            out _);

        Assert.Equal(10m, result);
        Assert.Equal(0, multiplier.CallCount);
    }

    [Fact]
    public async Task Damage_ReportsKillAndOverkill_WhenLethal()
    {
        Creature target = MakeCreature(5);
        Creature secondTarget = MakeCreature(30);
        var listeners = new List<AbstractModel>();
        var modifier = new RemoveDamageModifierAfterDeath(
            target, secondTarget, () => listeners.Clear());
        listeners.Add(modifier);
        var combatState = new FakeCombatState(new MutableRunState(listeners));

        IReadOnlyList<DamageResult> results =
            await CreatureCmd.Damage(combatState, new[] { target, secondTarget }, 20m, ValueProp.Move, null, null, null);

        Assert.Equal(0, target.CurrentHp);
        Assert.True(results[0].WasTargetKilled);
        Assert.Equal(15, results[0].OverkillDamage);
        Assert.Equal(20, secondTarget.CurrentHp);
    }

    [Fact]
    public async Task GainBlock_AddsToCreatureBlock()
    {
        var combatState = new FakeCombatState(new FakeRunState());
        Creature creature = MakeCreature(30);

        decimal gained = await CreatureCmd.GainBlock(combatState, creature, 5m, ValueProp.Move, null, null);

        Assert.Equal(5, creature.Block);
        Assert.Equal(5m, gained);
    }

    [Fact]
    public async Task GainBlock_NeverGoesNegative_WhenModifierReducesBelowZero()
    {
        var combatState = new FakeCombatState(new FakeRunState(new MultiplyBlock(-10m)));
        Creature creature = MakeCreature(30);

        decimal gained = await CreatureCmd.GainBlock(combatState, creature, 5m, ValueProp.Move, null, null);

        Assert.Equal(0, creature.Block);
        Assert.Equal(0m, gained);
    }

    [Fact]
    public async Task RunLevelDamage_ConsumesBlockAndReportsBlockedAndUnblockedDamage()
    {
        var runState = new FakeRunState();
        Creature target = MakeCreature(30);
        target.GainBlockInternal(5m);

        DamageResult result = await CreatureCmd.Damage(runState, target, 8m, ValueProp.Move);

        Assert.Equal(0, target.Block);
        Assert.Equal(27, target.CurrentHp);
        Assert.Equal(5, result.BlockedDamage);
        Assert.Equal(3, result.UnblockedDamage);
    }

    [Fact]
    public async Task RunLevelDamage_RunsDamageHooksInSourceOrder()
    {
        var calls = new List<string>();
        var probe = new RunLevelDamageProbe(calls);
        probe.ExecutionFinished += _ => calls.Add("finished");
        var runState = new FakeRunState(probe);
        Creature target = MakeCreature(30);
        target.GainBlockInternal(3m);

        DamageResult result = await CreatureCmd.Damage(runState, target, 8m, ValueProp.Move);

        Assert.Equal(0, target.Block);
        Assert.Equal(24, target.CurrentHp);
        Assert.Equal(3, result.BlockedDamage);
        Assert.Equal(6, result.UnblockedDamage);
        Assert.Equal(
            ["modify-damage", "before", "finished", "modify-hp", "finished", "finished", "after", "finished"],
            calls);
    }

    [Fact]
    public async Task RunLevelDamage_NullArgumentsThrowBeforeMutationOrHooks()
    {
        var calls = new List<string>();
        var probe = new RunLevelDamageProbe(calls);
        var runState = new FakeRunState(probe);
        Creature target = MakeCreature(30);
        target.GainBlockInternal(5m);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreatureCmd.Damage(null!, target, 1m, ValueProp.Move));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreatureCmd.Damage(runState, null!, 1m, ValueProp.Move));

        Assert.Equal(30, target.CurrentHp);
        Assert.Equal(5, target.Block);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunLevelDamage_NegativeAmountThrowsBeforeMutationOrHooks()
    {
        var calls = new List<string>();
        var probe = new RunLevelDamageProbe(calls);
        var runState = new FakeRunState(probe);
        Creature target = MakeCreature(30);
        target.GainBlockInternal(5m);

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreatureCmd.Damage(runState, target, -1m, ValueProp.Move));

        Assert.Equal(30, target.CurrentHp);
        Assert.Equal(5, target.Block);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task GainMaxHp_NegativeAmount_ThrowsWithoutChangingHp()
    {
        Creature creature = MakeCreature(30);

        await Assert.ThrowsAsync<ArgumentException>(() => CreatureCmd.GainMaxHp(creature, -1m));

        Assert.Equal(30, creature.MaxHp);
        Assert.Equal(30, creature.CurrentHp);
    }
}
