using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PhilosophersStone : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player == Owner ? amount + 1m : amount;

    public override Task AfterCreatureAddedToCombat(Creature creature) =>
        creature.Side == Owner.Creature.Side ? Task.CompletedTask :
            PowerCmd.Apply<StrengthPower>(creature.CombatState!, creature, 1m, null, null);

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        var state = Owner.Creature.CombatState!;
        foreach (Creature creature in state.GetOpponentsOf(Owner.Creature).Where(creature => creature.IsAlive))
            await PowerCmd.Apply<StrengthPower>(state, creature, 1m, null, null);
    }
}
