using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class StoneHumidifier : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override Task AfterRestSiteHeal(Player player, bool isMimicked) =>
        ReferenceEquals(player, Owner)
            ? CreatureCmd.GainMaxHp(Owner.Creature, 5m)
            : Task.CompletedTask;
}
