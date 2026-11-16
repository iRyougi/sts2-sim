using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RoyalStamp : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        using IDisposable rngScope = Owner.PlayerRng.BeginSemanticScope(
            $"{Owner.CurrentSemanticLocationKey}/noncombat/RoyalStamp");
        var enchantment = (RoyallyApproved)ModelDb.Get(typeof(RoyallyApproved));
        List<CardModel> eligible = Owner.Deck.Cards.Where(enchantment.CanEnchant).ToList();
        eligible.UnstableShuffle(Owner.PlayerRng.NonCombatGeneration);
        CardModel? card = (await CardSelectCmd.SelectCardsAsync(
            Owner.RunState, Owner, eligible, 1, 1, this)).FirstOrDefault();
        if (card is not null)
            await CardCmd.Enchant<RoyallyApproved>(card, 1m);
    }
}