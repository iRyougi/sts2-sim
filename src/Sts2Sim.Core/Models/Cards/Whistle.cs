using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Whistle : CardModel, ICombatStateDescriptionContributor, ICardDamageVariableProvider
{
    private decimal _damage = 33m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await CreatureCmd.Stun(cardPlay.Target);
    }

    protected override void OnUpgrade() => _damage += 11m;

    void ICombatStateDescriptionContributor.AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) => builder.Append(_damage);
}
