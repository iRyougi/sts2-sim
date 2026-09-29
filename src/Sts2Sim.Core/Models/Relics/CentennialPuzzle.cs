using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class CentennialPuzzle : RelicModel
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        // 对应上游 CentennialPuzzle.AfterDamageReceived 的 CombatManager.Instance.IsInProgress
        // 守卫，移植时漏了。事件里扣血（如 SunkenStatue.DiveIntoWaterAsync）同样会触发本 hook，
        // 此时 CombatState 为 null，后面 CardPileCmd.Draw 的 null! 会直接 NullReferenceException。
        // Plan 08b-6 Task 8 的 800 局 A10 里在种子 512 撞到一次。
        if (_triggeredThisCombat ||
            Owner.Creature.CombatState is null ||
            target != Owner.Creature ||
            result.UnblockedDamage <= 0)
        {
            return;
        }

        _triggeredThisCombat = true;
        for (int i = 0; i < 3; i++)
        {
            await CardPileCmd.Draw(
                Owner.Creature.CombatState!,
                1,
                Owner,
                fromHandDraw: false);
        }
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
