using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ToughBandages : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterCardDiscarded(CardModel card)
    {
        if (card.Owner != Owner || Owner.Creature.Side != Owner.Creature.CombatState!.CurrentSide)
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.GainBlock(Owner.Creature.CombatState, Owner.Creature, 3m,
            ValueProp.Unpowered, null, null);
    }
}
