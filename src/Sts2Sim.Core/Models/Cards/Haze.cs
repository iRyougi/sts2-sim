using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Haze : CardModel
{
    private decimal Poison => IsUpgraded ? 6m : 4m;
    private decimal Weak => IsUpgraded ? 2m : 1m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 2;
    protected override async Task OnPlay(CardPlay play)
    {
        // 游戏是两次群体施加：先对全部可攻击敌人施加毒，再重新取一遍可攻击敌人施加虚弱（毒的触发可能已杀死某个敌人）。
        foreach (Creature enemy in CombatState!.HittableEnemies)
            await PowerCmd.Apply<PoisonPower>(CombatState, enemy, Poison, Owner.Creature, this);
        foreach (Creature enemy in CombatState!.HittableEnemies)
            await PowerCmd.Apply<WeakPower>(CombatState, enemy, Weak, Owner.Creature, this);
    }

}
