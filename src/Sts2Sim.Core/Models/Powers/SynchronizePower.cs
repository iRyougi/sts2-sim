using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Powers;

public sealed class SynchronizePower : TemporaryFocusPower
{
    public override AbstractModel OriginModel => ModelDb.Card<Synchronize>();
}
