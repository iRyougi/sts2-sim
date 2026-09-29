using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Swift : EnchantmentModel
{
    public override async Task OnPlay(CardModel card)
    {
        if (Status != EnchantmentStatus.Normal || card.Owner.Creature.CombatState is null) return;
        Status = EnchantmentStatus.Disabled;
        await CardPileCmd.Draw(card.Owner.Creature.CombatState, (int)Magnitude, card.Owner, fromHandDraw: false);
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        Status = EnchantmentStatus.Normal;
    }
}
