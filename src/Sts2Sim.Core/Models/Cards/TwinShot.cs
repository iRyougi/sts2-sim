using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>射出箭矢造成 7（升级 10）点伤害 2 次（v0.114.0 新增，原版 <c>TwinShot</c>；取代 RapidShot）。</summary>
public sealed class TwinShot : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const int Hits = 2;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shoot];
    private decimal Damage => IsUpgraded ? 10m : 7m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override bool ShouldGlowRedInternal => !ArrowCmd.PlayerHasAnyArrows(Owner);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        AttackCommand command = DamageCmd.Attack(Damage)
            .WithHitCount(Hits)
            .FromArrow()
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target);
        await ArrowCmd.ShootArrowAtTarget(cardPlay.Target, Owner, command);
    }
}
