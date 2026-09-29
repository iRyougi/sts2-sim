using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Precise Cut: base damage minus two per other card in hand.</summary>
public sealed class PreciseCut : CardModel, ICardDamageVariableProvider
{
    private decimal _baseDamage = 13m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        // CalculatedDamage.Calculate(null) excludes this card when it remains in hand.
        int otherCardsInHand = CombatState is null ? 0 :
            Owner.PlayerCombatState!.Hand.Cards.Count - (Pile?.Type == PileType.Hand ? 1 : 0);
        amount = _baseDamage - 2m * otherCardsInHand;
        return true;
    }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal damage = _baseDamage - 2m * Owner.PlayerCombatState!.Hand.Cards.Count;
        await DamageCmd.Attack(damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
    protected override void OnUpgrade() => _baseDamage += 3m;
}
