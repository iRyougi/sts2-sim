using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>若攻击前目标已被穿刺，攻击后获得能量（原版 <c>BowSmack</c>）。</summary>
public sealed class BowSmack : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const int Energy = 1;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    private decimal Damage => IsUpgraded ? 11m : 8m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Energy: Energy);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool wasImpaled = ArrowCmd.CreatureIsImpaledByArrow(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        if (wasImpaled)
        {
            await PlayerCmd.GainEnergy(Energy, Owner);
        }
    }
}
