using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class GigantificationPower : PowerModel
{
    private AttackCommand? _claimedCommand;
    private CardModel? _claimedCard;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeAttack(AttackCommand command)
    {
        if (_claimedCommand is null &&
            command.Attacker == Owner &&
            command.DamageProps.IsPoweredAttack() &&
            command.ModelSource is CardModel { Type: CardType.Attack } card)
        {
            _claimedCommand = command;
            _claimedCard = card;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        dealer == Owner &&
        props.IsPoweredAttack() &&
        ReferenceEquals(cardSource, _claimedCard)
            ? 3m
            : 1m;

    public override async Task AfterAttack(AttackCommand command)
    {
        if (!ReferenceEquals(command, _claimedCommand))
        {
            return;
        }

        await PowerCmd.TickDownDuration(Owner.CombatState!, this);
        _claimedCommand = null;
        _claimedCard = null;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        context.AssertTransientEmpty(
            _claimedCommand is null && _claimedCard is null,
            $"{nameof(_claimedCommand)}/{nameof(_claimedCard)}");
    }
}
