using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>SleightOfFleshPower</c>：持有者给敌人施加（或改变）一次非临时性的减益层数后，
/// 对该敌人造成层数点 Unpowered 伤害。</summary>
public sealed class SleightOfFleshPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPowerAmountChanged(
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (amount != 0m &&
            power.GetTypeForAmount(amount) == PowerType.Debuff &&
            power.Owner.Side == CombatSide.Enemy &&
            applier == Owner &&
            power is not ITemporaryPower)
        {
            await CreatureCmd.Damage(Owner.CombatState!, [power.Owner], Amount, ValueProp.Unpowered, Owner, null, null);
        }
    }
}
