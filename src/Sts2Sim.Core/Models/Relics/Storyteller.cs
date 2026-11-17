using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Fool 初始遗物 SongBow 的 Orobas 升级版：入场获 12 层 AimPower，回合末获 3 层 AimPower（原版 <c>Storyteller</c>）。</summary>
public sealed class Storyteller : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom && Owner.Creature.CombatState is { } combatState)
        {
            Flash();
            await PowerCmd.Apply<AimPower>(
                combatState, Owner.Creature, 12m, Owner.Creature, cardSource: null);
        }
    }

    public override async Task BeforeSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner.Creature) && Owner.Creature.CombatState is { } combatState)
        {
            Flash();
            await PowerCmd.Apply<AimPower>(
                combatState, Owner.Creature, 3m, Owner.Creature, cardSource: null);
        }
    }
}
