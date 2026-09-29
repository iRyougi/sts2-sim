using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Sozu : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool ShouldProcurePotion(PotionModel potion, Player player) => player != Owner;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player == Owner ? amount + 1m : amount;
}
