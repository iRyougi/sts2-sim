using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class PhantomBladesPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    /// <summary>原版 v0.113.1：能力存续期间由全局关键字钩子给己方 Shiv 提供保留，不写入卡牌；能力移除即失效，
    /// 不影响其他来源的保留（#922）。原版 AfterApplied/AfterRemoved 只发 UI 刷新事件，模拟器不需要。</summary>
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (card.Owner != Owner.Player)
        {
            return false;
        }
        if (!card.Tags.Contains(CardTag.Shiv))
        {
            return false;
        }
        return keywords.Add(CardKeyword.Retain);
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!props.IsPoweredAttack() || cardSource?.Tags.Contains(CardTag.Shiv) != true || dealer != Owner)
        {
            return 0m;
        }

        return Owner.Player!.PlayerCombatState!.ShivsPlayedThisTurn == 0 ? Amount : 0m;
    }
}
