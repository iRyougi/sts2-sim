using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Adroit : EnchantmentModel
{
    public override Task OnPlay(CardModel card, CardPlay cardPlay) => CreatureCmd.GainBlock(
        card.CombatState!, card.Owner.Creature, (int)Magnitude, ValueProp.Move, card, cardPlay);
}
