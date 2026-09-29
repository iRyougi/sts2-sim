namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

public sealed class SkittishPower : PowerModel
{
    private bool _hasGainedBlockThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public bool HasGainedBlockThisTurn
    {
        get => _hasGainedBlockThisTurn;
        private set
        {
            AssertMutable();
            _hasGainedBlockThisTurn = value;
        }
    }

    public override async Task AfterAttack(AttackCommand command)
    {
        if (_hasGainedBlockThisTurn || !command.DamageProps.HasFlag(ValueProp.Move) || command.ModelSource is not CardModel)
        {
            return;
        }

        DamageResult? result = command.Results
            .SelectMany(results => results)
            .FirstOrDefault(result => ReferenceEquals(result.Receiver, Owner));
        if (result is null || result.UnblockedDamage == 0)
        {
            return;
        }

        HasGainedBlockThisTurn = true;
        await CreatureCmd.GainBlock(Owner.CombatState!, Owner, Amount, ValueProp.Unpowered, null, null);
    }

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side)
        {
            HasGainedBlockThisTurn = false;
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_hasGainedBlockThisTurn);
}
