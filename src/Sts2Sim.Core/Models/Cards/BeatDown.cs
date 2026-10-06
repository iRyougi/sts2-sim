using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>弃牌堆洗出3张攻击牌自动打出(随机目标)。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.BeatDown</c>），
/// 按原版过滤 Unplayable 攻击并 StableShuffle，只对 AnyEnemy 显式抽目标；
/// RandomEnemy 等其它类型传 null，由卡牌效果自行选择目标。</summary>
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
            if (CombatState!.IsOverOrEnding())
            {
                break;
            }

            Creature? target = null;
            if (card.TargetType == TargetType.AnyEnemy)
            {
                target = CombatState!.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies);
            }

            // Native BeatDown chooses its explicit target before CardCmd.AutoPlay's owner guard.
            if (card.Owner.Creature.IsDead)
            {
                continue;
            }
            // Native CardCmd.AutoPlay checks vetoes before refusing an unresolved AnyEnemy target.
            // A reviving primary enemy can keep combat live while HittableEnemies is empty.
            if (card.HasKeyword(CardKeyword.Unplayable) ||
                !Hook.ShouldPlay(CombatState!, card, isAutoPlay: true) ||
                card.TargetType == TargetType.AnyEnemy && target is null)
            {
                await card.MoveToResultPileWithoutPlaying(CombatState!);
                continue;
            }

            await card.AutoPlayPrevalidatedWithResultAsync(target);
        }
    }

    protected override void OnUpgrade() => _count += 1;
}
