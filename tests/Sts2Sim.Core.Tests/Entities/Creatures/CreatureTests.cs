namespace Sts2Sim.Core.Tests.Entities.Creatures;

using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

public class CreatureTests
{
    private static Creature MakeStandalone(int hp, int maxHp = -1) => Creature.CreateStandaloneForTests(hp, maxHp < 0 ? hp : maxHp);

    [Fact]
    public void DamageBlockInternal_AbsorbsUpToCurrentBlock()
    {
        Creature creature = MakeStandalone(50);
        creature.GainBlockInternal(10m);

        decimal blocked = creature.DamageBlockInternal(6m, ValueProps.ValueProp.Move);

        Assert.Equal(6m, blocked);
        Assert.Equal(4, creature.Block);
    }

    [Fact]
    public void DamageBlockInternal_UnblockableFlag_IgnoresBlock()
    {
        Creature creature = MakeStandalone(50);
        creature.GainBlockInternal(10m);

        decimal blocked = creature.DamageBlockInternal(6m, ValueProp.Unblockable);

        Assert.Equal(0m, blocked);
        Assert.Equal(10, creature.Block);
    }

    [Fact]
    public void LoseHpInternal_ClampsAtZero_AndReportsOverkill()
    {
        Creature creature = MakeStandalone(10);

        DamageResult result = creature.LoseHpInternal(15m, ValueProp.Move);

        Assert.Equal(0, creature.CurrentHp);
        Assert.Equal(10, result.UnblockedDamage);
        Assert.True(result.WasTargetKilled);
        Assert.Equal(5, result.OverkillDamage);
    }

    [Fact]
    public void LoseHpInternal_NonLethal_ReportsNoOverkillAndNotKilled()
    {
        Creature creature = MakeStandalone(10);

        DamageResult result = creature.LoseHpInternal(3m, ValueProp.Move);

        Assert.Equal(7, creature.CurrentHp);
        Assert.Equal(3, result.UnblockedDamage);
        Assert.False(result.WasTargetKilled);
        Assert.Equal(0, result.OverkillDamage);
    }

    [Fact]
    public void GainAndLoseBlockInternal_ClampToNonNegative()
    {
        Creature creature = MakeStandalone(10);
        creature.GainBlockInternal(5m);
        creature.LoseBlockInternal(20m);

        Assert.Equal(0, creature.Block);
    }

    [Fact]
    public void HealInternal_CapsAtMaxHp()
    {
        Creature creature = MakeStandalone(5, 10);

        creature.HealInternal(100m);

        Assert.Equal(10, creature.CurrentHp);
    }

    [Fact]
    public void SetMaxHpInternal_ClampsCurrentHpDownWhenNewMaxIsLower()
    {
        Creature creature = MakeStandalone(50);

        creature.SetMaxHpInternal(30m);

        Assert.Equal(30, creature.MaxHp);
        Assert.Equal(30, creature.CurrentHp);
    }

    [Fact]
    public void PowersInternal_AddAndRemove_AreVisibleInPowersList()
    {
        Creature creature = MakeStandalone(10);
        var power = new FakePower();

        creature.ApplyPowerInternal(power);
        Assert.Single(creature.Powers);

        creature.RemovePowerInternal(power);
        Assert.Empty(creature.Powers);
    }

    [Fact]
    public void IsAlive_TracksCurrentHp()
    {
        Creature creature = MakeStandalone(1);
        Assert.True(creature.IsAlive);

        creature.LoseHpInternal(1m, ValueProp.Move);
        Assert.False(creature.IsAlive);
        Assert.True(creature.IsDead);
    }

    private sealed class FakePower : Sts2Sim.Core.Models.PowerModel
    {
        public override Sts2Sim.Core.Entities.Powers.PowerType Type => Sts2Sim.Core.Entities.Powers.PowerType.Buff;

        public override Sts2Sim.Core.Entities.Powers.PowerStackType StackType => Sts2Sim.Core.Entities.Powers.PowerStackType.Counter;

        public override bool ShouldReceiveCombatHooks => true;
    }
}
