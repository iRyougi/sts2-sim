using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class GremlinHorn : RelicModel
{
    public override Task AfterDeath(Creature target, bool wasRemovalPrevented) => AfterDeath(target);

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterDeath(Creature target)
    {
        if (target.Side == Owner.Creature.Side)
        {
            return;
        }

        Owner.PlayerCombatState!.GainEnergy(1m);
        await CardPileCmd.Draw(
            Owner.Creature.CombatState!,
            1,
            Owner,
            fromHandDraw: false);
    }
}
