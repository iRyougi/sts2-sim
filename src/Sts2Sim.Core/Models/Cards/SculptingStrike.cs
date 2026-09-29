using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>SculptingStrike</c>：1 费 Strike，造成 9（升级 12）伤害，然后从手牌选 1 张
/// 本身不带虚无的牌，使其获得虚无。</summary>
public sealed class SculptingStrike : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Strike];

    private decimal Damage => IsUpgraded ? 12m : 9m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();

        // 原版过滤只看卡牌自身关键字：Hexed 附加了虚无的手牌仍可被选中。
        CardModel? selected = (await CardSelectCmd.FromHand(CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards.Where(card => !card.LocalKeywords.Contains(CardKeyword.Ethereal)),
            1, 1, this)).FirstOrDefault();
        selected?.AddKeywordInternal(CardKeyword.Ethereal);
    }
}
