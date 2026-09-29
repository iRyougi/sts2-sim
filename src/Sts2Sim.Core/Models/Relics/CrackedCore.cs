using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class CrackedCore : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override async Task BeforeSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants)
    {
        // This hook precedes SetupPlayerTurnAsync's increment: native turn 1 is simulator turn 0.
        if (participants.Contains(Owner.Creature) && Owner.PlayerCombatState!.TurnNumber == 0)
            await OrbCmd.Channel<LightningOrb>(Owner.Creature.CombatState!, Owner);
    }
}
