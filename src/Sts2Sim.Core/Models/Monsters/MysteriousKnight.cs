using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Monsters;

public sealed class MysteriousKnight : FlailKnight
{
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 6m, Creature, null);
        await PowerCmd.Apply<PlatingPower>(Creature.CombatState!, Creature, 6m, Creature, null);
    }
}
