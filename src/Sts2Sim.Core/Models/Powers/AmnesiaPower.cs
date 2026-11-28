using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class AmnesiaPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (Owner.Player == player)
        {
            await PlayerCmd.LoseEnergy(1m, player);
        }
    }

    public override decimal ModifyHandDraw(Player player, decimal originalCardCount) =>
        player != Owner.Player
            ? originalCardCount
            : Math.Max(0m, originalCardCount - 2m);

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy)
        {
            await PowerCmd.Remove(this);
        }
    }
}
