using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>兼带 Wah 与 Ki 标签的两段攻击，再从弃牌堆选 1 张置于抽牌堆顶（v0.114.0 原版 <c>WahKi</c>）。</summary>
public sealed class WahKi : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const int Hits = 2;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Wah, CardTag.Ki];
    private decimal Damage => IsUpgraded ? 4m : 3m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).WithHitCount(Hits).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        CardModel? selected = (await CardSelectCmd.FromCombatPile(
            CombatState!,
            Owner,
            Owner.PlayerCombatState!.DiscardPile,
            1,
            1,
            this,
            cancelable: false)).FirstOrDefault();
        if (selected is not null)
        {
            CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
        }
    }
}
