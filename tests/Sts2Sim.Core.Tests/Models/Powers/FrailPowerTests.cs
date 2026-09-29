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
public sealed class FrailPowerTests : IDisposable
{
    public FrailPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(FrailPower), typeof(TrainingDummy) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task GainBlock_PoweredMoveBlockReceivedByOwner_IsReducedToSeventyFivePercent()
    {
        (CombatState combatState, Creature owner) = CreateCombat("frail-powered-block");
        await PowerCmd.Apply<FrailPower>(combatState, owner, 2m, applier: null, cardSource: null);

        decimal gained = await CreatureCmd.GainBlock(
            combatState,
            owner,
            20m,
            ValueProp.Move,
            cardSource: null,
            cardPlay: null);

        Assert.Equal(15m, gained);
        Assert.Equal(15, owner.Block);
    }

    [Fact]
    public async Task GainBlock_UnpoweredBlock_IsNotReduced()
    {
        (CombatState combatState, Creature owner) = CreateCombat("frail-unpowered-block");
        await PowerCmd.Apply<FrailPower>(combatState, owner, 2m, applier: null, cardSource: null);

        decimal gained = await CreatureCmd.GainBlock(
            combatState,
            owner,
            20m,
            ValueProp.Move | ValueProp.Unpowered,
            cardSource: null,
            cardPlay: null);

        Assert.Equal(20m, gained);
        Assert.Equal(20, owner.Block);
    }

    [Fact]
    public async Task EnemyTurnEnd_ThroughHook_TicksDownAndRemovesFinalStack()
    {
        (CombatState combatState, Creature owner) = CreateCombat("frail-duration");
        FrailPower power = (await PowerCmd.Apply<FrailPower>(
            combatState,
            owner,
            1m,
            applier: null,
            cardSource: null))!;

        await Hook.AfterSideTurnEnd(combatState, CombatSide.Player, new[] { owner });
        Assert.Contains(power, owner.Powers);

        await Hook.AfterSideTurnEnd(combatState, CombatSide.Enemy, new[] { owner });
        Assert.DoesNotContain(power, owner.Powers);
    }

    private static (CombatState CombatState, Creature Owner) CreateCombat(string seed)
    {
        var runState = new RunState(seed, new Sts2Sim.Core.Content.Acts.Overgrowth());
        var combatState = new CombatState(runState);
        Creature owner = combatState.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);
        return (combatState, owner);
    }
}
