using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

public sealed class PotionOfBinding : PotionModel
{
    private const decimal WeakApplied = 1m;
    private const decimal VulnerableApplied = 1m;

    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AllEnemies;

    protected override async Task OnUse(Creature? target)
    {
        _ = target;
        ICombatState combatState = Owner.Creature.CombatState!;
        foreach (Creature enemy in combatState.HittableEnemies.ToArray())
        {
            // 偏离 #138：真实源码交叉使用 WeakPower/VulnerablePower 的动态变量名；本项目不复刻命名混淆，仅保留两者均为 1 的数值结果。
            await PowerCmd.Apply<WeakPower>(
                combatState,
                enemy,
                WeakApplied,
                applier: null,
                cardSource: null);
            await PowerCmd.Apply<VulnerablePower>(
                combatState,
                enemy,
                VulnerableApplied,
                applier: null,
                cardSource: null);
        }
    }
}
