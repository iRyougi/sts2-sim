using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>本回合接下来的旋律牌额外打出 1 次（原版 <c>KiVersePower</c>）。</summary>
public sealed class KiVersePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (!ReferenceEquals(card.Owner.Creature, Owner))
            return playCount;
        // v0.114.0：只对 Wah/Cho 牌生效，仅 Ki 牌不再重放。
        if (!card.Tags.Contains(CardTag.Wah) && !card.Tags.Contains(CardTag.Cho))
            return playCount;
        return playCount + 1;
    }

    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }
}
