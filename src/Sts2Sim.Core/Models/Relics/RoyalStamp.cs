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
        // #1044：原版 CardSelectCmd.FromDeckForEnchantment(IReadOnlyList<CardModel>, ...) 展示前按牌组下标重排
        // （OrderBy(indexMap)），所以玩家看到的是牌组顺序；上面的洗牌只消耗 NonCombatGeneration。
        Dictionary<CardModel, int> deckIndex = Owner.Deck.Cards
            .Select((deckCard, index) => (deckCard, index))
            .ToDictionary(pair => pair.deckCard, pair => pair.index);
        List<CardModel> shown = eligible.OrderBy(deckCard => deckIndex[deckCard]).ToList();
        CardModel? card = (await CardSelectCmd.SelectCardsAsync(
            Owner.RunState, Owner, shown, 1, 1, this)).FirstOrDefault();
        if (card is not null)
            await CardCmd.Enchant<RoyallyApproved>(card, 1m);
    }
}