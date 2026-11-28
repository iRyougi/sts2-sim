using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class LagavulinMatriarchDominionPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner))
        {
            await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, -Amount, applier: null, cardSource: null);
            await PowerCmd.Apply<DexterityPower>(Owner.CombatState!, Owner, -Amount, applier: null, cardSource: null);
            await PowerCmd.Remove(this);
        }
    }
}
