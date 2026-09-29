using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Slither : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) =>
        base.CanEnchant(card) &&
        !card.HasKeyword(CardKeyword.Unplayable) &&
        !card.CostsXEnergy;

    public override Task OnDrawn(CardModel card)
    {
        if (card.Pile?.Type == PileType.Hand)
        {
            int cost = card.CombatState!.RunState.Rng.CombatEnergyCosts.NextInt(4);
            card.SetTemporaryCostOverrideThisCombat(cost);
        }

        return Task.CompletedTask;
    }
}
