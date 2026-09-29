using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Powers;

public sealed class InfiniteBladesPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override async Task BeforeHandDraw(Player player)
    {
        if (player == Owner.Player) await Shiv.CreateInHand(player, Amount, Owner.CombatState!);
    }
}
