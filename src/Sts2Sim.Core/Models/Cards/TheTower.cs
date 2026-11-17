using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>造成本局当前楼层数 × 2（升级 × 3）点伤害并消耗（原版 <c>TheTower</c>）。</summary>
public sealed class TheTower : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private decimal ExtraDamage => IsUpgraded ? 3m : 2m;
    private decimal CalculatedDamage => HasOwner ? ExtraDamage * Owner.RunState.TotalFloor : 0m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = CalculatedDamage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(CalculatedDamage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
}
