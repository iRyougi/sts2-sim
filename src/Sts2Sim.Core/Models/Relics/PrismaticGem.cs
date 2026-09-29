using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PrismaticGem : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player == Owner ? amount + 1m : amount;

    public override CardCreationOptions ModifyCardRewardCreationOptions(Player player, CardCreationOptions options)
    {
        if (Owner != player) return options;
        if (options.Flags.HasFlag(CardCreationFlags.NoCardPoolModifications)) return options;
        if (!options.Flags.HasFlag(CardCreationFlags.IsCardReward)) return options;
        if (options.CardPools.All(pool => pool.IsColorless)) return options;
        return options.WithCardPools(player.UnlockState.CharacterCardPools.Union(options.CardPools));
    }
}
