namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Glam : EnchantmentModel
{
    public override int EnchantPlayCount(int originalPlayCount)
    {
        if (Status != EnchantmentStatus.Normal)
        {
            return originalPlayCount;
        }

        Status = EnchantmentStatus.Disabled;
        return originalPlayCount + 1;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        Status = EnchantmentStatus.Normal;
    }
}
