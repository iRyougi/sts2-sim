using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>群体 Wah 攻击；若本回合打出过 Cho 或 Ki 则命中两次（原版 <c>WahBlast</c>）。</summary>
public sealed class WahBlast : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Wah];
    private decimal Damage => IsUpgraded ? 18m : 14m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int hitCount = MelodyCmd.PlayedChoThisTurn(Owner) || MelodyCmd.PlayedKiThisTurn(Owner) ? 2 : 1;
        await DamageCmd.Attack(Damage).WithHitCount(hitCount).FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();
    }
}
