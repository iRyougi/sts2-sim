using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>群体 Wah 攻击，伤害随本场全部牌堆里的 Cho/Ki 牌数量递增（v0.114.0 不再计 Wah）（原版 <c>WahSymphony</c>）。</summary>
public sealed class WahSymphony : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const decimal BaseDamage = 6m;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Wah];
    private decimal ExtraDamage => IsUpgraded ? 6m : 4m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    private decimal CalculatedDamage
    {
        get
        {
            int count = Owner?.PlayerCombatState?.AllPiles
                .SelectMany(p => p.Cards)
                .Count(c => c.Tags.Contains(CardTag.Cho) || c.Tags.Contains(CardTag.Ki)) ?? 0;
            return BaseDamage + ExtraDamage * count;
        }
    }

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = CalculatedDamage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await DamageCmd.Attack(CalculatedDamage).FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();
    }
}
