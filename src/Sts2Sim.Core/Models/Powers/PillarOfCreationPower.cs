using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class PillarOfCreationPower : PowerModel
{
    private bool _triggeredThisTurn;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner))
        {
            _triggeredThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (_triggeredThisTurn || creator?.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        _triggeredThisTurn = true;
        return CreatureCmd.GainBlock(
            Owner.CombatState!, Owner, Amount, ValueProp.Unpowered, null, null);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_triggeredThisTurn);
    }
}
