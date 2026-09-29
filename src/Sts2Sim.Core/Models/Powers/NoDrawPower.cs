using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class NoDrawPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override bool ShouldDraw(Player player, bool fromHandDraw) =>
        fromHandDraw || player != Owner.Player;
    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) =>
        participants.Contains(Owner) ? PowerCmd.Remove(this) : Task.CompletedTask;
}
