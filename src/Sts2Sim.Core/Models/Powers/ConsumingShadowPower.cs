using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ConsumingShadowPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.Player?.PlayerCombatState?.OrbQueue.Orbs.Count == 0)
            return;
        for (int i = 0; i < Amount; i++)
            await OrbCmd.EvokeLast(Owner.CombatState!, Owner.Player!);
    }
}
