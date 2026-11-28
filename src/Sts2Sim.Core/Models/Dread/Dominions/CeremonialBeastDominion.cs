using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Dread.Dominions;

public sealed class CeremonialBeastDominion : DominionModel
{
    private const int RitualAmount = 1;

    protected override async Task AfterEnemiesEnteredCombat(IEnumerable<Creature> creatures)
    {
        foreach (Creature creature in creatures)
        {
            await PowerCmd.Apply<RitualPower>(
                creature.CombatState!, creature, RitualAmount, applier: null, cardSource: null);
        }
    }
}
