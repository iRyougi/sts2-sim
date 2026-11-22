using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Glam : EnchantmentModel
{
    // game Glam.EnchantPlayCount is a pure query (observation encoders and previews ask for it too);
    // game Glam.AfterCardPlayed disables the enchantment once its own card has been played.
    public override int EnchantPlayCount(int originalPlayCount) =>
        Status == EnchantmentStatus.Normal ? originalPlayCount + 1 : originalPlayCount;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, Owner))
        {
            Status = EnchantmentStatus.Disabled;
        }

        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        Status = EnchantmentStatus.Normal;
    }
}
