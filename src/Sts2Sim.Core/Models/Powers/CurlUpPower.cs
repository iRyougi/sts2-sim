namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

public sealed class CurlUpPower : PowerModel
{
    private CardModel? _triggeringCard;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Task AfterDamageReceived(Creature target, DamageResult result, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && cardSource is not null && props.IsPoweredAttack() &&
            (_triggeringCard is null || ReferenceEquals(_triggeringCard, cardSource)))
        {
            _triggeringCard = cardSource;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card != _triggeringCard || !Owner.Powers.Contains(this)) return;
        await CreatureCmd.GainBlock(Owner.CombatState!, Owner, Amount, ValueProp.Unpowered, null, null);
        if (Owner.Monster is LouseProgenitor louse) louse.Curled = true;
        await PowerCmd.Remove(this);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        context.AppendCardReferences(ref builder, [_triggeringCard]);
}
