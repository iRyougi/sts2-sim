using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Marker power read by <see cref="PoisonPower"/> to repeat poison triggers.</summary>
public sealed class AccelerantPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}
