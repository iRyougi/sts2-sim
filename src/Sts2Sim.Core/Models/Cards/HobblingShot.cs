using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>射击成功后对目标施加 Weak 与 Vulnerable（原版 <c>HobblingShot</c>）。</summary>
public sealed class HobblingShot : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shoot];
    private decimal Damage => IsUpgraded ? 14m : 13m;
    private decimal PowerAmount => IsUpgraded ? 2m : 1m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override bool ShouldGlowRedInternal => !ArrowCmd.PlayerHasAnyArrows(Owner);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        AttackCommand command = DamageCmd.Attack(Damage).FromArrow().FromCard(this, cardPlay).Targeting(cardPlay.Target);
        if (await ArrowCmd.ShootArrowAtTarget(cardPlay.Target, Owner, command))
        {
            await PowerCmd.Apply<WeakPower>(CombatState!, cardPlay.Target, PowerAmount, Owner.Creature, this);
            await PowerCmd.Apply<VulnerablePower>(CombatState!, cardPlay.Target, PowerAmount, Owner.Creature, this);
        }
    }
}
