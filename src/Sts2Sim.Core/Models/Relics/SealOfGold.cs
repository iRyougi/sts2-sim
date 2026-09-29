using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SealOfGold : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner || Owner.Gold < 3) return;
        Owner.PlayerCombatState!.GainEnergy(1m);
        await PlayerCmd.LoseGold(3m, Owner, GoldLossType.Lost);
    }
}
