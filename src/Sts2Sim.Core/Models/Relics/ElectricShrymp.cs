using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ElectricShrymp : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        Imbued imbued = ModelDb.GetById<Imbued>(ModelDb.GetId<Imbued>());
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(
            Owner.RunState,
            Owner,
            Owner.Deck.Cards.Where(imbued.CanEnchant),
            minCount: 1,
            maxCount: 1,
            source: this)).SingleOrDefault();
        if (selected is not null)
        {
            await CardCmd.Enchant<Imbued>(selected, 1m);
        }
    }
}