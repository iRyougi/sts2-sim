using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Unleash</c>：起始牌，1 费 Osty 攻击，伤害 6（升级 9）+ Osty 当前生命；Osty 不在时无效果。</summary>
public sealed class Unleash : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const decimal ExtraDamage = 1m;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Basic;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal CalculationBase => IsUpgraded ? 9m : 6m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = CalculatedDamage();
        return true;
    }

    // 原版 CalculatedVar.Calculate：战斗外乘数为 0；乘数是存活 Osty 的当前生命。
    private decimal CalculatedDamage()
    {
        int ostyHp = CombatState is not { } combatState || !combatState.IsLiveCombat()
            ? 0
            : Owner.Osty is { IsAlive: true } osty ? osty.CurrentHp : 0;
        return CalculationBase + ExtraDamage * ostyHp;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (Owner.IsOstyMissing)
            return;

        await DamageCmd.Attack(CalculatedDamage()).FromOsty(Owner.Osty!, this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }
}
