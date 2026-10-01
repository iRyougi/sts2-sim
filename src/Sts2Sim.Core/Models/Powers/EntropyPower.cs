using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>熵，所属玩家回合开始时把手牌里等于层数的卡各转化成一张随机卡。
/// 对应 <c>MegaCrit.Sts2.Core.Models.Powers.EntropyPower</c>；每张牌的随机变换使用
/// <see cref="CardCmd.TransformToRandom"/>，从原牌所属卡池取候选。</summary>
public sealed class EntropyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        ICombatState combatState = Owner.CombatState!;
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(combatState, player,
            player.PlayerCombatState!.Hand.Cards, Amount, Amount, this, cancelable: false);
        foreach (CardModel card in selected)
        {
            await CardCmd.TransformToRandom(card, player.RunState.Rng.CombatCardSelection,
                player.RunState);
        }
    }
}
