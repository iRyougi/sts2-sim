using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>
/// 下一次有威力的攻击额外造成等于层数的伤害，并在攻击后消耗开始攻击时的层数。
/// 偏离 #90：用私有字段记录攻击命令和层数快照，不复刻 InitInternalData/GetInternalData 间接寻址。
/// </summary>
public sealed class VigorPower : PowerModel, ICombatStateDescriptionContributor
{
    private AttackCommand? _commandBeingModified;
    private int _amountWhenAttackStarted;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeAttack(AttackCommand command)
    {
        if (command.Attacker != Owner ||
            !command.DamageProps.IsPoweredAttack() ||
            _commandBeingModified is not null)
        {
            return Task.CompletedTask;
        }

        _commandBeingModified = command;
        _amountWhenAttackStarted = Amount;
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return Owner == dealer && props.IsPoweredAttack() ? Amount : 0m;
    }

    public override async Task AfterAttack(AttackCommand command)
    {
        if (!ReferenceEquals(command, _commandBeingModified))
        {
            return;
        }

        await PowerCmd.ModifyAmount(Owner.CombatState!, this, -_amountWhenAttackStarted, null, null);
        _commandBeingModified = null;
        _amountWhenAttackStarted = 0;
    }

    void ICombatStateDescriptionContributor.AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        context.AssertTransientEmpty(
            _commandBeingModified is null && _amountWhenAttackStarted == 0,
            $"{nameof(_commandBeingModified)}/{nameof(_amountWhenAttackStarted)}");
    }
}
