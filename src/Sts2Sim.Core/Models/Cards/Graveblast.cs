using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Graveblast</c>：消耗（升级移除），造成 4（升级 6）伤害，再从弃牌堆选 1 张放入手牌。</summary>
public sealed class Graveblast : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    private decimal Damage => IsUpgraded ? 6m : 4m;

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
        // 原版 CardSelectCmd.FromCombatPile 在战斗正在结束时直接返回空选择。
        if (CombatState!.IsOverOrEnding())
            return;
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(
            CombatState!, Owner, Owner.PlayerCombatState!.DiscardPile.Cards, 1, 1, this)).FirstOrDefault();
        if (selected is not null)
            CardPileCmd.Add(selected, PileType.Hand);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
