using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BlessedAntler : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player == Owner ? amount + 1m : amount;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player != Owner || Owner.PlayerCombatState?.TurnNumber != 1 || Owner.Creature.CombatState is null) return;
        for (int index = 0; index < 3; index++)
        {
            var dazed = (Dazed)ModelDb.Card<Dazed>().MutableClone();
            dazed.AssignOwner(Owner);
            await CardPileCmd.Generate(Owner.Creature.CombatState, dazed, PileType.Draw, CardPilePosition.Random);
        }
    }
}
