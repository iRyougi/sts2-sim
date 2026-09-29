using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class MementoMori : CardModel, ICardDamageVariableProvider
{
    private decimal _baseDamage = 9m;
    private decimal _extraDamage = 4m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        // Native CalculatedDamage.Calculate(null) counts this owner's discards this turn.
        amount = _baseDamage + (CombatState is null ? 0m :
            _extraDamage * Owner.PlayerCombatState!.CardsDiscardedThisTurn);
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int discardedThisTurn = Owner.PlayerCombatState!.CardsDiscardedThisTurn;
        await DamageCmd.Attack(_baseDamage + _extraDamage * discardedThisTurn).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
    protected override void OnUpgrade() { _baseDamage += 2m; _extraDamage += 1m; }
}
