using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>本回合每打出一种不同旋律减费 2 的高伤终曲（原版 <c>Coda</c>）。</summary>
public sealed class Coda : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    protected override bool ShouldGlowGoldInternal => MelodyCmd.MelodiesThisTurn(Owner) > 0;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 6;
    private decimal Damage => IsUpgraded ? 55m : 40m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || originalCost <= 0m)
            return false;
        int reduction = 2 * MelodyCmd.MelodiesThisTurn(Owner);
        if (reduction == 0)
            return false;

        modifiedCost = Math.Max(0m, originalCost - reduction);
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
}
