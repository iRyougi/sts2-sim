using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Vambrace : RelicModel
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeCombatStart()
    {
        _triggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (_triggeredThisCombat ||
            amount <= 0m ||
            target != Owner.Creature ||
            cardSource?.Owner != Owner ||
            cardPlay?.Player != Owner)
        {
            return 1m;
        }

        // 偏离 #122：无 AfterModifyingBlockAmount；在乘算切片原子消费。后续 modifier 仍可能把最终格挡归零。
        _triggeredThisCombat = true;
        return 2m;
    }

    public override Task AfterCombatEnd()
    {
        _triggeredThisCombat = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_triggeredThisCombat);
    }
}
