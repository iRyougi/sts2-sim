using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Dagger Spray: two hits against every opponent.</summary>
public sealed class DaggerSpray : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 4m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay) =>
        await DamageCmd.Attack(_damage).WithHitCount(2).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!).Execute();
    protected override void OnUpgrade() => _damage += 2m;
}
