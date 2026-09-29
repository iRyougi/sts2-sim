using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PaelsBlood : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyHandDraw(Player player, decimal count) => player == Owner ? count + 1m : count;
}
