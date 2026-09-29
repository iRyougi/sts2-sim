using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Pomander : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task AfterObtained()
    {
        CardModel? selected = (await CardSelectCmd.FromDeckForUpgrade(Owner, 1, this))
            .FirstOrDefault();
        if (selected is not null)
        {
            CardCmd.Upgrade(selected);
        }
    }
}
