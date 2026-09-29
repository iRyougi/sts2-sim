using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RoyalStamp : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        var enchantment = (RoyallyApproved)ModelDb.Get(typeof(RoyallyApproved));
        List<CardModel> eligible = Owner.Deck.Cards.Where(enchantment.CanEnchant).ToList();
        Owner.RunState.Rng.Niche.Shuffle(eligible);
        CardModel? card = (await CardSelectCmd.SelectCardsAsync(
            Owner.RunState, Owner, eligible, 1, 1, this)).FirstOrDefault();
        if (card is not null)
            await CardCmd.Enchant<RoyallyApproved>(card, 1m);
    }
}