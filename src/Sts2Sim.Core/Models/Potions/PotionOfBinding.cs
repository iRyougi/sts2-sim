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
        // 游戏取一次 HittableEnemies，对同一个列表先群体施加虚弱、再群体施加易伤（列表中中途死亡的敌人在第二轮收不到）。
        IReadOnlyList<Creature> targets = combatState.HittableEnemies;
        // 偏离 #138：真实源码交叉使用 WeakPower/VulnerablePower 的动态变量名；本项目不复刻命名混淆，仅保留两者均为 1 的数值结果。
        foreach (Creature enemy in targets)
            await PowerCmd.Apply<WeakPower>(combatState, enemy, WeakApplied, applier: Owner.Creature, cardSource: null);
        foreach (Creature enemy in targets)
            await PowerCmd.Apply<VulnerablePower>(combatState, enemy, VulnerableApplied, applier: Owner.Creature, cardSource: null);
    }
}
