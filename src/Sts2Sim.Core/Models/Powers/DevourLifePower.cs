using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>DevourLifePower</c>：持有者打出 <see cref="Soul"/> 后召唤层数。</summary>
public sealed class DevourLifePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card is Soul && cardPlay.Card.Owner.Creature == Owner)
            await OstyCmd.Summon(cardPlay.Card.Owner, Amount, this);
    }
}
