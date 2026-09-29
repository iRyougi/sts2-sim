using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Akabeko : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature) ||
            Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        await PowerCmd.Apply<VigorPower>(
            Owner.Creature.CombatState!,
            Owner.Creature,
            8m,
            Owner.Creature,
            null);
    }
}
