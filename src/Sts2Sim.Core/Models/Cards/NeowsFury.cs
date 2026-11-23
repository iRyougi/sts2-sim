using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>NeowsFury</c>：造成伤害，再从弃牌堆选至多 2（升级 3）张放回手牌。</summary>
public sealed class NeowsFury : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 10m;
    private int _retrieveCount = 2;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.AnyEnemy;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();

        // 原版：从弃牌堆选 0..min(张数, 手牌空位) 张放回手牌（CardSelectCmd.FromCombatPile，可以少选），
        // 交给决策源选，不再自动取弃牌堆最前面的几张（原来的偏离 #158）
        int count = Math.Min(_retrieveCount, CardPile.MaxCardsInHand - Owner.PlayerCombatState!.Hand.Cards.Count);
        if (count > 0)
        {
            IReadOnlyList<CardModel> selected = await CardSelectCmd.FromCombatPile(
                CombatState!, Owner, Owner.PlayerCombatState.DiscardPile, 0, count, this);
            foreach (CardModel card in selected)
            {
                CardPileCmd.Add(card, PileType.Hand);
            }
        }
    }

    protected override void OnUpgrade()
    {
        _damage = 14m;
        _retrieveCount = 3;
    }
}
