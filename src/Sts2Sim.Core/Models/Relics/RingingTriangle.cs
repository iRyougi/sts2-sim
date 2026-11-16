using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.113.1 原版 <c>RingingTriangle</c>：进入战斗房时施加 1 层保留手牌（可与其他保留叠加）。</summary>
public sealed class RingingTriangle : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom combatRoom)
        {
            await PowerCmd.Apply<RetainHandPower>(
                combatRoom.Engine.State,
                Owner.Creature,
                1m,
                Owner.Creature,
                null);
        }
    }
}
