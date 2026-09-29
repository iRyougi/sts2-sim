namespace Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Entities.Powers;
public sealed class BackAttackLeftPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}
