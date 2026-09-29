using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Relics;

public sealed class TriBoomerang : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public async override Task AfterObtained()
    {
        Instinct instinct = ModelDb.GetById<Instinct>(ModelDb.GetId<Instinct>());
        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            Owner.RunState, Owner, Owner.Deck.Cards.Where(instinct.CanEnchant), 3, 3, this);
        foreach (CardModel card in selected)
        {
            await CardCmd.Enchant<Instinct>(card, 1m);
        }
    }
}
