using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class VeryHotCocoa : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature) && Owner.PlayerCombatState?.TurnNumber == 1)
            Owner.PlayerCombatState.GainEnergy(4m);
        return Task.CompletedTask;
    }
}
