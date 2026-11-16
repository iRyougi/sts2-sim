using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>群体 Wah 攻击（原版 <c>WahSweep</c>）。</summary>
public sealed class WahSweep : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Wah];
    private decimal Damage => IsUpgraded ? 11m : 8m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();
    }
}
