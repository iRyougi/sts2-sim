using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RadiantPearl : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player != Owner || player.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        var card = (Luminesce)ModelDb.Card<Luminesce>().MutableClone();
        card.AssignOwner(Owner);
        await CardPileCmd.Generate(Owner.Creature.CombatState!, card, PileType.Hand);
    }
}
