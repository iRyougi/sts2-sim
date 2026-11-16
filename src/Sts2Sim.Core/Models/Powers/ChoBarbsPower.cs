using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>由 ChoBarbs 施加的临时荆棘能力（原版 <c>ChoBarbsPower</c>）。</summary>
public sealed class ChoBarbsPower : TemporaryThornsPower
{
    public override AbstractModel OriginModel => ModelDb.Card<ChoBarbs>();
}
