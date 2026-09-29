using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>HighFive</c>：Osty 不在时不可打出；Osty 对所有敌人造成 11（升级 13）伤害，
/// 再给攻击后仍可命中的每个敌人施加 2（升级 3）易伤。</summary>
public sealed class HighFive : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AllEnemies;

    protected override int CanonicalEnergyCost => 2;

    protected override bool IsPlayable => !Owner.IsOstyMissing;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal OstyDamage => IsUpgraded ? 13m : 11m;

    private decimal Vulnerable => IsUpgraded ? 3m : 2m;

    // OstyDamage 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = OstyDamage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        if (Owner.IsOstyMissing)
            return;

        await DamageCmd.Attack(OstyDamage).FromOsty(Owner.Osty!, this, cardPlay)
            .TargetingAllOpponents(CombatState!).Execute();
        foreach (Creature enemy in CombatState!.HittableEnemies.ToList())
            await PowerCmd.Apply<VulnerablePower>(CombatState, enemy, Vulnerable, Owner.Creature, this);
    }
}
