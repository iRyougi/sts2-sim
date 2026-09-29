using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>任意数量手牌转化为 MinionSacrifice。逐字移植结构（<c>MegaCrit.Sts2.Core.Models.Cards.Guards</c>），
/// 偏离 #96：真实效果允许玩家自选0~全部张数,本项目没有交互式选择,确定性策略取"不选任何牌"
/// （0张）——比"默认全选"更保守,不会让自动策略把整手牌无脑喂给一张收益不确定的卡；
/// 需要真实策略时后续接入 RL 决策面即可覆盖这个默认值。</summary>
public sealed class Guards : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 2;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override Task OnPlay(CardPlay cardPlay) => Task.CompletedTask;
}
