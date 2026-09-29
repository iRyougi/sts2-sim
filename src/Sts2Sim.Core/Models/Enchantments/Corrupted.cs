using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Corrupted : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) => card.Type == CardType.Attack && base.CanEnchant(card);
    public override decimal EnchantDamageMultiplicative(decimal amount, ValueProp props) => props.IsPoweredAttack() ? 1.5m : 1m;

    public override Task OnPlay(CardModel card) => DamageOwner(card, null);
    public override Task OnPlay(CardModel card, CardPlay cardPlay) => DamageOwner(card, cardPlay);

    private static async Task DamageOwner(CardModel card, CardPlay? cardPlay)
    {
        const ValueProp props = ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move;
        if (card.Owner.Creature.CombatState is { } combat)
            await CreatureCmd.Damage(combat, [card.Owner.Creature], 2, props, null, card, cardPlay);
        else
            await CreatureCmd.Damage(card.Owner.RunState, card.Owner.Creature, 2, props);
    }
}
