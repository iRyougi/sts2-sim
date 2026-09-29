using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>SentryModePower 生成的 Token：0 费、虚无、消耗，由 Osty 随机攻击一名敌人 10（升级 15）；Osty 不在时无效果。</summary>
public sealed class SweepingGaze : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.RandomEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Exhaust];

    private decimal OstyDamage => IsUpgraded ? 15m : 10m;

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
            .TargetingRandomOpponents(CombatState!).Execute();
    }
}
