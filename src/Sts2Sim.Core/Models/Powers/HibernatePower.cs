using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Frost orbs check this power when granting block to players.</summary>
public sealed class HibernatePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (ReferenceEquals(player.Creature, Owner))
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
    }
}
