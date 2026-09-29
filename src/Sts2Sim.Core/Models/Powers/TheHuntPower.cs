using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Visual marker that The Hunt achieved Fatal; reward state remains on the card that dealt the fatal damage.</summary>
public sealed class TheHuntPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}
