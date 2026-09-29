using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BiiigHug : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override async Task AfterObtained()
    {
        foreach (CardModel card in (await CardSelectCmd.FromDeckForRemoval(Owner, 4, this)).ToList())
            await CardPileCmd.RemoveFromDeck(Owner, card);
    }
    public override async Task AfterShuffle(Player player)
    {
        if (player != Owner) return;
        var soot = (Soot)ModelDb.Card<Soot>().MutableClone();
        soot.AssignOwner(Owner);
        await CardPileCmd.Generate(Owner.Creature.CombatState!, soot, PileType.Draw, CardPilePosition.Random);
    }
}
