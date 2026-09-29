using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust,抽3张,选1技能牌重复出3次。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.DecisionsDecisions</c>），
/// 选择经过选牌决策源。</summary>
public sealed class DecisionsDecisions : CardModel
{
    private int _draw = 3;
    private int _repeat = 3;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;

    protected override int CanonicalStarCost => 6;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardPileCmd.Draw(CombatState!, _draw, Owner, fromHandDraw: false);
        CardModel? selected = (await CardSelectCmd.FromHand(
            CombatState!, Owner, Owner.PlayerCombatState!.Hand.Cards.Where(c => c.Type == CardType.Skill && c != this && !c.HasKeyword(CardKeyword.Unplayable)),
            1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is null)
        {
            return;
        }

        // 上游统一传 target:null 且引擎自行定标；本项目引擎要求需要目标的卡必须有非空目标
        // （同偏离 #104）。被重复打出的技能牌可能是敌方单体目标，目标池空时按 BeatDown 的同款
        // 处理直接停止重复，而不是带 null 打出去。Plan 08b-6 Task 8 在种子 789 撞到该路径。
        for (int i = 0; i < _repeat; i++)
        {
            // Native CardCmd.AutoPlay rejects the card before choosing a random target.
            // A vetoed attempt still passes through the play pile to its result pile.
            if (selected.Keywords.Contains(CardKeyword.Unplayable) ||
                !Hook.ShouldPlay(CombatState!, selected, isAutoPlay: true))
            {
                CardPileCmd.Add(selected, PileType.Play);
                await selected.MoveToResultPileWithoutPlaying(CombatState!);
                continue;
            }

            Creature? target = selected.TargetType switch
            {
                TargetType.AnyEnemy or TargetType.RandomEnemy =>
                    CombatState!.RunState.Rng.CombatTargets.NextItem(CombatState!.HittableEnemies),
                TargetType.AnyAlly or TargetType.AnyPlayer =>
                    CombatState!.RunState.Rng.CombatTargets.NextItem(CombatState!.Allies),
                _ => null,
            };

            if (target is null &&
                selected.TargetType is TargetType.AnyEnemy or TargetType.RandomEnemy
                    or TargetType.AnyAlly or TargetType.AnyPlayer)
            {
                break;
            }

            await selected.AutoPlayPrevalidatedWithResultAsync(target);
        }
    }

    protected override void OnUpgrade() => _draw += 2;
}
