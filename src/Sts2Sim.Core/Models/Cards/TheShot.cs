using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Florescence 衍生射击牌（原版 <c>MegaCrit.Sts2.Core.Models.Cards.TheShot</c>）。</summary>
public sealed class TheShot : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _extraDamage = 10m;
    private decimal _cardsExhaustedByFlorescence;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shoot];
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public decimal CalculatedDamage => _extraDamage * _cardsExhaustedByFlorescence;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = CalculatedDamage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        AttackCommand command = DamageCmd.Attack(CalculatedDamage).FromArrow().FromCard(this, cardPlay).Targeting(cardPlay.Target);
        await ArrowCmd.ShootArrowAtTarget(cardPlay.Target, Owner, command);
    }

    protected override void OnUpgrade() => _extraDamage += 3m;

    public void BuffFromFlorescence(decimal extraDamage)
    {
        AssertMutable();
        _cardsExhaustedByFlorescence += extraDamage;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_extraDamage);
        builder.Append(_cardsExhaustedByFlorescence);
    }
}
