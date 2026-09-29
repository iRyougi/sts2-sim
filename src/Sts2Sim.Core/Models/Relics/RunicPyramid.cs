using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RunicPyramid : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool ShouldFlush(Player player)
    {
        if (player != Owner) return true;
        Flash();
        return false;
    }
}
