using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Poke</c>：Osty 对目标造成 6（升级 9）伤害；Osty 不在时无效果。</summary>
public sealed class Poke : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal OstyDamage => IsUpgraded ? 9m : 6m;

    // OstyDamage 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = OstyDamage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (Owner.IsOstyMissing)
            return;

        await DamageCmd.Attack(OstyDamage).FromOsty(Owner.Osty!, this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }
}
