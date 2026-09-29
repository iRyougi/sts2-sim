using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>SicEm</c>：1 费 Osty 攻击 5（升级 6）；无论 Osty 是否在场，随后都给目标
/// 3（升级 4）层 <see cref="SicEmPower"/>。</summary>
public sealed class SicEm : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal OstyDamage => IsUpgraded ? 6m : 5m;

    private decimal SicEmAmount => IsUpgraded ? 4m : 3m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = OstyDamage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (!Owner.IsOstyMissing)
        {
            await DamageCmd.Attack(OstyDamage).FromOsty(Owner.Osty!, this, cardPlay)
                .Targeting(cardPlay.Target).Execute();
        }

        await PowerCmd.Apply<SicEmPower>(CombatState!, cardPlay.Target, SicEmAmount, Owner.Creature, this);
    }
}
