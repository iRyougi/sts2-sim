using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Claws : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public async override Task AfterObtained()
    {
        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            Owner.RunState, Owner,
            Owner.Deck.Cards.Where(card => card.Type != CardType.Quest && card.IsTransformable),
            0, 6, this);
        var transformations = new List<CardTransformation>();
        foreach (CardModel card in selected)
        {
            var maul = (Maul)ModelDb.Card<Maul>().MutableClone();
            maul.AssignOwner(Owner);
            if (card.IsUpgraded)
            {
                maul.Upgrade();
            }

            foreach (EnchantmentModel enchantment in card.Enchantments.Where(enchantment => enchantment.CanEnchant(maul)).ToArray())
            {
                var clone = (EnchantmentModel)enchantment.MutableClone();
                await CardCmd.Enchant(clone, maul, enchantment.Magnitude);
            }

            transformations.Add(new CardTransformation(card, maul));
        }

        await CardCmd.Transform(transformations, Owner.PlayerRng.Transformations);
    }
}
