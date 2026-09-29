using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class PerfectedStrike : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        int strikes = Owner.PlayerCombatState!.AllPiles
            .SelectMany(pile => pile.Cards)
            .Count(card => card.Tags.Contains(CardTag.Strike));
        amount = 6m + _extraDamagePerStrike * strikes;
        return true;
    }

    private decimal _extraDamagePerStrike = 2m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Strike];

    protected override Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int strikes = Owner.PlayerCombatState!.AllPiles
            .SelectMany(pile => pile.Cards)
            .Count(card => card.Tags.Contains(CardTag.Strike));
        return DamageCmd.Attack(6m + _extraDamagePerStrike * strikes)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => _extraDamagePerStrike++;
}
