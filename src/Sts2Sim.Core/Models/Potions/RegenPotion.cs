using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

public sealed class RegenPotion : PotionModel
{
    private const decimal RegenApplied = 5m;

    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    /// <summary>逐字移植真实源码中的战斗内生成限制。合并前审查发现计划文档的 Uncommon 参考表漏标了
    /// 这一条元数据（只标注了 FairyInABottle/FruitJuice 两个），导致这张药水本会被 Alchemize 之类的
    /// 战斗内生成效果错误地摇到，真实游戏明确排除。</summary>
    public override bool CanBeGeneratedInCombat => false;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await PowerCmd.Apply<RegenPower>(
            target.CombatState!,
            target,
            RegenApplied,
            applier: Owner.Creature,
            cardSource: null);
    }
}
