using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Deathbringer</c>：2 费技能，给所有可命中的敌人施加 21（升级 26）灾厄，再施加 1 虚弱。
/// 两轮各自重新读取可命中敌人。</summary>
public sealed class Deathbringer : CardModel, ICardChoiceBaseValueProvider
{
    private const decimal Weak = 1m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AllEnemies;

    protected override int CanonicalEnergyCost => 2;

    private decimal Doom => IsUpgraded ? 26m : 21m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        foreach (var enemy in CombatState!.HittableEnemies)
            await PowerCmd.Apply<DoomPower>(CombatState, enemy, Doom, Owner.Creature, this);
        foreach (var enemy in CombatState!.HittableEnemies)
            await PowerCmd.Apply<WeakPower>(CombatState, enemy, Weak, Owner.Creature, this);
    }
}
