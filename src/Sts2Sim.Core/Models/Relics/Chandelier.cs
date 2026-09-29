using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Chandelier : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature) &&
            Owner.PlayerCombatState?.TurnNumber == 3)
        {
            Owner.PlayerCombatState.GainEnergy(3m);
        }

        return Task.CompletedTask;
    }
}
