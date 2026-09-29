using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>熵，自己回合开始时把手牌里等于层数的卡各转化成一张随机卡。逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.EntropyPower</c>），
/// 偏离：真实源码触发点是"这名玩家的回合开始"
/// （`AfterPlayerTurnStart`），本项目没有单独的玩家回合开始 hook，用 <see cref="AfterSideTurnStart"/> 过滤到
/// 自己一侧近似替代（沿用 Furnace/Reflect/Tyranny 的既有模式）；每张牌的随机变换使用
/// <see cref="CardCmd.TransformToRandom"/>，从原牌所属卡池取候选。</summary>
public sealed class EntropyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        ICombatState combatState = Owner.CombatState!;
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(combatState, Owner.Player!,
            Owner.Player!.PlayerCombatState!.Hand.Cards, Amount, Amount, this, cancelable: false);
        foreach (CardModel card in selected)
        {
            await CardCmd.TransformToRandom(card, combatState.RunState.Rng.CombatCardSelection,
                combatState.RunState);
        }
    }
}
