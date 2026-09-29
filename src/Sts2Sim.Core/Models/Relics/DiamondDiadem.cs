using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DiamondDiadem : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature) || Owner.PlayerCombatState?.TurnNumber > 1) return;
        await CreatureCmd.GainBlock(Owner.Creature.CombatState!, Owner.Creature, 20m, ValueProp.Unpowered, null, null);
        await PowerCmd.Apply<BlurPower>(Owner.Creature.CombatState!, Owner.Creature, 1m, Owner.Creature, null);
    }
}
