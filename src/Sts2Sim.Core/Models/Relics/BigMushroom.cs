using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Event relic: +20 Max HP, but two fewer cards on the first hand draw.</summary>
public sealed class BigMushroom : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool HasUponPickupEffect => true;

    public override Task AfterObtained() => CreatureCmd.GainMaxHp(Owner.Creature, 20m);

    /// <summary>偏离 #250：真实遗物在拾取和进房时把角色模型放大到 1.5 倍；
    /// 无头模拟器没有角色渲染节点，玩法侧的 Max HP 与首回合少摸两张完整保留。</summary>
    public override Task AfterRoomEntered(AbstractRoom room) => Task.CompletedTask;

    public override decimal ModifyHandDraw(Player player, decimal cardsToDraw) =>
        ReferenceEquals(player, Owner) && Owner.PlayerCombatState?.TurnNumber == 1
            ? cardsToDraw - 2m
            : cardsToDraw;
}
