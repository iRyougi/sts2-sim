namespace Sts2Sim.Core.Tests.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class MinionPowerTests : IDisposable
{
    public MinionPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(MinionPower), typeof(TrainingDummy) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Apply_ThroughPowerCmd_InstallsStableBuffSingleMarker()
    {
        var runState = new RunState("minion-marker", new Sts2Sim.Core.Content.Acts.Overgrowth());
        var combatState = new CombatState(runState);
        Creature owner = combatState.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);

        MinionPower? minion = await PowerCmd.Apply<MinionPower>(
            combatState,
            owner,
            1m,
            applier: owner,
            cardSource: null);

        Assert.NotNull(minion);
        Assert.Contains(minion!, owner.Powers);
        Assert.Equal(PowerType.Buff, minion!.Type);
        Assert.Equal(PowerStackType.Single, minion.StackType);
    }

    [Fact]
    public void OwnerDeath_DoesNotRemoveMinionMarker()
    {
        Assert.False(ModelDb.Power<MinionPower>().ShouldPowerBeRemovedAfterOwnerDeath());
    }

    [Fact]
    public async Task OwnerDeath_ThroughDamageResolution_RetainsMinionMarker()
    {
        var runState = new RunState("minion-owner-death", new Sts2Sim.Core.Content.Acts.Overgrowth());
        var combatState = new CombatState(runState);
        Creature owner = combatState.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);
        MinionPower? minion = await PowerCmd.Apply<MinionPower>(combatState, owner, 1m, owner, null);

        await CreatureCmd.Damage(
            combatState,
            new[] { owner },
            owner.CurrentHp,
            Sts2Sim.Core.ValueProps.ValueProp.Unblockable,
            null,
            null,
            null);

        Assert.True(owner.IsDead);
        Assert.Contains(minion!, owner.Powers);
    }
}
