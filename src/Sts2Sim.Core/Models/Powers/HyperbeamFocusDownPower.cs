using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Powers;

public sealed class HyperbeamFocusDownPower : TemporaryFocusPower
{
    public override AbstractModel OriginModel => ModelDb.Card<Hyperbeam>();
    protected override bool IsPositive => false;
}
