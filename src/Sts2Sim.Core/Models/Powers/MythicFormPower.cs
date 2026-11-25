using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>抽牌前将层数张随机塔罗牌加入手牌（v0.114.0 原版 <c>MythicFormPower</c>；删除了升级计数）。</summary>
public sealed class MythicFormPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(Player player)
    {
        if (!ReferenceEquals(player, Owner.Player))
            return;

        ICombatState combatState = Owner.CombatState!;
        for (int i = 0; i < Amount; i++)
        {
            CardModel? canonical = player.RunState.Rng.CombatCardSelection.NextItem(
                TarotCards.All.Where(c => c.IsAllowedInRun(player.RunState)));
            if (canonical is null)
                continue;

            var card = (CardModel)canonical.MutableClone();
            card.AssignOwner(player);
            await CardPileCmd.Generate(combatState, card, PileType.Hand, player);
        }
    }
}
