using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Ectoplasm : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyGoldGained(Player player, decimal amount) => player == Owner ? 0m : amount;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player == Owner ? amount + 1m : amount;
}
