using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Skewer: X-energy hit count uses the play's resolved energy value.</summary>
public sealed class Skewer : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 8m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        // Native DamageVar is per hit and independent of resolved X energy.
        amount = _damage;
        return true;
    }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override bool IsXEnergyCost => true;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hitCount = Hook.ModifyXValue(CombatState!, this, cardPlay.Resources.EnergyXValue);
        await DamageCmd.Attack(_damage).WithHitCount(hitCount)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
    protected override void OnUpgrade() => _damage += 3m;
}
