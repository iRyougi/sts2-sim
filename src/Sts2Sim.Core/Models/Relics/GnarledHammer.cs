using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Relics;

public sealed class GnarledHammer : RelicModel
{
    private const decimal SharpAmount = 3m;

    public override RelicRarity Rarity => RelicRarity.Shop;

    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        var sharp = (Sharp)ModelDb.Get(typeof(Sharp));
        var selected = await CardSelectCmd.SelectCardsAsync(
            Owner.RunState,
            Owner,
            Owner.Deck.Cards.Where(sharp.CanEnchant),
            minCount: 0,
            maxCount: 3,
            source: this,
            cancelable: false);
        foreach (var card in selected)
        {
            await CardCmd.Enchant<Sharp>(card, SharpAmount);
        }
    }
}
