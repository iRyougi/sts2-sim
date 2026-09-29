using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Candelabra : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature) &&
            Owner.PlayerCombatState?.TurnNumber == 2)
        {
            Owner.PlayerCombatState.GainEnergy(2m);
        }

        return Task.CompletedTask;
    }
}
