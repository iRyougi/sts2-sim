using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>暗淡蓝点，本回合打出满5张卡时（每回合只触发一次）获得等于层数的
/// <see cref="DrawCardsNextTurnPower"/>。逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.PaleBlueDotPower</c>），
/// 偏离 #106：真实源码用 <c>CombatManager.History</c> 回查"本回合已出牌数"，本项目改用
/// <see cref="Entities.Players.PlayerCombatState.CardsPlayedThisTurn"/> 计数器；触发阈值(5)是真实源码里的
/// 每回合 5 次的 CardPlay 动态变量，本项目没有可升级的 DynamicVar 包装，直接硬编码常量。</summary>
public sealed class PaleBlueDotPower : PowerModel
{
    private const int Threshold = 5;

    private bool _alreadyActivatedThisTurn;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player || _alreadyActivatedThisTurn)
        {
            return;
        }

        if (Owner.Player!.PlayerCombatState!.CardsPlayedThisTurn >= Threshold)
        {
            _alreadyActivatedThisTurn = true;
            await PowerCmd.Apply<DrawCardsNextTurnPower>(Owner.CombatState!, Owner, Amount, Owner, null);
        }
    }

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            _alreadyActivatedThisTurn = false;
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_alreadyActivatedThisTurn);
    }
}
