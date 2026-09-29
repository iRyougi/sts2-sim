using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class BeaconOfHopePower : PowerModel
{
    private bool _isSharingBlock;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        if (_isSharingBlock ||
            creature != Owner ||
            Owner.CombatState!.CurrentSide != Owner.Side ||
            amount < 1m)
        {
            return;
        }

        decimal amountToShare = amount * 0.5m;
        if (amountToShare < 1m)
        {
            return;
        }

        _isSharingBlock = true;
        try
        {
            foreach (Creature teammate in Owner.CombatState.Players
                         .Select(player => player.Creature)
                         .Where(candidate => candidate.IsAlive && candidate != Owner))
            {
                await CreatureCmd.GainBlock(
                    Owner.CombatState,
                    teammate,
                    amountToShare,
                    ValueProp.Unpowered,
                    cardSource: null,
                    cardPlay: null);
            }
        }
        finally
        {
            _isSharingBlock = false;
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        context.AssertTransientEmpty(!_isSharingBlock, nameof(_isSharingBlock));
    }
}
