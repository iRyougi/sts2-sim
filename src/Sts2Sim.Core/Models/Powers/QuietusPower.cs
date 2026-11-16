using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>下一张打出的箭矢牌乘以层数伤害并在穿刺后销毁箭矢（原版 <c>QuietusPower</c>）。</summary>
public sealed class QuietusPower : PowerModel
{
    private AttackCommand? _commandToModify;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterArrowShot(Player owner, ArrowSlot? arrow, CardModel? source)
    {
        if (arrow is not null && source is not null && ReferenceEquals(owner.Creature, Owner))
        {
            arrow.DestroyAfterImpaled = true;
        }
        return Task.CompletedTask;
    }

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
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!ReferenceEquals(dealer, Owner) || !props.IsPoweredAttack() || !props.HasFlag(ValueProp.FromArrow))
            return 1m;
        if (_commandToModify is not null && cardSource is not null && !ReferenceEquals(cardSource, _commandToModify.ModelSource))
            return 1m;
        return Amount;
    }

    public override async Task AfterAttack(AttackCommand command)
    {
        if (ReferenceEquals(command, _commandToModify))
        {
            _commandToModify = null;
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _commandToModify = null;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        context.AssertTransientEmpty(_commandToModify is null, nameof(_commandToModify));
    }
}
