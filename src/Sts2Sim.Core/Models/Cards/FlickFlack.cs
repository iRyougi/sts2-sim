using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Flick Flack: Sly all-enemy attack.</summary>
public sealed class FlickFlack : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 7m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Sly];
    protected override Task OnPlay(CardPlay cardPlay) => DamageCmd.Attack(_damage).FromCard(this, cardPlay)
        .TargetingAllOpponents(CombatState!).Execute();
    protected override void OnUpgrade() => _damage += 2m;
}
