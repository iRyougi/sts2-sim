using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Ricochet: Sly, repeated random-enemy hits.</summary>
public sealed class Ricochet : CardModel, ICardDamageVariableProvider
{
    private int _repeats = 4;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        // Native DamageVar is per hit; RepeatVar does not multiply it here.
        amount = 3m;
        return true;
    }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.RandomEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Sly];
    protected override Task OnPlay(CardPlay cardPlay) => DamageCmd.Attack(3m).WithHitCount(_repeats)
        .FromCard(this, cardPlay).TargetingRandomOpponents(CombatState!).Execute();
    protected override void OnUpgrade() => _repeats++;
}
