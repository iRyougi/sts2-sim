namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

public sealed class SlothPower : PowerModel
{
    private int _cardsPlayed;
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    /// <summary>偏离 #316（2026-09-08 修复）：上游签名是
    /// 上游签名的第二个参数是自动出牌类型，且被**显式忽略**。
    /// 本仓库对应参数是 <paramref name="isAutoPlay"/>，此前被误当成"当前折叠值"用，
    /// 错误地将该参数当成当前折叠值使用。
    ///
    /// <see cref="Hooks.Hook.ShouldPlay"/> 是 veto 折叠（任一监听器返回 false 即整体 false），
    /// 而普通出牌路径（<c>CardModel.CanPlay</c> 与 <c>CardModel.PlayAsync</c> 两个调用点）
    /// 传 <c>isAutoPlay: false</c>——于是场上只要有**任何**生物带 Sloth，
    /// **所有玩家的所有普通出牌都会被拒绝**，而不只是 Sloth 持有者每回合限 3 张。</summary>
    public override bool ShouldPlay(CardModel card, bool isAutoPlay) =>
        card.Owner.Creature != Owner || _cardsPlayed < Amount;
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner) _cardsPlayed++;
        return Task.CompletedTask;
    }
    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        // 上游判据是 participants.Contains(Owner)，不是阵营相同——Owner 不在本次参与者
        // 列表里时（已离场/未参与该侧回合）不应重置计数。
        if (participants.Contains(Owner)) _cardsPlayed = 0;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_cardsPlayed);
}
