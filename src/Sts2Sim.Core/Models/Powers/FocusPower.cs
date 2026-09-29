using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class FocusPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => true;

    public override decimal ModifyOrbValue(OrbModel orb, decimal value) =>
        Owner.Player != orb.Owner ? value : Math.Max(value + Amount, 0m);
}
