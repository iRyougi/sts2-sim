using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>禁止通过卡牌获得格挡（Unpowered 格挡不受影响）。逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.NoBlockPower</c>）：
/// 在敌方回合结束时递减，配合"施加时长为2"实现"本回合+下回合都无法获得格挡"。</summary>
public sealed class NoBlockPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != Owner || props.HasFlag(ValueProp.Unpowered) || cardSource is null)
        {
            return 1m;
        }

        return 0m;
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.TickDownDuration(Owner.CombatState!, this);
        }
    }
}
