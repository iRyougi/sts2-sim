using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SignetRing : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override Task AfterObtained() => PlayerCmd.GainGold(888m, Owner);
}
