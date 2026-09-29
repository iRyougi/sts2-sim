using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Vigorous : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) => card.Type == CardType.Attack && base.CanEnchant(card);
    public override decimal EnchantDamageAdditive(decimal amount, ValueProp props) =>
        Status == EnchantmentStatus.Normal && props.IsPoweredAttack() ? Magnitude : 0m;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, Owner)) Status = EnchantmentStatus.Disabled;
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        Status = EnchantmentStatus.Normal;
    }
}
