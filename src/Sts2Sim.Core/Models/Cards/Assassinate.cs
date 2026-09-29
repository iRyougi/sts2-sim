using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Assassinate : CardModel, ICardDamageVariableProvider
{
    private decimal Damage => IsUpgraded ? 13m : 10m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    private decimal Vulnerable => IsUpgraded ? 2m : 1m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Innate, CardKeyword.Exhaust];
    protected override async Task OnPlay(CardPlay play) { ArgumentNullException.ThrowIfNull(play.Target); await DamageCmd.Attack(Damage).FromCard(this, play).Targeting(play.Target).Execute(); await PowerCmd.Apply<VulnerablePower>(CombatState!, play.Target, Vulnerable, Owner.Creature, this); }

}
