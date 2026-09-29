using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PaelsClaw : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override async Task AfterObtained()
    {
        Goopy goopy = ModelDb.GetById<Goopy>(ModelDb.GetId<Goopy>());
        foreach (CardModel card in Owner.Deck.Cards.Where(goopy.CanEnchant).ToList())
            await CardCmd.Enchant<Goopy>(card, 1m);
    }
}
