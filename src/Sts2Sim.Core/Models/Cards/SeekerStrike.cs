using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>9伤害,抽牌堆3张随机选1进手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.SeekerStrike</c>），
/// 候选先按上游 StableShuffle 使用 CombatCardSelection 流排序并洗牌，再由选牌决策源选择。
/// 最终选择由选牌决策源决定。</summary>
public sealed class SeekerStrike : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 9m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }
    // 偏离 #148：上游 CanonicalVars 的 CardsVar(3)，以具名数量常量承载。
    private const int CandidateCount = 3;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();

        List<CardModel> drawPile = Owner.PlayerCombatState!.DrawPile.Cards.ToList();
        List<CardModel> cardOptions = drawPile.StableShuffle(Owner.RunState.Rng.CombatCardSelection).Take(CandidateCount).ToList();
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(CombatState!, Owner,
            Owner.PlayerCombatState!.DrawPile.Cards.Where(card => cardOptions.Any(option => ReferenceEquals(option, card))),
            1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is not null)
        {
            CardPileCmd.Add(selected, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => _damage += 3m;
}
