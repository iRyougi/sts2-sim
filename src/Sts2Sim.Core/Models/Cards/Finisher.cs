using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative history query narrowed to the cloned/fingerprinted completed-Attack counter.</summary>
public sealed class Finisher : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 6m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage)
            .WithHitCount(Owner.PlayerCombatState!.AttackCardsPlayedThisTurn)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
    protected override void OnUpgrade() => _damage += 2m;
}
