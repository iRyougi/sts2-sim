using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class DemisePower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        _ = side;
        if (!participants.Contains(Owner))
        {
            return;
        }

        await CreatureCmd.Damage(
            Owner.CombatState!,
            new[] { Owner },
            Amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: null,
            cardSource: null,
            cardPlay: null);
    }
}
