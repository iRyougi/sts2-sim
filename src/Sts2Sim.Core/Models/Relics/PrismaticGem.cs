using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PrismaticGem : RelicModel
{
    // Deviation #226: future rewards should union every unlocked character card pool; the simulator lacks both the unlocked-character flags and the reward-pool union hook.
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player == Owner ? amount + 1m : amount;
}
