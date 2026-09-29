using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust,当前格挡转为下回合格挡。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Prolong</c>）：
/// 把玩家当前的格挡值原样授予 BlockNextTurnPower（不清空当前格挡本身,和真实源码一致——
/// 真实效果是"额外获得等量的下回合格挡”，不是"转移"）。升级后移除 Exhaust。</summary>
public sealed class Prolong : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<BlockNextTurnPower>(CombatState!, Owner.Creature, Owner.Creature.Block, Owner.Creature, this);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
