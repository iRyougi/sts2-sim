using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>
/// Deviation #157: extra-turn scheduling targets the simulator's single-player combat model;
/// multiplayer subset scheduling and the source game's hidden-power UI are omitted.
/// </summary>
public sealed class AmbergrisPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldTakeExtraTurn(Player player) =>
        Amount > 0 && ReferenceEquals(Owner.Player, player);

    public override async Task AfterTakingExtraTurn(Player player)
    {
        if (ReferenceEquals(Owner.Player, player))
        {
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        }
    }
}
