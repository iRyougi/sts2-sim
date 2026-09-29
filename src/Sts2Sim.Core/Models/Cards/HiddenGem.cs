using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>随机1张抽牌堆卡+2次重打(Replay)。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.HiddenGem</c>），
/// 偏离 #105：真实源码先排除带 Unplayable 关键字的卡，本项目没有这个关键字（延续偏离 #85），跳过该过滤；
/// 优先选 Attack/Skill/Power 类型（排除 Status/Curse/Quest），若都不满足再退回全部候选；用
/// `RunState.Rng.CombatCardSelection` 随机选1张，再给它的 <see cref="CardModel.BaseReplayCount"/> 加值，
/// 之后每次打出那张卡都会自动重放对应次数（见 <see cref="CardModel.PlayAsync"/>）。省略真实源码的
/// 出牌动画与 `CardCmd.Preview`。</summary>
public sealed class HiddenGem : CardModel
{
    private int _replay = 2;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    public override bool CanBeGeneratedInCombat => false;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        List<CardModel> eligible = Owner.PlayerCombatState!.DrawPile.Cards
            .Where(c => !c.HasKeyword(CardKeyword.Unplayable)
                && c.Type != CardType.Status && c.Type != CardType.Curse && c.Type != CardType.Quest
                && c.BaseReplayCount < 1)
            .ToList();
        if (eligible.Count == 0)
        {
            return Task.CompletedTask;
        }

        List<CardModel> preferred = eligible
            .Where(c => c.Type is CardType.Attack or CardType.Skill or CardType.Power)
            .ToList();
        IEnumerable<CardModel> candidates = preferred.Count == 0 ? eligible : preferred;
        CardModel? selected = CombatState!.RunState.Rng.CombatCardSelection.NextItem(candidates);
        if (selected is not null)
        {
            selected.BaseReplayCount += _replay;
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => _replay += 1;
}
