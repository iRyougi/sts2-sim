using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>保留的高伤射击（原版 <c>DivineShot</c>，ArchaicTooth 转化目标）。</summary>
public sealed class DivineShot : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shoot];
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    private decimal Damage => IsUpgraded ? 50m : 40m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        AttackCommand command = DamageCmd.Attack(Damage).FromArrow().FromCard(this, cardPlay).Targeting(cardPlay.Target);
        await ArrowCmd.ShootArrowAtTarget(cardPlay.Target, Owner, command);
    }
}
