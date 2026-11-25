using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Florescence 衍生射击牌：0 费、消耗，射出箭矢造成 20（升级 25）点伤害（v0.114.0 原版 <c>TheShot</c>）。</summary>
public sealed class TheShot : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shoot];
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private decimal Damage => IsUpgraded ? 25m : 20m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override bool ShouldGlowRedInternal => !ArrowCmd.PlayerHasAnyArrows(Owner);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        AttackCommand command = DamageCmd.Attack(Damage).FromArrow().FromCard(this, cardPlay).Targeting(cardPlay.Target);
        await ArrowCmd.ShootArrowAtTarget(cardPlay.Target, Owner, command);
    }
}
