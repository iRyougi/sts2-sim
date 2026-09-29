using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class YummyCookie : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override async Task AfterObtained()
    {
        foreach (CardModel card in (await CardSelectCmd.FromDeckForUpgrade(Owner, 4, this))) CardCmd.Upgrade(card);
    }
}
