using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class RollingBoulderPower : PowerModel
{
    private const int GrowthPerTrigger = 5;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        await CreatureCmd.Damage(
            Owner.CombatState!,
            Owner.CombatState!.GetOpponentsOf(Owner),
            Amount,
            ValueProp.Unpowered,
            Owner,
            null,
            null);
        SetAmount(Amount + GrowthPerTrigger);
    }
}
