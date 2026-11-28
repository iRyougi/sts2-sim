using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Dread.Dominions;

public sealed class TestSubjectDominion : DominionModel
{
    protected override async Task AfterEnemiesEnteredCombat(IEnumerable<Creature> creatures)
    {
        foreach (Creature creature in creatures)
        {
            await PowerCmd.Apply<StrengthPower>(
                creature.CombatState!, creature, 3m, applier: null, cardSource: null);
        }
    }
}
