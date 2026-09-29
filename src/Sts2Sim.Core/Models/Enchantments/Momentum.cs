using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Momentum : EnchantmentModel
{
    private int _extraDamage;

    public override bool CanEnchant(CardModel card) => card.Type == CardType.Attack && base.CanEnchant(card);

    public override Task OnPlay(CardModel card)
    {
        AssertMutable();
        _extraDamage += (int)Magnitude;
        return Task.CompletedTask;
    }

    public override decimal EnchantDamageAdditive(decimal amount, ValueProp props) =>
        props.IsPoweredAttack() ? _extraDamage : 0m;

    internal override void AppendCombatStateDescription(ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) => builder.Append(_extraDamage);
}
