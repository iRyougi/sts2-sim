using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Official source contains only existing damage, Weak, and static keyword metadata.</summary>
public sealed class Suppress : CardModel, ICardDamageVariableProvider
{
    private decimal Damage => IsUpgraded ? 17m : 11m;
    private decimal Weak => IsUpgraded ? 5m : 3m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];
    protected override async Task OnPlay(CardPlay play) { ArgumentNullException.ThrowIfNull(play.Target); await DamageCmd.Attack(Damage).FromCard(this, play).Targeting(play.Target).Execute(); await PowerCmd.Apply<WeakPower>(CombatState!, play.Target, Weak, Owner.Creature, this); }

}
