using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>每回合每种首次开始的旋律在打出完成时抽对应层数张牌（原版 <c>MotifPower</c>）。</summary>
public sealed class MotifPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card.Owner, Owner.Player) || Owner.Player?.PlayerCombatState is not { } pcs)
            return;

        int triggers = 0;
        if (cardPlay.Card.Tags.Contains(CardTag.Wah) && pcs.WahStartedThisTurn == 1)
            triggers++;
        if (cardPlay.Card.Tags.Contains(CardTag.Cho) && pcs.ChoStartedThisTurn == 1)
            triggers++;
        if (cardPlay.Card.Tags.Contains(CardTag.Ki) && pcs.KiStartedThisTurn == 1)
            triggers++;

        if (triggers > 0)
        {
            await CardPileCmd.Draw(Owner.CombatState!, triggers * Amount, Owner.Player!, fromHandDraw: false);
        }
    }
}
