using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>本回合结束时保留整手牌不弃置。逐字移植核心行为（<c>MegaCrit.Sts2.Core.Models.Powers.RetainHandPower</c>），
/// #102 已销案：通过通用 ShouldFlush veto hook 生效。</summary>
public sealed class RetainHandPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldFlush(Entities.Players.Player player) => player != Owner.Player;

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner))
        {
            await PowerCmd.TickDownDuration(Owner.CombatState!, this);
        }
    }
}
