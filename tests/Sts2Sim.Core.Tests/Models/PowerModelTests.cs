namespace Sts2Sim.Core.Tests.Models;

using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Exceptions;

[Collection("ModelDb")]
public class PowerModelTests
{
    private sealed class CounterPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Counter;
    }

    private sealed class NegativeAllowedPower : PowerModel
    {
        public override PowerType Type => PowerType.Debuff;
        public override PowerStackType StackType => PowerStackType.Counter;
        public override bool AllowNegative => true;
    }

    [Fact]
    public void ShouldRemoveDueToAmount_DefaultAllowNegativeFalse_TrueWhenAmountLessOrEqualZero()
    {
        var power = (CounterPower)new CounterPower().MutableClone();
        power.SetAmount(0);
        Assert.True(power.ShouldRemoveDueToAmount());

        power.SetAmount(-3);
        Assert.True(power.ShouldRemoveDueToAmount());

        power.SetAmount(2);
        Assert.False(power.ShouldRemoveDueToAmount());
    }

    [Fact]
    public void ShouldRemoveDueToAmount_AllowNegativeTrue_OnlyTrueAtExactlyZero()
    {
        var power = (NegativeAllowedPower)new NegativeAllowedPower().MutableClone();
        power.SetAmount(-5);
        Assert.False(power.ShouldRemoveDueToAmount());

        power.SetAmount(0);
        Assert.True(power.ShouldRemoveDueToAmount());
    }

    [Fact]
    public void ApplyInternal_SetsOwnerAndAmount_AndRegistersOnCreature()
    {
        var power = (CounterPower)new CounterPower().MutableClone();
        Creature target = Creature.CreateStandaloneForTests(30, 30);

        power.ApplyInternal(target, 3m);

        Assert.Equal(3, power.Amount);
        Assert.Contains(power, target.Powers);
    }

    [Fact]
    public void RemoveInternal_UnregistersFromCreature()
    {
        var power = (CounterPower)new CounterPower().MutableClone();
        Creature target = Creature.CreateStandaloneForTests(30, 30);
        power.ApplyInternal(target, 3m);

        power.RemoveInternal();

        Assert.DoesNotContain(power, target.Powers);
    }

    [Fact]
    public void SkipNextDurationTick_RejectsCanonicalMutation_AndIsPreservedByMutableClone()
    {
        var canonical = new CounterPower();

        Assert.Throws<CanonicalModelException>(() => canonical.SkipNextDurationTick = true);

        var mutable = (CounterPower)canonical.MutableClone();
        mutable.SkipNextDurationTick = true;
        var clone = (CounterPower)mutable.MutableClone();

        Assert.True(mutable.SkipNextDurationTick);
        Assert.True(clone.SkipNextDurationTick);
    }
}
