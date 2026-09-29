using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Glitter : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option)
    {
        if (!ReferenceEquals(player, Owner))
        {
            return null;
        }

        Glam glam = ModelDb.GetById<Glam>(ModelDb.GetId<Glam>());
        if (!glam.CanEnchant(option))
        {
            return null;
        }

        var clone = (CardModel)option.MutableClone();
        CardCmd.Enchant<Glam>(clone, 1m).GetAwaiter().GetResult();
        return clone;
    }
}
