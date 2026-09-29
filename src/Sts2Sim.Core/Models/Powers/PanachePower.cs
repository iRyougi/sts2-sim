using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class PanachePower : PowerModel
{
    private const int CardsPerTrigger = 5;
    private bool _alreadyApplied;
    private int _cardsLeft = CardsPerTrigger;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player.Creature != Owner)
        {
            return;
        }

        if (!_alreadyApplied)
        {
            _alreadyApplied = true;
            return;
        }

        _cardsLeft--;
        if (_cardsLeft > 0)
        {
            return;
        }

        await CreatureCmd.Damage(
            Owner.CombatState!,
            Owner.CombatState!.GetOpponentsOf(Owner),
            Amount,
            ValueProp.Unpowered,
            Owner,
            null,
            cardPlay);
        _cardsLeft = CardsPerTrigger;
    }

    public override Task BeforeSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            _cardsLeft = CardsPerTrigger;
        }
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_alreadyApplied);
        builder.Append(_cardsLeft);
    }
}
