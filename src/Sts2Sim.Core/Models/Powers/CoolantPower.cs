using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class CoolantPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        int distinctOrbs = Owner.Player!.PlayerCombatState!.OrbQueue.Orbs.Select(orb => orb.Id).Distinct().Count();
        await CreatureCmd.GainBlock(Owner.CombatState!, Owner, distinctOrbs * Amount,
            ValueProp.Unpowered, null, null);
    }
}
