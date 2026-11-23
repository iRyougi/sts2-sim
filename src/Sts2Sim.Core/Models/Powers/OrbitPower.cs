using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class OrbitPower : PowerModel
{
    private const int EnergyPerTrigger = 4;
    // sls2：照原版的内部数据记累计花掉的能量与已经触发的次数（原来记的是除以 4 的余数），规则不变；观测格式 20 的能力内部数据（8、9）读这两个数
    private int _energySpent;

    private int _triggerCount;

    public int EnergySpent => _energySpent;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override Task AfterEnergySpent(CardModel card, int amount)
    {
        if (card.Owner.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        _energySpent += amount;
        int triggers = _energySpent / EnergyPerTrigger - _triggerCount;
        for (int k = 0; k < triggers; k++)
        {
            Owner.Player!.PlayerCombatState!.GainEnergy(Amount);
        }
        if (triggers > 0) _triggerCount += triggers;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_energySpent);
        builder.Append(_triggerCount);
    }
}
