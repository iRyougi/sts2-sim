using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class UnceasingTop : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterHandEmptied(Player player)
    {
        if (player == Owner)
        {
            await CardPileCmd.Draw(Owner.Creature.CombatState!, 1, Owner, fromHandDraw: false);
        }
    }
}
