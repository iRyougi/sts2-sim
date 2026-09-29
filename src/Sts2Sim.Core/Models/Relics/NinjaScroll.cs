using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class NinjaScroll : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player != Owner || Owner.PlayerCombatState?.TurnNumber > 1)
        {
            return;
        }

        await Shiv.CreateInHand(Owner, 3, Owner.Creature.CombatState!);
    }
}
