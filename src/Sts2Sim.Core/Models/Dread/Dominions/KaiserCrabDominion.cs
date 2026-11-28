using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Dread.Dominions;

public sealed class KaiserCrabDominion : DominionModel
{
    private const int PlatingAmount = 7;

    protected override async Task AfterEnemiesEnteredCombat(IEnumerable<Creature> creatures)
    {
        foreach (Creature creature in creatures)
        {
            await PowerCmd.Apply<PlatingPower>(
                creature.CombatState!, creature, PlatingAmount, applier: null, cardSource: null);
        }
    }
}
