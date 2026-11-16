using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>箭矢伤害加成（原版 <c>MegaCrit.Sts2.Core.Models.Powers.AimPower</c>）。</summary>
public sealed class AimPower : PowerModel
{
    private AttackCommand? _commandToModify;
    private int _amountWhenAttackStarted;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeAttack(AttackCommand command)
    {
        if (command.Attacker != Owner ||
            !command.DamageProps.IsPoweredAttack() ||
            !command.DamageProps.HasFlag(ValueProp.FromArrow) ||
            _commandToModify is not null ||
            (command.ModelSource is not null && command.ModelSource is not CardModel))
        {
            return Task.CompletedTask;
        }

        _commandToModify = command;
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
        if (Owner != dealer || !props.IsPoweredAttack() || !props.HasFlag(ValueProp.FromArrow))
            return 0m;
        if (_commandToModify is not null && cardSource is not null && !ReferenceEquals(cardSource, _commandToModify.ModelSource))
            return 0m;

        decimal value = Amount;
        if (cardSource is IAimAmountModifier modifier)
            value = modifier.ModifyAimAmount(value);
        return value;
    }

    public override async Task AfterAttack(AttackCommand command)
    {
        if (ReferenceEquals(command, _commandToModify))
        {
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -_amountWhenAttackStarted, null, null);
            _commandToModify = null;
            _amountWhenAttackStarted = 0;
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _commandToModify = null;
        _amountWhenAttackStarted = 0;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        context.AssertTransientEmpty(
            _commandToModify is null && _amountWhenAttackStarted == 0,
            $"{nameof(_commandToModify)}/{nameof(_amountWhenAttackStarted)}");
    }
}
