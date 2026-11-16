using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Fool 初始射击（原版 <c>MegaCrit.Sts2.Core.Models.Cards.FoolishShot</c>）。</summary>
public sealed class FoolishShot : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _damage = 15m;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Basic;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shoot];
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        AttackCommand command = DamageCmd.Attack(_damage).FromArrow().FromCard(this, cardPlay).Targeting(cardPlay.Target);
        await ArrowCmd.ShootArrowAtTarget(cardPlay.Target, Owner, command);
    }

    protected override void OnUpgrade() => _damage += 5m;
}
