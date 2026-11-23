using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>随机自动打出抽牌堆2张可用卡。逐字移植选牌行为（<c>MegaCrit.Sts2.Core.Models.Cards.Catastrophe</c>）。
/// 偏离 #104：真实源码对被自动打出的卡统一传 target:null；本项目引擎对 AnyEnemy 等单体卡
/// 仍要求非空目标，因此仅这些类型先用 <c>CombatTargets</c> 显式定标。RandomEnemy 由牌的效果自行抽目标。</summary>
public sealed class Catastrophe : CardModel
{
    private int _count = 2;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        for (int i = 0; i < _count; i++)
        {
            List<CardModel> candidates = Owner.PlayerCombatState!.DrawPile.Cards
                .Where(c => !c.Keywords.Contains(CardKeyword.Unplayable))
                .ToList()
                .StableShuffle(CombatState!.RunState.Rng.Shuffle);
            CardModel? picked = candidates.FirstOrDefault() ?? Owner.PlayerCombatState.DrawPile.Cards
                .ToList()
                .StableShuffle(CombatState.RunState.Rng.Shuffle)
                .FirstOrDefault();
            if (picked is null)
            {
                continue;
            }

            // Native CardCmd.AutoPlay checks this before moving cards or choosing targets.
            // Keep selecting on later iterations, as native Catastrophe does, but do not play.
            if (CombatState!.IsOverOrEnding() || picked.Owner.Creature.IsDead)
            {
                continue;
            }

            // Native CardCmd.AutoPlay moves an Unplayable fallback pick to its result pile
            // without running OnPlay. This direct AutoPlayAsync path needs the same guard.
            if (picked.Keywords.Contains(CardKeyword.Unplayable))
            {
                await picked.MoveToResultPileWithoutPlaying(CombatState!);
                continue;
            }

            Creature? target = picked.TargetType switch
            {
                TargetType.AnyEnemy =>
                    CombatState!.RunState.Rng.CombatTargets.NextItem(CombatState!.HittableEnemies),
                TargetType.AnyAlly or TargetType.AnyPlayer =>
                    CombatState!.RunState.Rng.CombatTargets.NextItem(CombatState!.Allies),
                _ => null,
            };

            // 偏离 #104 的补充：显式定标类型的目标池可能为空（例如敌人已全部死亡），
            // Rng.NextItem 在空集合上返回 null，而这些卡会在自己的 OnPlay 里抛异常。
            // RandomEnemy 不在此列；其效果可自行选择目标，不应预先消耗 CombatTargets。
            // Plan 08b-6 Task 8 的 A10 随机策略在种子 52 撞到该路径。
            if (target is null &&
                picked.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly or TargetType.AnyPlayer)
            {
                continue;
            }

            await picked.AutoPlayAsync(target);
        }
    }

    protected override void OnUpgrade() => _count += 1;
}
