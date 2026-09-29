using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>弃牌堆洗出3张攻击牌自动打出(随机目标)。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.BeatDown</c>），
/// 按原版过滤 Unplayable 攻击并 StableShuffle。偏离 #104：RandomEnemy 候选显式从
/// CombatTargets 选目标；原版只对 AnyEnemy 显式抽目标，其他类型传 null 交给底层定标。</summary>
public sealed class BeatDown : CardModel
{
    private int _count = 3;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.RandomEnemy;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 3;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        List<CardModel> candidates = Owner.PlayerCombatState!.DiscardPile.Cards
            .Where(c => c.Type == CardType.Attack && !c.Keywords.Contains(CardKeyword.Unplayable))
            .ToList()
            .StableShuffle(CombatState!.RunState.Rng.Shuffle);

        foreach (CardModel card in candidates.Take(_count))
        {
            Creature? target = null;
            if (card.TargetType is TargetType.AnyEnemy or TargetType.RandomEnemy)
            {
                target = CombatState!.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies);
                if (target is null)
                {
                    break;
                }
            }

            await card.AutoPlayAsync(target);
        }
    }

    protected override void OnUpgrade() => _count += 1;
}
