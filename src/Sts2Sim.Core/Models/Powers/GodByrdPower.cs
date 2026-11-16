using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>v0.113.1 <c>GodByrdPower</c>：持有者下个玩家回合开始（<see cref="PowerModel.AmountOnTurnStart"/> 非 0）时
/// 对全部可命中的敌人造成 Amount 点 Unpowered 伤害，然后移除自身。偏离 #160：Byrdine 的攻击动画与等待省略。</summary>
public sealed class GodByrdPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner.Player || AmountOnTurnStart == 0) return;
        await CreatureCmd.Damage(Owner.CombatState!, Owner.CombatState!.HittableEnemies,
            Amount, ValueProp.Unpowered, Owner, null, null);
        await PowerCmd.Remove(this);
    }
}
