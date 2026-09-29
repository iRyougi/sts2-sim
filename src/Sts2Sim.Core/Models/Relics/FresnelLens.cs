using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FresnelLens : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override CardModel? TryModifyCardRewardOptionLate(IRunState runState, Player player, CardModel option) =>
        player == Owner && CanEnchant(option) ? EnchantCard(option) : null;

    public override void ModifyMerchantCardCreationResults(Player player, List<CardModel> cards)
    {
        if (player != Owner) return;
        for (int i = 0; i < cards.Count; i++)
            if (CanEnchant(cards[i])) cards[i] = EnchantCard(cards[i]);
    }

    public override bool TryModifyCardBeingAddedToDeck(CardModel card, out CardModel? newCard)
    {
        newCard = null;
        if (card.Owner != Owner || !CanEnchant(card)) return false;
        newCard = EnchantCard(card);
        return true;
    }

    private static bool CanEnchant(CardModel card) =>
        ((Nimble)ModelDb.Get(typeof(Nimble))).CanEnchant(card);

    private static CardModel EnchantCard(CardModel card)
    {
        var clone = (CardModel)card.MutableClone();
        CardCmd.Enchant<Nimble>(clone, 2m).GetAwaiter().GetResult();
        return clone;
    }
}
