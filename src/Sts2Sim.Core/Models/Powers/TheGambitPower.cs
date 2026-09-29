using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>孤注一掷，若自身受到任何未被格挡的powered伤害则立即死亡（先移除本层再结算，避免自我重入）。
/// 对照真实源码，触发时直接调用 <see cref="CreatureCmd.Kill"/>，不会再制造一次普通伤害结果。</summary>
public sealed class TheGambitPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterDamageReceived(
        Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || !props.IsPoweredAttack() || result.UnblockedDamage <= 0)
        {
            return;
        }

        await PowerCmd.Remove(this);
        await CreatureCmd.Kill(Owner);
    }
}
