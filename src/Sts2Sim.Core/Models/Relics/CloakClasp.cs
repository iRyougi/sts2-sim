using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class CloakClasp : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task BeforeSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.GainBlock(
            Owner.Creature.CombatState!,
            Owner.Creature,
            Owner.PlayerCombatState!.Hand.Cards.Count,
            ValueProp.Unpowered,
            null,
            null);
    }
}
