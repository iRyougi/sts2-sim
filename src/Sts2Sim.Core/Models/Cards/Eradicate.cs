using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Eradicate</c>：X 费、保留，对目标造成 11（升级 14）伤害 X 次。</summary>
public sealed class Eradicate : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override bool IsXEnergyCost => true;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    private decimal Damage => IsUpgraded ? 14m : 11m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hitCount = Hook.ModifyXValue(CombatState!, this, cardPlay.Resources.EnergyXValue);
        await DamageCmd.Attack(Damage).WithHitCount(hitCount).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }
}
