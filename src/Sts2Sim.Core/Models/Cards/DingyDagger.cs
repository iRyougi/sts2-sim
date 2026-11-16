using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>v0.113.1 Waterfall event card (#927): unlimited upgrades, each adding 3 + new upgrade level damage.</summary>
public sealed class DingyDagger : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public const int FineUpgradeLevel = 3;
    public const int PristineUpgradeLevel = 6;

    private decimal _damage = 7m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.AnyEnemy;
    public override int MaxUpgradeLevel => 999999999;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    // Native UpgradeInternal increments CurrentUpgradeLevel before OnUpgrade.
    protected override void OnUpgrade() => _damage += 3 + CurrentUpgradeLevel;
}
