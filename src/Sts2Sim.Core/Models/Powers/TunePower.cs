using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>若我方回合结束时本回合打出过 Ki，则获得 FreeCardPower（原版 <c>TunePower</c>）。</summary>
public sealed class TunePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy && Owner.Player is { } player && MelodyCmd.PlayedKiThisTurn(player))
        {
            await PowerCmd.Apply<FreeCardPower>(Owner.CombatState!, Owner, Amount, Owner, null);
        }
    }
}
