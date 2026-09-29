namespace Sts2Sim.Core.Tests.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public class WeakPowerTests : IDisposable
{
    public WeakPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(WeakPower), typeof(TrainingDummy) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task ModifyDamageMultiplicative_AppliesSeventyFivePercent_ForPoweredAttackFromOwner()
    {
        var runState = new RunState("weak-power-a", new Sts2Sim.Core.Content.Acts.Overgrowth());
        var combatState = new CombatState(runState);
        Creature attacker = combatState.AddMonster((TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(), CombatSide.Enemy);
        Creature target = combatState.AddMonster((TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(), CombatSide.Enemy);
        await PowerCmd.Apply<WeakPower>(combatState, attacker, 1m, applier: null, cardSource: null);
        WeakPower weak = attacker.Powers.OfType<WeakPower>().Single();

        decimal factor = weak.ModifyDamageMultiplicative(target, 10m, ValueProp.Move, attacker, cardSource: null, cardPlay: null);

        Assert.Equal(0.75m, factor);
    }

    [Fact]
    public async Task AfterSideTurnEnd_ExpiresOneStackWithoutInvalidatingCombatHookEnumeration()
    {
        var runState = new RunState("weak-power-c", new Sts2Sim.Core.Content.Acts.Overgrowth());
        var combatState = new CombatState(runState);
        Creature owner = combatState.AddMonster((TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(), CombatSide.Enemy);
        await PowerCmd.Apply<WeakPower>(combatState, owner, 1m, applier: null, cardSource: null);

        await Hook.AfterSideTurnEnd(combatState, CombatSide.Enemy, new[] { owner });

        Assert.DoesNotContain(owner.Powers, power => power is WeakPower);
    }
    [Fact]
    public async Task AfterSideTurnEnd_TicksDownOnlyAtEndOfEnemyTurn()
    {
        var runState = new RunState("weak-power-b", new Sts2Sim.Core.Content.Acts.Overgrowth());
        var combatState = new CombatState(runState);
        Creature owner = combatState.AddMonster((TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(), CombatSide.Enemy);
        WeakPower weak = (await PowerCmd.Apply<WeakPower>(combatState, owner, 2m, applier: null, cardSource: null))!;

        await weak.AfterSideTurnEnd(CombatSide.Player, Array.Empty<Creature>());
        Assert.Equal(2m, weak.Amount);

        await weak.AfterSideTurnEnd(CombatSide.Enemy, Array.Empty<Creature>());
        Assert.Equal(1m, weak.Amount);
    }
}
