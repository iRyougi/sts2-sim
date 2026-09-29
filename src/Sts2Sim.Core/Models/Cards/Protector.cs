using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Protector</c>（先古）：1（升级 0）费，Osty 对目标造成 10（升级 15）+ Osty 最大生命的伤害；
/// Osty 不在时无效果。原版 CalculatedDamageVar 在每次命中时计算，单次命中且 Osty 最大生命在
/// BeforeAttack 前后不变，这里在攻击前算好。</summary>
public sealed class Protector : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Ancient;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal CalculationBase => IsUpgraded ? 15m : 10m;

    private const decimal ExtraDamage = 1m;

    // 原版 CalculatedVar.Calculate：战斗外倍率为 0；倍率 = 存活 Osty 的最大生命，否则 0。
    private decimal CalculatedDamage =>
        CalculationBase + ExtraDamage * (CombatState is not null && Owner.Osty is { IsAlive: true } osty
            ? osty.MaxHp
            : 0m);

    // CalculationBase/ExtraDamage/CalculatedDamage 都不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    // 原版 Thrash 读 CalculatedDamage.Calculate(null)。
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = CalculatedDamage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (Owner.IsOstyMissing)
            return;

        await DamageCmd.Attack(CalculatedDamage).FromOsty(Owner.Osty!, this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
