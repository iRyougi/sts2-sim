using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RedMask : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature) ||
            Owner.PlayerCombatState?.TurnNumber != 0)
        {
            return;
        }

        foreach (Creature enemy in Owner.Creature.CombatState!.HittableEnemies.ToArray())
        {
            await PowerCmd.Apply<WeakPower>(
                Owner.Creature.CombatState,
                enemy,
                1m,
                Owner.Creature,
                null);
        }
    }
}
