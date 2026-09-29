using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ViciousPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPowerAmountChanged(
        PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount > 0m && ReferenceEquals(applier, Owner) &&
            power is VulnerablePower && Owner.Player is { } player)
            await CardPileCmd.Draw(Owner.CombatState!, Amount, player, fromHandDraw: false);
    }
}
