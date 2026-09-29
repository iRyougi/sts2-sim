using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SymbioticVirus : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature) && Owner.PlayerCombatState!.TurnNumber == 1)
            await OrbCmd.Channel<DarkOrb>(Owner.Creature.CombatState!, Owner);
    }
}
