using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;
using Sts2Sim.Core.Commands;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Prevents HP loss after ordinary reducers; consumption follows the committed modifier pass.</summary>
public sealed class BufferPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return target == Owner ? 0m : amount;
    }

    public override Task AfterModifyingHpLostAfterOsty() =>
        PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
}
