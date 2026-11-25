using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.114.0 (#1110): the Clone rest-site option now comes from the <see cref="Clone"/> enchantment.</summary>
public sealed class PaelsGrowth : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        Clone clone = ModelDb.GetById<Clone>(ModelDb.GetId<Clone>());
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(Owner.RunState, Owner,
            Owner.Deck.Cards.Where(clone.CanEnchant).ToList(), 1, 1, this)).FirstOrDefault();
        if (selected is not null)
        {
            await CardCmd.Enchant<Clone>(selected, 4m);
        }
    }
}
