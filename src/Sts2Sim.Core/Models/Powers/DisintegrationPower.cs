namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

public sealed class DisintegrationPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    // 原版挂在 AfterSideTurnEndLate（同一时机的 AfterSideTurnEnd 全部结算完之后，如格挡盾先按扣血前的格挡判定），
    // 伤害来源是自己
    public override Task AfterSideTurnEndLate(CombatSide side, IEnumerable<Creature> participants) =>
        participants.Contains(Owner)
            ? CreatureCmd.Damage(Owner.CombatState!, [Owner], Amount,
                ValueProp.Unpowered, Owner, null, null)
            : Task.CompletedTask;
}
