using Sts2Sim.Core.Commands;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Sown : EnchantmentModel
{
    public override async Task OnPlay(CardModel card)
    {
        if (Status != EnchantmentStatus.Normal)
        {
            return;
        }

        Status = EnchantmentStatus.Disabled;
        await PlayerCmd.GainEnergy(Magnitude, card.Owner);
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        Status = EnchantmentStatus.Normal;
    }
}
