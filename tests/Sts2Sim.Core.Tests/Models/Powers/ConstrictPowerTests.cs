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
public sealed class ConstrictPowerTests : IDisposable
{
    public ConstrictPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(ConstrictPower), typeof(TrainingDummy) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task OwnersSideTurnEnd_ThroughHook_DealsUnpoweredBlockableDamageToOwner()
    {
        (CombatState combatState, Creature owner, Creature applier, _) = CreateCombat("constrict-damage");
        await PowerCmd.Apply<ConstrictPower>(combatState, owner, 5m, applier, cardSource: null);
        await CreatureCmd.GainBlock(combatState, owner, 2m, ValueProp.Move, cardSource: null, cardPlay: null);
        int hpBefore = owner.CurrentHp;

        await Hook.AfterSideTurnEnd(combatState, CombatSide.Player, new[] { owner });

        Assert.Equal(0, owner.Block);
        Assert.Equal(hpBefore - 3, owner.CurrentHp);
    }

    [Fact]
    public async Task ApplierDeath_ThroughDamageLifecycle_RemovesConstrictButOtherDeathDoesNot()
    {
        (CombatState combatState, Creature owner, Creature applier, Creature other) = CreateCombat("constrict-applier");
        ConstrictPower constrict = (await PowerCmd.Apply<ConstrictPower>(
            combatState,
            owner,
            5m,
            applier,
            cardSource: null))!;

        await CreatureCmd.Damage(
            combatState,
            new[] { other },
            999m,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: owner,
            cardSource: null,
            cardPlay: null);
        Assert.Contains(constrict, owner.Powers);

        await CreatureCmd.Damage(
            combatState,
            new[] { applier },
            999m,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: owner,
            cardSource: null,
            cardPlay: null);

        Assert.DoesNotContain(constrict, owner.Powers);
    }

    private static (CombatState CombatState, Creature Owner, Creature Applier, Creature Other) CreateCombat(string seed)
    {
        var runState = new RunState(seed, new Sts2Sim.Core.Content.Acts.Overgrowth());
        var combatState = new CombatState(runState);
        Creature owner = AddDummy(combatState, CombatSide.Player);
        Creature applier = AddDummy(combatState, CombatSide.Enemy);
        Creature other = AddDummy(combatState, CombatSide.Enemy);
        return (combatState, owner, applier, other);
    }

    private static Creature AddDummy(CombatState combatState, CombatSide side)
    {
        return combatState.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            side);
    }
}
