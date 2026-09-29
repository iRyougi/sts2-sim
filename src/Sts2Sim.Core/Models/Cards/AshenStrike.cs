using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class AshenStrike : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = 6m + _extraDamage * Owner.PlayerCombatState!.ExhaustPile.Cards.Count;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    private decimal _extraDamage = 3m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Strike];
    protected override int CanonicalEnergyCost => 1;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal damage = 6m + _extraDamage * Owner.PlayerCombatState!.ExhaustPile.Cards.Count;
        return DamageCmd.Attack(damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => _extraDamage++;
}
