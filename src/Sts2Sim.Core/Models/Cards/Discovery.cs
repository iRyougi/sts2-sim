using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust，3选1随机生成卡免费进手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Discovery</c>），
/// 偏离 #97：省略真实源码的"本次出牌免费"（SetToFreeThisTurn）——
/// 本项目 CardModel.EnergyCost 没有"本回合临时费用修饰符"叠加链（偏离 #32 延续），生成卡进手牌后仍按其本身费用出牌。</summary>
public sealed class Discovery : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ICombatState combatState = CombatState!;
        IReadOnlyList<CardModel> generated = CardFactory.GetDistinctForCombat(
            Owner,
            Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.Players.Count > 1),
            3,
            combatState.RunState.Rng.CombatCardGeneration);

        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(combatState, Owner, generated, 0, 1, this, cancelable: true)).FirstOrDefault();
        if (selected is not null)
        {
            await CardPileCmd.Generate(combatState, selected, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
