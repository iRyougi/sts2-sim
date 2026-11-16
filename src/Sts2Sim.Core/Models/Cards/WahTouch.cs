using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>单体 Wah 攻击并施加 Vulnerable（原版 <c>WahTouch</c>）。</summary>
public sealed class WahTouch : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Wah];
    private decimal Damage => IsUpgraded ? 8m : 6m;
    private decimal VulnerableAmount => IsUpgraded ? 2m : 1m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await PowerCmd.Apply<VulnerablePower>(CombatState!, cardPlay.Target, VulnerableAmount, Owner.Creature, this);
    }
}
